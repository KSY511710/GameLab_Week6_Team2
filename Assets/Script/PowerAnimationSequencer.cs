using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Special.Data;
using Special.Effects;
using Special.Integration;
using Special.Runtime;
using TMPro;
using UI.Info;
using UnityEngine;

/// <summary>
/// 세 종류의 시퀀스를 주관한다.
///  1. <see cref="EnqueueAnimation"/>         — 새 발전소 완성 연출.
///  2. <see cref="EnqueueSpecialPlacement"/>  — 특수 블럭 설치 시 효과 계산 과정 연출.
///  3. <see cref="PlayDayEndSequence"/>       — "다음 일차" 일일 정산 종합 연출.
///
/// 발전소 완성과 특수 설치는 단일 큐(<see cref="sequenceQueue"/>)로 직렬화되어
/// 이전에 두 시퀀서가 <c>IsAnimating</c> 게이트를 두고 경합하던 문제를 제거한다.
/// 일일 정산은 여전히 독립 코루틴이며 큐 진행 중엔 콜백만 수행해 게임 진행을 막지 않는다.
///
/// 두 시퀀스 모두 공용 스포트라이트 API(<see cref="PlacedBlockVisual.SetSpotlight"/>)를 통해
/// "지금 주목해야 할 발전소"를 밝기 편차로 가리킨다. 나머지 보드는 <c>Dimmed</c>로 깔린다.
/// 진행 중엔 <c>PowerManager.IsAnimating = true</c>로 입력/Skip을 차단하고, 종료 시 해제한다.
/// </summary>
public class PowerAnimationSequencer : MonoBehaviour
{
    public static PowerAnimationSequencer Instance;

    [Header("Pacing (Group Completed)")]
    [Tooltip("발전소 완성 팝업의 단계별 대기 시간.")]
    [SerializeField, Min(0.05f)] private float stepSeconds = 0.8f;
    [Tooltip("그룹 멤버에 주는 플래시 펄스 길이.")]
    [SerializeField, Min(0.05f)] private float highlightDuration = 0.4f;
    [Tooltip("발전소 완성 팝업의 최종 프레임 유지 시간.")]
    [SerializeField, Min(0f)] private float postFinalDelay = 1.0f;
    [Tooltip("스포트라이트 전환 후 플래시 직전까지 기다리는 정착 시간.")]
    [SerializeField, Min(0f)] private float spotlightSettleDelay = 0.12f;

    [Header("Day End Pacing")]
    [Tooltip("일일 정산에서 각 발전소 줄이 찍히는 간격.")]
    [SerializeField, Min(0.05f)] private float dayEndPerEntryDelay = 0.3f;
    [Tooltip("일일 정산에서 각 발전소/미연결 강조 플래시 길이.")]
    [SerializeField, Min(0.05f)] private float dayEndFlashDuration = 0.2f;
    [Tooltip("일일 정산 총합 표시 후 패널이 닫히기 전까지 유지 시간.")]
    [SerializeField, Min(0f)] private float dayEndPostTotalDelay = 1.5f;

    [Header("Pacing (Special Placement)")]
    [SerializeField, Min(0.05f)] private float specialStepSeconds = 0.7f;
    [SerializeField, Min(0.05f)] private float specialFlashDuration = 0.4f;
    [SerializeField, Min(0f)] private float specialPostFinalDelay = 0.6f;
    [SerializeField, Min(0f)] private float specialOverlayHoldExtra = 0.4f;
    [SerializeField, Min(0f)] private float specialSpotlightSettleDelay = 0.1f;
    [Tooltip("큐 안에서 각 연출 사이 여백. 0이면 붙여 재생.")]
    [SerializeField, Min(0f)] private float waitBetweenPlacements = 0.1f;

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI calcStepText;

    [Header("Visual")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.4f);
    [SerializeField] private Color ungroupedHighlightColor = new Color(0.7f, 0.7f, 0.7f);

    [Header("Special Placement Visual")]
    [Tooltip("영역 오버레이로 사용할 1셀 짜리 정사각 스프라이트. 비우면 1x1 흰색을 자동 생성.")]
    [SerializeField] private Sprite overlayCellSprite;
    [Tooltip("오버레이 SpriteRenderer 의 정렬 순서. 바닥 타일 위, 외곽선 아래 권장.")]
    [SerializeField] private int overlaySortingOrder = 50;
    [SerializeField] private Color footprintFlashColor = new Color(1f, 0.95f, 0.4f);
    [SerializeField] private Color affectedFlashColor = new Color(0.6f, 1f, 0.9f);

