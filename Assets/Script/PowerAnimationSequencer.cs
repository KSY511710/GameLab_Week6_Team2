using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 두 종류의 시퀀스를 주관한다.
///  1. <see cref="EnqueueAnimation"/>  — 블럭 배치로 새 발전소가 완성되는 순간 해당 그룹의 계산 과정을 팝업으로 보여준다.
///  2. <see cref="PlayDayEndSequence"/> — "다음 일차" 버튼 클릭 시 모든 발전소의 일일 정산을 종합 연출한다.
///
/// 두 시퀀스 모두 공용 스포트라이트 API(<see cref="PlacedBlockVisual.SetSpotlight"/>)를 통해
/// "지금 주목해야 할 발전소"를 크기+밝기 편차로 가리킨다. 나머지 보드는 <c>Dimmed</c>로 깔린다.
/// 진행 중엔 <c>PowerManager.IsAnimating = true</c>로 입력/Skip을 차단하고, 종료 시 해제한다.
/// </summary>
public class PowerAnimationSequencer : MonoBehaviour
{
    public static PowerAnimationSequencer Instance;

    [Header("Pacing")]
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

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI calcStepText;

    [Header("Visual")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.4f);
    [SerializeField] private Color ungroupedHighlightColor = new Color(0.7f, 0.7f, 0.7f);

    private readonly Queue<GroupInfo> animationQueue = new Queue<GroupInfo>();
    private bool isPlaying;

    // 시퀀스 동안 스포트라이트를 토글할 대상들. 시퀀스 시작 시 스냅샷을 뜨고,
    // 종료/인터럽트 시점에 이 리스트로 전원 Normal 복귀를 보장한다.
    private readonly List<PlacedBlockVisual> activeVisuals = new List<PlacedBlockVisual>();
    private readonly HashSet<PlacedBlockVisual> focusSetScratch = new HashSet<PlacedBlockVisual>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // =========================================================
    // 1. 발전소 완성 팝업 — 블럭 배치로 새 그룹이 생기는 즉시 큐잉
    // =========================================================
    public void EnqueueAnimation(GroupInfo newGroup)
    {
        if (newGroup == null) return;
        animationQueue.Enqueue(newGroup);
        if (!isPlaying) StartCoroutine(GroupCompletedRoutine());
    }

    private IEnumerator GroupCompletedRoutine()
    {
        isPlaying = true;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
        if (panelRoot != null) panelRoot.SetActive(true);

        // EnqueueAnimation은 CheckAndFormGroups 호출 스택에서 불린다. 바로 이어서
        // GridManager가 CalculateTotalPower(LastUngroupedVisuals 갱신)를 실행하므로,
        // 보드 스냅샷을 뜨기 전 한 프레임 양보해 최신 상태에서 스포트라이트를 적용한다.
        yield return null;

        while (animationQueue.Count > 0)
        {
            GroupInfo g = animationQueue.Dequeue();
            if (g == null) continue;

            SetCalcText("");
            CollectActiveVisualsFromBoard();

            FocusSubset(g.members);
            if (spotlightSettleDelay > 0f) yield return new WaitForSeconds(spotlightSettleDelay);

            FlashSubset(g.members, highlightColor, highlightDuration);
            yield return new WaitForSeconds(highlightDuration);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"기본 {g.baseProduction} + 부품 {g.uniqueParts}종\n" +
                        $"= <color=#FFE066>{g.baseProduction + g.uniqueParts}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"형태 시너지 보너스\n= <color=#66D9FF>x {g.completionMultiplier}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"색상 순도 배율\n= <color=#FF99CC>x {g.colorMultiplier:F2}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 가동 시작!</b></size>\n\n" +
                        $"최종 발전량\n<color=#FFFF00><size=40><b>+ {Mathf.Round(g.groupPower)} GWh</b></size></color>");
            yield return new WaitForSeconds(postFinalDelay);

            // 다음 큐 아이템(또는 종료) 전에 스포트라이트를 원상태로 — 큐에 남아있다면 다음 루프에서 재설정한다.
            RestoreSpotlightToNormal();
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        isPlaying = false;
    }

    // =========================================================
    // 2. 일일 정산 — "다음 일차" 버튼이 호출. 종료 후 onComplete 실행.
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
        // 씬 전환 등으로 시퀀서가 비활성화될 때만 청소. 정상 종료 경로는 각 루틴에서 이미 정리됨.
        if (!isPlaying) return;

        StopAllCoroutines();
        isPlaying = false;
        animationQueue.Clear();
        RestoreSpotlightToNormal();

        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
