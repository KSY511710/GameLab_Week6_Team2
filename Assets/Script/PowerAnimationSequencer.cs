using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "다음 일차로" 버튼 클릭 → 그룹별 발전량 계산 과정을 순차 시각화한 뒤
/// PowerManager.CommitYesterdayProduction + ResourceManager.ProcessNextDay 호출.
/// 시퀀스 진행 중에는 PowerManager.IsAnimating = true 로 입력/Skip 차단.
/// </summary>
public class PowerAnimationSequencer : MonoBehaviour
{
    [Header("Pacing")]
    [SerializeField, Min(0.05f)] private float baseStepSeconds = 0.6f;
    [Tooltip("그룹마다 적용되는 가속 배율. 0.85면 매 그룹 시간 간격이 15%씩 짧아진다.")]
    [SerializeField, Range(0.4f, 0.99f)] private float accelerationFactor = 0.85f;
    [SerializeField, Min(0.02f)] private float minStepSeconds = 0.08f;
    [SerializeField, Min(0.05f)] private float highlightDuration = 0.4f;
    [SerializeField, Min(0.05f)] private float runningTotalLerpDuration = 0.3f;
    [SerializeField, Min(0f)]    private float postFinalDelay = 0.7f;

    [Header("UI")]
    [Tooltip("시퀀스 진행 중에만 활성화될 패널 (선택).")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("그룹의 단계별 계산식을 표시할 TMP.")]
    [SerializeField] private TextMeshProUGUI calcStepText;
    [Tooltip("누적 총합을 표시할 TMP.")]
    [SerializeField] private TextMeshProUGUI runningTotalText;
    [Tooltip("시퀀스 진행 중 비활성화될 Next Day 버튼.")]
    [SerializeField] private Button nextDayButton;

    [Header("Visual")]
    [SerializeField] private Color highlightColor          = new Color(1f, 1f, 0.4f);
    [SerializeField] private Color ungroupedHighlightColor = new Color(0.7f, 0.7f, 0.7f);

    private bool isPlaying;
    private float displayedTotal;

