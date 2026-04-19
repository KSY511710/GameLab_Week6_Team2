using System.Collections;
using System.Collections.Generic;
using Special.Data;
using Special.Effects;
using Special.Runtime;
using TMPro;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// 특수 블럭이 보드에 설치되는 순간, 그 블럭의 영향 범위와 효과 계산을 시각적으로 보여준다.
    /// PowerAnimationSequencer 의 자매 클래스 — 같은 IsAnimating 게이트를 공유해 충돌을 피한다.
    ///
    /// 흐름
    ///  1. SpecialBlockRegistry.OnSpecialPlaced 구독 → 큐잉
    ///  2. PowerAnimationSequencer 가 진행 중이면 양보 (IsAnimating)
    ///  3. 특수 블럭의 footprint 를 플래시
    ///  4. 효과별로 영역 오버레이 + 계산 단계 텍스트를 차례로 보여줌
    ///  5. 영향 받은 발전소 멤버를 추가로 플래시
    ///
    /// 효과별 표현은 EffectAsset.BuildPreview() 가 결정한다 — 새 효과를 추가해도 sequencer 코드는 변경 없음.
    /// </summary>
    public class SpecialPlacementSequencer : MonoBehaviour
    {
        public static SpecialPlacementSequencer Instance;

        [Header("UI")]
        [Tooltip("계산 단계가 출력될 패널. 비어 있으면 sequencer 는 콘솔로만 로그를 남긴다.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI calcStepText;

        [Header("Pacing")]
        [SerializeField, Min(0.05f)] private float stepSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float flashDuration = 0.4f;
        [SerializeField, Min(0f)] private float postFinalDelay = 0.6f;
        [SerializeField, Min(0f)] private float overlayHoldExtra = 0.4f;
        [SerializeField, Min(0f)] private float waitBetweenPlacements = 0.1f;
        [SerializeField, Min(0f)] private float spotlightSettleDelay = 0.1f;

        [Header("Scope Overlay")]
        [Tooltip("영역 오버레이로 사용할 1셀 짜리 정사각 스프라이트. 비우면 1x1 흰색을 자동 생성.")]
        [SerializeField] private Sprite overlayCellSprite;
        [Tooltip("오버레이 SpriteRenderer 의 정렬 순서. 바닥 타일 위, 외곽선 아래 권장.")]
        [SerializeField] private int overlaySortingOrder = 50;

        [Header("Flash Colors")]
        [SerializeField] private Color footprintFlashColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] private Color affectedFlashColor = new Color(0.6f, 1f, 0.9f);

        private readonly Queue<SpecialBlockInstance> queue = new Queue<SpecialBlockInstance>();
        private bool isPlaying;
        private GridManager grid;
        private ScopeOverlayRenderer overlay;
        private bool subscribed;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            grid = Object.FindFirstObjectByType<GridManager>();
            overlay = new ScopeOverlayRenderer(transform, overlayCellSprite, overlaySortingOrder);
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            // SpecialBlockRegistry 는 lazy-create 싱글턴이라 이 시점엔 항상 살아 있다.
            SpecialBlockRegistry.Instance.OnSpecialPlaced += HandlePlaced;
            subscribed = true;
        }

        private void OnDisable()
        {
            if (subscribed && SpecialBlockRegistry.Instance != null)
            {
                SpecialBlockRegistry.Instance.OnSpecialPlaced -= HandlePlaced;
                subscribed = false;
            }

            if (!isPlaying) return;
            StopAllCoroutines();
            isPlaying = false;
            queue.Clear();
            overlay?.Hide();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        }

        private void HandlePlaced(SpecialBlockInstance inst)
        {
            if (inst == null) return;
            queue.Enqueue(inst);
            if (!isPlaying) StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            isPlaying = true;
            while (queue.Count > 0)
            {
                // PowerAnimationSequencer 가 같은 프레임에 IsAnimating 을 잡으러 들어가는 경우가 있어
                // 한 프레임 양보 후 그 결과를 보고 대기한다.
                yield return null;
                while (PowerManager.Instance != null && PowerManager.Instance.IsAnimating) yield return null;

                SpecialBlockInstance inst = queue.Dequeue();
                if (inst == null) continue;

                yield return PlayOne(inst);

                if (waitBetweenPlacements > 0f) yield return new WaitForSeconds(waitBetweenPlacements);
            }
            isPlaying = false;
        }

        private IEnumerator PlayOne(SpecialBlockInstance inst)
        {
            if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
            if (panelRoot != null) panelRoot.SetActive(true);

            SpecialBlockDefinition def = inst.definition;
            string title = string.IsNullOrEmpty(def.displayName) ? def.id : def.displayName;
            string header = $"<size=30><b>✨ {title} 설치!</b></size>\n";

            List<PlacedBlockVisual> footprintVisuals = CollectVisuals(inst.footprint);

            // 0) 우선 footprint 자체를 플래시 — 어디 놓였는지 시선이 잡히도록.
            FlashSubset(footprintVisuals, footprintFlashColor);
            SetCalcText(header + "\n<size=22>설치 위치 확인...</size>");
            yield return new WaitForSeconds(flashDuration);

            EffectAsset[] effects = def.effectAssets;
            if (effects == null || effects.Length == 0)
            {
                SetCalcText(header + "\n<size=22>(연결된 효과 없음)</size>");
                yield return new WaitForSeconds(stepSeconds);
            }
            else
            {
                for (int i = 0; i < effects.Length; i++)
                {
                    EffectAsset eff = effects[i];
                    if (eff == null) continue;
                    yield return PlayEffect(inst, eff, header, i + 1, effects.Length);
                }
            }

            // 마지막으로 "효과가 활성화되어 매 정산마다 적용됨" 을 명시.
            SetCalcText(header + $"\n<size=24><color=#FFFF66><b>▶ 효과 등록 완료</b></color></size>\n" +
                                 $"<size=18>이후 정산/그룹 형성 시 자동 적용됩니다.</size>");
            yield return new WaitForSeconds(postFinalDelay);

            overlay.Hide();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        }

        private IEnumerator PlayEffect(SpecialBlockInstance inst, EffectAsset eff, string header, int index, int count)
        {
            EffectPreview preview;
            try
            {
                preview = eff.BuildPreview(inst);
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                preview = new EffectPreview { title = eff.DisplayName };
                preview.steps.Add("(효과 프리뷰 계산 실패 — 콘솔 확인)");
            }

            overlay.Show(preview.scopeCells, preview.overlayColor, grid);
            if (spotlightSettleDelay > 0f) yield return new WaitForSeconds(spotlightSettleDelay);

            // 영향 받은 셀(보드 위 발전소 멤버) 을 동시에 플래시.
            FlashSubset(CollectVisuals(preview.impactCells), affectedFlashColor);

            string text = header + $"\n<size=22><b>[{index}/{count}] {preview.title}</b></size>";
            SetCalcText(text);
            yield return new WaitForSeconds(stepSeconds);

            for (int s = 0; s < preview.steps.Count; s++)
            {
                text += $"\n{preview.steps[s]}";
                SetCalcText(text);
                yield return new WaitForSeconds(stepSeconds);
            }

            if (overlayHoldExtra > 0f) yield return new WaitForSeconds(overlayHoldExtra);
            overlay.Hide();
        }

        private List<PlacedBlockVisual> CollectVisuals(IReadOnlyList<Vector2Int> arrayCells)
        {
            List<PlacedBlockVisual> list = new List<PlacedBlockVisual>();
            if (arrayCells == null || grid == null) return list;
            for (int i = 0; i < arrayCells.Count; i++)
            {
                BlockData bd = grid.GetBlockAtArrayIndex(arrayCells[i]);
                if (bd == null || bd.blockObject == null) continue;
                PlacedBlockVisual v = bd.blockObject.GetComponent<PlacedBlockVisual>();
                if (v != null) list.Add(v);
            }
            return list;
        }

        private void FlashSubset(List<PlacedBlockVisual> members, Color color)
        {
            if (members == null) return;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] != null) members[i].PlayFlash(color, flashDuration);
            }
        }

        private void SetCalcText(string s)
        {
            if (calcStepText != null) calcStepText.text = s;
            else Debug.Log($"[SpecialPlacementSequencer] {s}");
        }
    }
}
