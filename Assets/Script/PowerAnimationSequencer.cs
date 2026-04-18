using System; // 🌟 Action(콜백)을 사용하기 위해 추가
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PowerAnimationSequencer : MonoBehaviour
{
    public static PowerAnimationSequencer Instance;

    [Header("Pacing")]
    [SerializeField, Min(0.05f)] private float stepSeconds = 0.8f;
    [SerializeField, Min(0.05f)] private float highlightDuration = 0.4f;
    [SerializeField, Min(0f)] private float postFinalDelay = 1.0f;

    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI calcStepText;

    [Header("Visual")]
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.4f);

    private Queue<GroupInfo> animationQueue = new Queue<GroupInfo>();
    private bool isPlaying = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // =========================================================
    // 1. 실시간 발전소 완성 팝업 (기존 기능)
    // =========================================================
    public void EnqueueAnimation(GroupInfo newGroup)
    {
        animationQueue.Enqueue(newGroup);
        if (!isPlaying) StartCoroutine(SequenceRoutine());
    }

    private IEnumerator SequenceRoutine()
    {
        isPlaying = true;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
        if (panelRoot != null) panelRoot.SetActive(true);

        while (animationQueue.Count > 0)
        {
            GroupInfo g = animationQueue.Dequeue();
            SetCalcText("");

            yield return HighlightMembers(g.members, highlightColor, highlightDuration);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"기본 {g.baseProduction} + 부품 {g.uniqueParts}종\n" +
                        $"= <color=#FFE066>{g.baseProduction + g.uniqueParts}</color>");
            yield return new WaitForSeconds(stepSeconds);

            int shapeBonus = g.completionMultiplier - 2;
            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"형태 시너지 보너스\n= <color=#66D9FF>x {g.completionMultiplier}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 완성!</b></size>\n\n" +
                        $"색상 순도 배율\n= <color=#FF99CC>x {g.colorMultiplier:F2}</color>");
            yield return new WaitForSeconds(stepSeconds);

            SetCalcText($"<size=30><b>⚡ 발전소 {g.groupID} 가동 시작!</b></size>\n\n" +
                        $"최종 발전량\n<color=#FFFF00><size=40><b>+ {Mathf.Round(g.groupPower)} GWh</b></size></color>");
            yield return new WaitForSeconds(postFinalDelay);
        }

        if (panelRoot != null) panelRoot.SetActive(false);
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        isPlaying = false;
    }

    // =========================================================
    // 🌙 2. 새로 추가된 일일 정산 연출! (Day 넘어갈 때 재생)
    // =========================================================
    public void PlayDayEndSequence(List<GroupInfo> activeGroups, int ungroupedCount, int totalPower, Action onComplete)
    {
        if (isPlaying) return;
        StartCoroutine(DayEndSequenceRoutine(activeGroups, ungroupedCount, totalPower, onComplete));
    }

    private IEnumerator DayEndSequenceRoutine(List<GroupInfo> groups, int ungroupedCount, int targetTotal, Action onComplete)
    {
        isPlaying = true;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(true);
        if (panelRoot != null) panelRoot.SetActive(true);

        string logText = "<size=35><b>🌙 일일 전력 정산</b></size>\n\n";
        SetCalcText(logText);
        yield return new WaitForSeconds(0.5f);

        // 1. 각 발전소의 최종 생산량만 빠르게 쫘르륵 더해줍니다. (스피디하게 0.3초 간격)
        foreach (var g in groups)
        {
            yield return HighlightMembers(g.members, highlightColor, 0.2f); // 발전소 반짝!

            logText += $"발전소 {g.groupID} : <color=#FFFF00>+{Mathf.Round(g.groupPower)} GWh</color>\n";
            SetCalcText(logText);
            yield return new WaitForSeconds(0.3f);
        }

        // 2. 미연결 블럭 자투리 전력 추가
        if (ungroupedCount > 0)
        {
            logText += $"미연결 자투리 : <color=#AAAAAA>+{ungroupedCount} GWh</color>\n";
            SetCalcText(logText);
            yield return new WaitForSeconds(0.3f);
        }

        // 3. 오늘 총 발전량 쾅! 찍어주기
        logText += $"\n<size=40><b>오늘의 총 발전량 : <color=#00FFFF>{targetTotal} GWh</color></b></size>";
        SetCalcText(logText);

        // 유저가 결과를 감상할 시간 제공
        yield return new WaitForSeconds(1.5f);

        // 4. 연출 종료 및 정리
        if (panelRoot != null) panelRoot.SetActive(false);
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        isPlaying = false;

        // 🌟 연출이 모두 끝난 뒤에 비로소 진짜로 다음 날로 넘기는 명령을 실행합니다!
        onComplete?.Invoke();
    }

    private IEnumerator HighlightMembers(List<PlacedBlockVisual> members, Color color, float duration)
    {
        if (members == null || members.Count == 0) yield break;
        foreach (var m in members) if (m != null) m.StartCoroutine(m.FlashHighlight(color, duration));
        yield return new WaitForSeconds(duration);
    }

    private void SetCalcText(string s) { if (calcStepText != null) calcStepText.text = s; }

    private void OnDisable()
    {
        StopAllCoroutines();
        isPlaying = false;
        if (PowerManager.Instance != null) PowerManager.Instance.SetAnimating(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }
}