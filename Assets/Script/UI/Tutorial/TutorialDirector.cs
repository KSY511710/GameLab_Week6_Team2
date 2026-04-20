using System.Collections;
using System.Collections.Generic;
using Prediction;
using UnityEngine;

namespace UI.Tutorial
{
    /// <summary>
    /// 8단계 온보딩 튜토리얼의 상태 머신 + 이벤트 라우터.
    /// 기존 게임플레이 코드를 수정하지 않고 다음 이벤트만 구독해 스텝을 전환한다.
    ///   - <see cref="PlacementInteractionHub.OnDragMoved"/> / <see cref="PlacementInteractionHub.OnDragEnded"/>
    ///   - <see cref="PowerManager.OnTotalPowerChanged"/> (+ <see cref="PowerManager.IsAnimating"/> 폴링)
    ///   - <see cref="ResourceManager.OnSkipAvailability"/>
    ///
    /// 최초 1회만 재생 (PlayerPrefs 플래그). Shift + T 로 플래그 삭제 후 재진입 시 재생.
    /// 시나리오 데이터는 <see cref="steps"/> 배열에 인스펙터에서 입력.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] private TutorialStep[] steps;

        [Header("View References")]
        [SerializeField] private TutorialDialogView dialogView;
        [SerializeField] private TutorialHighlighter highlighter;

        [Header("Persistence")]
        [Tooltip("완료 플래그 저장 키. 시나리오가 크게 바뀌면 버전 문자를 올려 재생 유도.")]
        [SerializeField] private string completionPrefKey = "Tutorial_Completed_v1";
        [Tooltip("체크 시 PlayerPrefs 완료 플래그를 무시하고 항상 재생. 디버깅/QA 용.")]
        [SerializeField] private bool forcePlayInEditor;
        [Tooltip("Shift + 이 키를 누르면 완료 플래그를 삭제해 다음 씬 로드 시 튜토리얼을 다시 재생.")]
        [SerializeField] private KeyCode devResetKey = KeyCode.T;

        [Header("Tuning")]
        [Tooltip("스텝 전환 시 다이얼로그가 사라지고 다음 스텝이 뜰 때까지의 여백.")]
        [SerializeField, Min(0f)] private float stepTransitionDelay = 0.2f;

        private int currentIndex = -1;
        private bool deferredWaitingForSkip;
        private bool firstPlantSeen;
        private bool awaitingAnimationEnd;

        // ========================= Unity lifecycle =========================

        private void Start()
        {
            if (steps == null || steps.Length == 0)
            {
                Debug.LogWarning("[TutorialDirector] steps 배열이 비어있어 튜토리얼을 재생할 수 없습니다.");
                gameObject.SetActive(false);
                return;
            }

            if (!forcePlayInEditor && PlayerPrefs.GetInt(completionPrefKey, 0) == 1)
            {
                gameObject.SetActive(false);
                return;
            }

            dialogView?.Hide();
            highlighter?.Clear();
            StartCoroutine(BeginAfterInit());
        }

        private IEnumerator BeginAfterInit()
        {
            // 씬의 싱글턴(ResourceManager, PowerManager, GridManager) 초기화를 한 프레임 기다린다.
            yield return null;
            ShowStep(0);
        }

        private void OnEnable()
        {
            PlacementInteractionHub.OnDragMoved += HandleDragMoved;
            PlacementInteractionHub.OnDragEnded += HandleDragEnded;
            PowerManager.OnTotalPowerChanged += HandleTotalPowerChanged;
            ResourceManager.OnSkipAvailability += HandleSkipAvailability;
        }

        private void OnDisable()
        {
            PlacementInteractionHub.OnDragMoved -= HandleDragMoved;
            PlacementInteractionHub.OnDragEnded -= HandleDragEnded;
            PowerManager.OnTotalPowerChanged -= HandleTotalPowerChanged;
            ResourceManager.OnSkipAvailability -= HandleSkipAvailability;
        }