    private enum PendingKind { GroupCompleted, SpecialPlaced }

    private sealed class PendingSequence
    {
        public PendingKind kind;
        public GroupInfo group;                  // kind == GroupCompleted
        public SpecialBlockInstance special;     // kind == SpecialPlaced
    }

    private readonly Queue<PendingSequence> sequenceQueue = new Queue<PendingSequence>();
    private bool isPlaying;

    // 시퀀스 동안 스포트라이트를 토글할 대상들. 시퀀스 시작 시 스냅샷을 뜨고,
    // 종료/인터럽트 시점에 이 리스트로 전원 Normal 복귀를 보장한다.
    private readonly List<PlacedBlockVisual> activeVisuals = new List<PlacedBlockVisual>();
    private readonly HashSet<PlacedBlockVisual> focusSetScratch = new HashSet<PlacedBlockVisual>();

    private GridManager gridRef;
    private ScopeOverlayRenderer overlay;
    private bool subscribedToRegistry;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        gridRef = FindFirstObjectByType<GridManager>();
        overlay = new ScopeOverlayRenderer(transform, overlayCellSprite, overlaySortingOrder);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        // SpecialBlockRegistry 는 lazy-create 싱글턴이라 이 시점엔 항상 살아 있다.
        if (SpecialBlockRegistry.Instance != null)
        {
            SpecialBlockRegistry.Instance.OnSpecialPlaced += HandleSpecialPlaced;
            subscribedToRegistry = true;
        }
    }

    // =========================================================
    // 1. 발전소 완성 팝업 — 블럭 배치로 새 그룹이 생기는 즉시 큐잉
    // =========================================================
    public void EnqueueAnimation(GroupInfo newGroup)
    {
        if (newGroup == null) return;
        sequenceQueue.Enqueue(new PendingSequence { kind = PendingKind.GroupCompleted, group = newGroup });
        if (!isPlaying) StartCoroutine(RunSequenceQueueRoutine());
    }

    // =========================================================
    // 2. 특수 블럭 설치 연출 — SpecialBlockRegistry.OnSpecialPlaced 에서 유입
    // =========================================================
    public void EnqueueSpecialPlacement(SpecialBlockInstance inst)
    {
        if (inst == null) return;
        sequenceQueue.Enqueue(new PendingSequence { kind = PendingKind.SpecialPlaced, special = inst });
        if (!isPlaying) StartCoroutine(RunSequenceQueueRoutine());
    }

    private void HandleSpecialPlaced(SpecialBlockInstance inst) => EnqueueSpecialPlacement(inst);

    private IEnumerator RunSequenceQueueRoutine()
    {
        isPlaying = true;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
        if (panelRoot != null) panelRoot.SetActive(true);

        // EnqueueAnimation 은 CheckAndFormGroups 호출 스택에서 불린다. 바로 이어서
        // GridManager 가 CalculateTotalPower(LastUngroupedVisuals 갱신)를 실행하므로,
        // 보드 스냅샷을 뜨기 전 한 프레임 양보해 최신 상태에서 스포트라이트를 적용한다.
        yield return null;

        while (sequenceQueue.Count > 0)
        {
            PendingSequence p = sequenceQueue.Dequeue();
            if (p == null) continue;

            SetCalcText("");
            CollectActiveVisualsFromBoard();

            if (p.kind == PendingKind.GroupCompleted && p.group != null)
                yield return PlayGroupCompletedRoutine(p.group);
            else if (p.kind == PendingKind.SpecialPlaced && p.special != null)
                yield return PlaySpecialPlacementRoutine(p.special);

            // 다음 큐 아이템(또는 종료) 전에 스포트라이트를 원상태로 — 큐에 남아있다면 다음 루프에서 재설정한다.
            RestoreSpotlightToNormal();

            if (sequenceQueue.Count > 0 && waitBetweenPlacements > 0f)
                yield return new WaitForSeconds(waitBetweenPlacements);
        }

        overlay?.Hide();
        if (panelRoot != null) panelRoot.SetActive(false);
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        isPlaying = false;
    }

    private IEnumerator PlayGroupCompletedRoutine(GroupInfo g)
    {
        FocusSubset(g.members);
        if (spotlightSettleDelay > 0f) yield return new WaitForSeconds(spotlightSettleDelay);

        FlashSubset(g.members, highlightColor, highlightDuration);
        yield return new WaitForSeconds(highlightDuration);

        string header = $"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n";

        if (g.lastTrace != null && g.lastTrace.Steps.Count > 0)
        {
            yield return PlayTraceStages(g, header);
        }
        else
        {
            // Trace 가 유실된 경우의 폴백 — 라이브 경로에서 Trace=null 로 호출됐을 때만 해당.
            SetCalcText(header + $"기본 {g.baseProduction} + 부품 {g.uniqueParts}종\n" +
                        $"= <color=#FFE066>{g.baseProduction + g.uniqueParts}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText(header + $"형태 시너지 보너스\n= <color=#66D9FF>x {g.completionMultiplier}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText(header + $"색상 순도 배율\n= <color=#FF99CC>x {g.colorMultiplier:F2}</color>");
            yield return new WaitForSeconds(stepSeconds);
        }

        SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 가동 시작!</b></size>\n\n" +
                    $"최종 발전량\n<color=#FFFF00><size=40><b>+ {Mathf.Round(g.groupPower)} GWh</b></size></color>");
        yield return new WaitForSeconds(postFinalDelay);
    }

    /// <summary>
    /// Trace 의 각 CalcStage 를 순서대로 누적해서 보여준다. 빈 단계는 건너뛰며,
    /// 단계 하나가 쌓일 때마다 stepSeconds 만큼 대기해 "수치가 하나씩 붙는" 연출을 낸다.
    /// </summary>
    private IEnumerator PlayTraceStages(GroupInfo g, string header)
    {
        StringBuilder accumulated = new StringBuilder(header.Length + 256);
        accumulated.Append(header);

        CalcStage[] order = InfoPanelFormatter.ProgressionStages;
        bool anyStageShown = false;
        for (int i = 0; i < order.Length; i++)
        {
            string section = InfoPanelFormatter.BuildStageSection(g.lastTrace, order[i]);
            if (string.IsNullOrEmpty(section)) continue;

            if (anyStageShown) accumulated.Append('\n');
            accumulated.Append(section).Append('\n');
            SetCalcText(accumulated.ToString());
            anyStageShown = true;
            yield return new WaitForSeconds(stepSeconds);
        }
    }

    private IEnumerator PlaySpecialPlacementRoutine(SpecialBlockInstance inst)
    {
        SpecialBlockDefinition def = inst.definition;
        string title = def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : def?.id;
        string header = $"<size=30><b>✨ {title} 설치!</b></size>\n";

        List<PlacedBlockVisual> footprintVisuals = CollectVisuals(inst.footprint);

        // 0) footprint 스포트라이트 + 플래시 — 일반 블럭 연출과 같은 언어로 "여기가 설치점" 을 가리킨다.
        FocusSubset(footprintVisuals);
        if (specialSpotlightSettleDelay > 0f) yield return new WaitForSeconds(specialSpotlightSettleDelay);

        FlashSubset(footprintVisuals, footprintFlashColor, specialFlashDuration);
        SetCalcText(header + "\n<size=22>설치 위치 확인...</size>");
        yield return new WaitForSeconds(specialFlashDuration);

        EffectAsset[] effects = def != null ? def.effectAssets : null;
        if (effects == null || effects.Length == 0)
        {
            SetCalcText(header + "\n<size=22>(연결된 효과 없음)</size>");
            yield return new WaitForSeconds(specialStepSeconds);
        }
        else
        {
            for (int i = 0; i < effects.Length; i++)
            {
                EffectAsset eff = effects[i];
                if (eff == null) continue;
                yield return PlaySpecialEffect(inst, eff, header, i + 1, effects.Length);
            }
        }

        // 마지막으로 "효과가 활성화되어 매 정산마다 적용됨" 을 명시.
        SetCalcText(header + $"\n<size=24><color=#FFFF66><b>▶ 효과 등록 완료</b></color></size>\n" +
                             $"<size=18>이후 정산/그룹 형성 시 자동 적용됩니다.</size>");
        yield return new WaitForSeconds(specialPostFinalDelay);

        overlay?.Hide();
    }

    private IEnumerator PlaySpecialEffect(SpecialBlockInstance inst, EffectAsset eff, string header, int index, int count)
    {
        EffectPreview preview;
        try
        {
            preview = eff.BuildPreview(inst);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            preview = new EffectPreview { title = eff.DisplayName };
            preview.steps.Add("(효과 프리뷰 계산 실패 — 콘솔 확인)");
        }

        overlay?.Show(preview.scopeCells, preview.overlayColor, gridRef);

        // 영향 받는 발전소 멤버는 Focused, 그 외는 Dimmed — 계산 대상이 시선에 잡힌다.
        List<PlacedBlockVisual> impactVisuals = CollectVisuals(preview.impactCells);
        FocusSubset(impactVisuals);
        if (specialSpotlightSettleDelay > 0f) yield return new WaitForSeconds(specialSpotlightSettleDelay);

        FlashSubset(impactVisuals, affectedFlashColor, specialFlashDuration);

        string text = header + $"\n<size=22><b>[{index}/{count}] {preview.title}</b></size>";
        SetCalcText(text);
        yield return new WaitForSeconds(specialStepSeconds);

        for (int s = 0; s < preview.steps.Count; s++)
        {
            text += $"\n{preview.steps[s]}";
            SetCalcText(text);
            yield return new WaitForSeconds(specialStepSeconds);
        }

        if (specialOverlayHoldExtra > 0f) yield return new WaitForSeconds(specialOverlayHoldExtra);
        overlay?.Hide();
    }

    // =========================================================
    // 3. 일일 정산 — "다음 일차" 버튼이 호출. 종료 후 onComplete 실행.
    // =========================================================
    public void PlayDayEndSequence(List<GroupInfo> groups, int ungroupedCount, int totalPower, Action onComplete)
    {
        // 다른 시퀀스가 이미 진행 중이면 정산 연출은 스킵하되 게임 진행은 멈추지 않도록 콜백은 수행한다.
        if (isPlaying)
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(DayEndSequenceRoutine(groups, ungroupedCount, totalPower, onComplete));
    }

    private IEnumerator DayEndSequenceRoutine(List<GroupInfo> groups, int ungroupedCount, int targetTotal, Action onComplete)
    {
        isPlaying = true;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
        if (panelRoot != null) panelRoot.SetActive(true);

        string logText = "<size=35><b>🌙 일일 전력 정산</b></size>\n\n";
        SetCalcText(logText);

        CollectActiveVisuals(groups);
        ApplySpotlightToAll(PlacedBlockVisual.SpotlightState.Dimmed);
        if (activeVisuals.Count > 0 && spotlightSettleDelay > 0f)
            yield return new WaitForSeconds(spotlightSettleDelay);

        if (groups != null)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                if (g == null) continue;

                FocusSubset(g.members);
                if (spotlightSettleDelay > 0f) yield return new WaitForSeconds(spotlightSettleDelay);
                FlashSubset(g.members, highlightColor, dayEndFlashDuration);
                yield return new WaitForSeconds(dayEndFlashDuration);

                logText += $"발전소 {g.groupID} : <color=#FFFF00>+{Mathf.Round(g.groupPower)} GWh</color>\n";
                SetCalcText(logText);
                yield return new WaitForSeconds(dayEndPerEntryDelay);
            }
        }

        if (ungroupedCount > 0)
        {
            var ungrouped = PowerManager.Instance != null ? PowerManager.Instance.LastUngroupedVisuals : null;

            FocusSubset(ungrouped);
            if (spotlightSettleDelay > 0f) yield return new WaitForSeconds(spotlightSettleDelay);
            FlashSubset(ungrouped, ungroupedHighlightColor, dayEndFlashDuration);
            yield return new WaitForSeconds(dayEndFlashDuration);

            logText += $"미연결 자투리 : <color=#AAAAAA>+{ungroupedCount} GWh</color>\n";
            SetCalcText(logText);
            yield return new WaitForSeconds(dayEndPerEntryDelay);
        }

        // 총합 표시 전에 모든 블럭을 Normal로 되돌려 "정산 완료" 분위기를 연출.
        RestoreSpotlightToNormal();

        logText += $"\n<size=40><b>오늘의 총 발전량 : <color=#00FFFF>{targetTotal} GWh</color></b></size>";
        SetCalcText(logText);
        yield return new WaitForSeconds(dayEndPostTotalDelay);

        if (panelRoot != null) panelRoot.SetActive(false);
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        isPlaying = false;

        onComplete?.Invoke();
    }

    // =========================================================
    // Spotlight / visual helpers
    // =========================================================

    /// <summary>현재 보드 상태(PowerManager 캐시)에서 활성 비주얼을 재스냅샷. 그룹 완성 팝업에서 사용.</summary>
    private void CollectActiveVisualsFromBoard()
    {
        if (PowerManager.Instance == null)
        {
            activeVisuals.Clear();
            return;
        }
        CollectActiveVisuals(PowerManager.Instance.activeGroups);
    }

    private void CollectActiveVisuals(List<GroupInfo> groups)
    {
        activeVisuals.Clear();
        if (groups != null)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                var members = groups[i]?.members;
                if (members == null) continue;
                for (int j = 0; j < members.Count; j++)
                    if (members[j] != null) activeVisuals.Add(members[j]);
            }
        }
        var ungrouped = PowerManager.Instance != null ? PowerManager.Instance.LastUngroupedVisuals : null;
        if (ungrouped != null)
        {
            for (int i = 0; i < ungrouped.Count; i++)
                if (ungrouped[i] != null) activeVisuals.Add(ungrouped[i]);
        }
    }

    private void ApplySpotlightToAll(PlacedBlockVisual.SpotlightState state)
    {
        for (int i = 0; i < activeVisuals.Count; i++)
        {
            var v = activeVisuals[i];
            if (v != null) v.SetSpotlight(state);
        }
    }

    /// <summary>focused 멤버는 Focused, 나머지 활성 비주얼은 Dimmed로 전환한다.</summary>
    private void FocusSubset(List<PlacedBlockVisual> focused)
    {
        focusSetScratch.Clear();
        if (focused != null)
        {
            for (int i = 0; i < focused.Count; i++)
                if (focused[i] != null) focusSetScratch.Add(focused[i]);
        }

        for (int i = 0; i < activeVisuals.Count; i++)
        {
            var v = activeVisuals[i];
            if (v == null) continue;
            v.SetSpotlight(focusSetScratch.Contains(v)
                ? PlacedBlockVisual.SpotlightState.Focused
                : PlacedBlockVisual.SpotlightState.Dimmed);
        }
    }

    private void FlashSubset(List<PlacedBlockVisual> members, Color color, float duration)
    {
        if (members == null) return;
        for (int i = 0; i < members.Count; i++)
        {
            var m = members[i];
            if (m != null) m.PlayFlash(color, duration);
        }
    }

    private List<PlacedBlockVisual> CollectVisuals(IReadOnlyList<Vector2Int> arrayCells)
    {
        List<PlacedBlockVisual> list = new List<PlacedBlockVisual>();
        if (arrayCells == null || gridRef == null) return list;
        for (int i = 0; i < arrayCells.Count; i++)
        {
            BlockData bd = gridRef.GetBlockAtArrayIndex(arrayCells[i]);
            if (bd == null || bd.blockObject == null) continue;
            PlacedBlockVisual v = bd.blockObject.GetComponent<PlacedBlockVisual>();
            if (v != null) list.Add(v);
        }
        return list;
    }

    /// <summary>현재 스냅샷된 모든 비주얼을 Normal로 복귀시키고 스냅샷을 비운다.</summary>
    private void RestoreSpotlightToNormal()
    {
        for (int i = 0; i < activeVisuals.Count; i++)
        {
            var v = activeVisuals[i];
            if (v != null) v.SetSpotlight(PlacedBlockVisual.SpotlightState.Normal);
        }
        activeVisuals.Clear();
    }

    private void SetCalcText(string s)
    {
        if (calcStepText != null) calcStepText.text = s;
    }

    private void OnDisable()
    {
        var registry = SpecialBlockRegistry.InstanceOrNull;
        if (subscribedToRegistry && registry != null)
        {
            registry.OnSpecialPlaced -= HandleSpecialPlaced;
            subscribedToRegistry = false;
        }

        // 씬 전환 등으로 시퀀서가 비활성화될 때만 청소. 정상 종료 경로는 각 루틴에서 이미 정리됨.
        if (!isPlaying) return;

        StopAllCoroutines();
        isPlaying = false;
        sequenceQueue.Clear();
        overlay?.Hide();
        RestoreSpotlightToNormal();

        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