    /// <summary>Next Day 버튼의 OnClick에 직접 바인딩.</summary>
    public void PlayNextDaySequence()
    {
        if (isPlaying) return;
        if (PowerManager.Instance == null || ResourceManager.Instance == null) return;
        if (PowerManager.Instance.IsAnimating) return;

        StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        isPlaying = true;
        if (nextDayButton != null) nextDayButton.interactable = false;
        PowerManager.Instance.SetAnimating(true);

        if (panelRoot != null) panelRoot.SetActive(true);
        SetRunningTotal(0f, immediate: true);
        SetCalcText("");

        // 시퀀스가 표현해야 할 정확한 목표값을 시작 시점에 한 번만 캡처.
        // 시퀀스 도중에 보드가 바뀌지 않는다는 전제 (입력 차단 + IsAnimating).
        int targetTotal = PowerManager.Instance.GetTotalPower();
        List<GroupInfo> groups = PowerManager.Instance.activeGroups;
        int groupCount = groups != null ? groups.Count : 0;

        // === 그룹별 계산 ===
        for (int i = 0; i < groupCount; i++)
        {
            GroupInfo g = groups[i];
            float step = Mathf.Max(minStepSeconds, baseStepSeconds * Mathf.Pow(accelerationFactor, i));

            yield return HighlightMembers(g.members, highlightColor, highlightDuration);

            // (a) 기본 발전량 = 칸 수 + 부품 종류
            SetCalcText(
                $"<b>그룹 {g.groupID}</b>\n" +
                $"기본 {g.baseProduction} + 부품 {g.uniqueParts} = " +
                $"<color=#FFE066>{g.baseProduction + g.uniqueParts}</color>");
            yield return new WaitForSeconds(step);

            // (b) 형태 계수 (기본 2 + 모양 보너스)
            int shapeBonus = g.completionMultiplier - 2;
            SetCalcText(
                $"<b>그룹 {g.groupID}</b>\n" +
                $"형태 계수: 2 + {shapeBonus} = " +
                $"<color=#66D9FF>x {g.completionMultiplier}</color>");
            yield return new WaitForSeconds(step);

            // (c) 색 배율 = 1 + (max - rest) * 0.2
            SetCalcText(
                $"<b>그룹 {g.groupID}</b>\n" +
                $"색 배율: <color=#FF99CC>x {g.colorMultiplier:F2}</color>");
            yield return new WaitForSeconds(step);

            // (d) 최종 + 누적
            SetCalcText(
                $"<b>그룹 {g.groupID} 최종</b>\n" +
                $"({g.baseProduction} + {g.uniqueParts}) × {g.completionMultiplier} × {g.colorMultiplier:F2} = " +
                $"<color=#FFFF00><b>{Mathf.Round(g.groupPower)} GWh</b></color>");
            yield return AddToRunningTotal(g.groupPower);
            yield return new WaitForSeconds(step);
        }

        // === 미그룹 블럭 (1칸당 1 GWh) ===
        int ungroupedCount = PowerManager.Instance.LastUngroupedCount;
        if (ungroupedCount > 0)
        {
            float ungroupedStep = Mathf.Max(minStepSeconds, baseStepSeconds * Mathf.Pow(accelerationFactor, groupCount));

            yield return HighlightMembers(PowerManager.Instance.LastUngroupedVisuals, ungroupedHighlightColor, highlightDuration);

            SetCalcText($"<b>미연결 블럭</b>\n+{ungroupedCount} GWh");
            yield return AddToRunningTotal(ungroupedCount);
            yield return new WaitForSeconds(ungroupedStep);
        }

        // 시작 시점의 정확한 목표값으로 보정 (lerp 누적의 부동소수 오차 흡수)
        SetRunningTotal(targetTotal, immediate: true);
        SetCalcText($"<color=#FFFF66><b>오늘 총 발전량: {targetTotal} GWh</b></color>");
        yield return new WaitForSeconds(postFinalDelay);

        // === 실제 정산 ===
        PowerManager.Instance.CommitYesterdayProduction(targetTotal);
        ResourceManager.Instance.ProcessNextDay();

        if (panelRoot != null) panelRoot.SetActive(false);
        PowerManager.Instance.SetAnimating(false);
        if (nextDayButton != null) nextDayButton.interactable = true;
        isPlaying = false;
    }

    private IEnumerator HighlightMembers(List<PlacedBlockVisual> members, Color color, float duration)
    {
        if (members == null || members.Count == 0) yield break;
        foreach (var m in members)
        {
            if (m == null) continue;
            m.StartCoroutine(m.FlashHighlight(color, duration));
        }
        yield return new WaitForSeconds(duration);
    }

    private IEnumerator AddToRunningTotal(float amount)
    {
        float start = displayedTotal;
        float target = start + amount;
        float t = 0f;
        while (t < runningTotalLerpDuration)
        {
            t += Time.deltaTime;
            displayedTotal = Mathf.Lerp(start, target, t / runningTotalLerpDuration);
            SetRunningTotal(displayedTotal, immediate: false);
            yield return null;
        }
        SetRunningTotal(target, immediate: true);
    }

    private void SetRunningTotal(float v, bool immediate)
    {
        if (immediate) displayedTotal = v;
        if (runningTotalText != null)
        {
            runningTotalText.text = $"= {Mathf.Round(v)} GWh";
        }
    }

    private void SetCalcText(string s)
    {
        if (calcStepText != null) calcStepText.text = s;
    }

    private void OnDisable()
    {
        // 씬 전환 등으로 시퀀서가 비활성화될 때 잔존 상태 청소
        if (!isPlaying) return;

        StopAllCoroutines();
        isPlaying = false;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        if (nextDayButton != null) nextDayButton.interactable = true;
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}