        private void Update()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && Input.GetKeyDown(devResetKey))
            {
                PlayerPrefs.DeleteKey(completionPrefKey);
                PlayerPrefs.Save();
                Debug.Log($"[TutorialDirector] '{completionPrefKey}' 플래그 삭제. 씬 재로드 시 튜토리얼이 다시 재생됩니다.");
            }
        }

        // ========================= Step orchestration =========================

        private TutorialStep Current => (currentIndex >= 0 && currentIndex < steps.Length) ? steps[currentIndex] : null;

        private void ShowStep(int index)
        {
            if (index < 0 || index >= steps.Length)
            {
                CompleteTutorial();
                return;
            }

            currentIndex = index;
            TutorialStep step = steps[index];

            // 표시 조건 게이팅: 스킵 가용 대기 스텝은 조건 충족 전까지 UI 를 완전히 숨긴다.
            if (step.showCondition == StepShowCondition.WhenSkipAvailable && !IsSkipAvailableNow())
            {
                deferredWaitingForSkip = true;
                dialogView?.Hide();
                highlighter?.Clear();
                return;
            }

            deferredWaitingForSkip = false;
            ApplyHighlight(step);

            bool useContinueButton = step.advanceTrigger == TutorialAdvanceTrigger.ContinueButton;
            RectTransform anchorTarget = step.targetKind == TutorialTargetKind.UIRect ? step.uiTarget : null;
            dialogView?.Show(step.message, step.anchor, anchorTarget, useContinueButton, HandleContinueClicked);

            // 이벤트 기반 트리거가 이미 충족된 상태로 진입했다면 즉시 한 번 평가.
            EvaluateEventTriggersOnEntry();
        }

        private void ApplyHighlight(TutorialStep step)
        {
            if (highlighter == null) return;

            switch (step.targetKind)
            {
                case TutorialTargetKind.None:
                    highlighter.Clear();
                    break;
                case TutorialTargetKind.UIRect:
                    if (step.uiTarget != null) highlighter.Highlight(step.uiTarget);
                    else highlighter.Clear();
                    break;
                case TutorialTargetKind.FirstBuiltPlant:
                    if (TryGetFirstPlantWorld(out Vector3 pos, out Vector2 size))
                        highlighter.SetWorldTarget(pos, size);
                    else
                        highlighter.Clear();
                    break;
            }
        }

        private void EvaluateEventTriggersOnEntry()
        {
            TutorialStep step = Current;
            if (step == null) return;

            switch (step.advanceTrigger)
            {
                case TutorialAdvanceTrigger.OnFirstPowerPlantBuilt:
                    HandleTotalPowerChanged();
                    break;
                case TutorialAdvanceTrigger.OnSkipAvailable:
                    if (IsSkipAvailableNow()) AdvanceNext();
                    break;
            }
        }

        private void HandleContinueClicked()
        {
            TutorialStep step = Current;
            if (step == null) return;
            if (step.advanceTrigger != TutorialAdvanceTrigger.ContinueButton) return;
            AdvanceNext();
        }

        private void AdvanceNext()
        {
            int next = currentIndex + 1;
            if (next >= steps.Length)
            {
                CompleteTutorial();
                return;
            }
            StartCoroutine(AdvanceAfterDelay(next));
        }

        private IEnumerator AdvanceAfterDelay(int nextIndex)
        {
            dialogView?.Hide();
            highlighter?.Clear();
            if (stepTransitionDelay > 0f) yield return new WaitForSecondsRealtime(stepTransitionDelay);
            // 스텝 간 상태 리셋
            firstPlantSeen = false;
            awaitingAnimationEnd = false;
            ShowStep(nextIndex);
        }

        private void CompleteTutorial()
        {
            dialogView?.Hide();
            highlighter?.Clear();
            PlayerPrefs.SetInt(completionPrefKey, 1);
            PlayerPrefs.Save();
            Debug.Log("[TutorialDirector] 튜토리얼 완료. 다음 진입부터 자동 스킵됩니다.");
            gameObject.SetActive(false);
        }

        // ========================= Event handlers =========================

        private void HandleDragMoved(DragMovedArgs args)
        {
            TutorialStep step = Current;
            if (step == null) return;
            if (step.advanceTrigger == TutorialAdvanceTrigger.OnFirstDragStart)
            {
                AdvanceNext();
            }
        }

        private void HandleDragEnded()
        {
            TutorialStep step = Current;
            if (step == null) return;
            if (step.advanceTrigger == TutorialAdvanceTrigger.ContinueButton && step.autoAdvanceOnDragEnded)
            {
                AdvanceNext();
            }
        }

        private void HandleTotalPowerChanged()
        {
            TutorialStep step = Current;
            if (step == null) return;
            if (step.advanceTrigger != TutorialAdvanceTrigger.OnFirstPowerPlantBuilt) return;

            var pm = PowerManager.Instance;
            if (pm == null) return;

            if (!firstPlantSeen && pm.activeGroups != null && pm.activeGroups.Count >= 1)
            {
                firstPlantSeen = true;
                awaitingAnimationEnd = true;
            }

            if (awaitingAnimationEnd && !pm.IsAnimating)
            {
                awaitingAnimationEnd = false;
                AdvanceNext();
            }
        }

        private void HandleSkipAvailability(bool canSkip)
        {
            TutorialStep step = Current;
            if (step == null) return;

            // 1) 지연 표시 스텝이 대기 중이면 조건 충족 시 활성화.
            if (deferredWaitingForSkip && canSkip)
            {
                ShowStep(currentIndex);
                return;
            }

            // 2) advanceTrigger 가 OnSkipAvailable 인 스텝은 조건 충족 시 즉시 진행.
            if (step.advanceTrigger == TutorialAdvanceTrigger.OnSkipAvailable && canSkip)
            {
                AdvanceNext();
            }
        }

        // ========================= Helpers =========================

        private static bool IsSkipAvailableNow()
        {
            return ResourceManager.Instance != null && ResourceManager.Instance.CanSkip();
        }

        /// <summary>
        /// 가장 먼저 형성된 그룹(첫 발전소) 의 월드 중심 + bounding box 크기를 구한다.
        /// PlacedBlockVisual 멤버들의 실제 world position 을 사용하여 TileMap 좌표 변환 API 에 의존하지 않는다.
        /// </summary>
        private bool TryGetFirstPlantWorld(out Vector3 pos, out Vector2 size)
        {
            pos = default;
            size = Vector2.one;
            var pm = PowerManager.Instance;
            if (pm == null || pm.activeGroups == null || pm.activeGroups.Count == 0) return false;

            GroupInfo group = pm.activeGroups[0];
            List<PlacedBlockVisual> members = group.members;
            if (members == null || members.Count == 0) return false;

            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
            int valid = 0;

            foreach (var m in members)
            {
                if (m == null) continue;
                Vector3 wp = m.transform.position;
                if (wp.x < min.x) min.x = wp.x;
                if (wp.y < min.y) min.y = wp.y;
                if (wp.x > max.x) max.x = wp.x;
                if (wp.y > max.y) max.y = wp.y;
                valid++;
            }
            if (valid == 0) return false;

            pos = (min + max) * 0.5f;
            // 셀 1개 크기를 최소 단위로 보장해 단일-셀 그룹에서도 강조가 보이게 한다.
            size = new Vector2(Mathf.Max(1f, max.x - min.x + 1f), Mathf.Max(1f, max.y - min.y + 1f));
            return true;
        }

        // ========================= Editor convenience =========================

        /// <summary>
        /// 인스펙터 컨텍스트 메뉴에서 8단계 기본 시나리오를 채워 넣는다.
        /// 채운 뒤 uiTarget 레퍼런스(인벤 / D-Day / 정보 패널 / 스킵 버튼) 4개만 수동으로 드래그-인 하면 된다.
        /// </summary>
        [ContextMenu("Reset Scenario (Default 8 Steps)")]
        private void ResetScenarioToDefault()
        {
            steps = new TutorialStep[]
            {
                new TutorialStep {
                    id = "welcome",
                    message = "반가워요! 이 도시의 전력을 책임지게 된 당신을 환영해요.\n먼저 기본 조작을 알려드릴게요.",
                    targetKind = TutorialTargetKind.None,
                    anchor = DialogAnchor.Center,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton
                },
                new TutorialStep {
                    id = "goal",
                    message = "화면 상단의 <b>D-Day</b>와 <b>일일 목표 생산량</b>을 확인하세요.\nD-Day가 끝나는 날까지 목표를 달성하지 못하면 게임 오버예요.",
                    targetKind = TutorialTargetKind.UIRect,
                    anchor = DialogAnchor.BelowTarget,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton
                },
                new TutorialStep {
                    id = "inventory",
                    message = "왼쪽 인벤토리에서 블럭을 <b>드래그해</b> 필드에 배치해 보세요.",
                    targetKind = TutorialTargetKind.UIRect,
                    anchor = DialogAnchor.RightOfTarget,
                    advanceTrigger = TutorialAdvanceTrigger.OnFirstDragStart
                },
                new TutorialStep {
                    id = "info_panel",
                    message = "드래그하는 동안 오른쪽 패널에 <b>예상 발전소 스펙</b>이 나타나요.\n인접한 블럭이 9칸 이상, 부품 종류가 3종 이상이면 발전소가 완성돼요.",
                    targetKind = TutorialTargetKind.UIRect,
                    anchor = DialogAnchor.LeftOfTarget,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton,
                    autoAdvanceOnDragEnded = true
                },
                new TutorialStep {
                    id = "build_first",
                    message = "좋아요! 이제 블럭을 더 이어 붙여서\n<b>첫 발전소</b>를 완성해 보세요.",
                    targetKind = TutorialTargetKind.None,
                    anchor = DialogAnchor.Center,
                    advanceTrigger = TutorialAdvanceTrigger.OnFirstPowerPlantBuilt
                },
                new TutorialStep {
                    id = "hover_info",
                    message = "축하해요! 완성된 발전소 위에 <b>마우스를 올려보면</b>\n오른쪽 패널에서 상세 스펙을 확인할 수 있어요.",
                    targetKind = TutorialTargetKind.FirstBuiltPlant,
                    anchor = DialogAnchor.Center,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton
                },
                new TutorialStep {
                    id = "skip_ticket",
                    message = "목표 일차보다 일찍 일일 생산량을 달성하면\n<b>스킵</b>으로 남은 일수만큼 <b>티켓</b>을 받을 수 있어요.\n티켓이 쌓이면... 좋은 일이 생길지도 몰라요.",
                    targetKind = TutorialTargetKind.UIRect,
                    anchor = DialogAnchor.AboveTarget,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton,
                    showCondition = StepShowCondition.WhenSkipAvailable
                },
                new TutorialStep {
                    id = "farewell",
                    message = "그럼 행운을 빌어요!",
                    targetKind = TutorialTargetKind.None,
                    anchor = DialogAnchor.Center,
                    advanceTrigger = TutorialAdvanceTrigger.ContinueButton
                }
            };
            Debug.Log("[TutorialDirector] 기본 8단계 시나리오로 초기화했습니다. uiTarget 4개(D-Day, 인벤토리, 정보 패널, 스킵 버튼)를 수동으로 연결하세요.");
        }
    }
}
