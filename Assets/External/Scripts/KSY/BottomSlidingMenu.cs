using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BottomSlidingMenu : MonoBehaviour
{
    [Header("UI 연결")]
    public RectTransform panelRect;
    public RectTransform buttonRect;
    public CanvasGroup panelCanvasGroup;

    [Header("설정값")]
    public float expandedPanelWidth = 400f;
    public float collapsedPanelWidth = 0f;
    [Tooltip("버튼과 패널 사이의 미세한 간격")]
    public float padding = 10f;
    public float moveSpeed = 0.3f;

    private bool isExpanded = true;
    private Coroutine slideCoroutine;

    private void Start()
    {
        // 시작 시 현재 상태에 맞춰 즉시 초기화 (애니메이션 없이)
        float targetWidth = isExpanded ? expandedPanelWidth : collapsedPanelWidth;
        panelRect.sizeDelta = new Vector2(targetWidth, panelRect.sizeDelta.y);

        // 버튼 위치 강제 고정
        float buttonX = CalculateButtonX(targetWidth);
        buttonRect.anchoredPosition = new Vector2(buttonX, buttonRect.anchoredPosition.y);

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.blocksRaycasts = isExpanded;
        }
    }

    public void ToggleMenu()
    {
        if (slideCoroutine != null) StopCoroutine(slideCoroutine);

        isExpanded = !isExpanded;

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.blocksRaycasts = isExpanded;
        }

        float targetWidth = isExpanded ? expandedPanelWidth : collapsedPanelWidth;
        slideCoroutine = StartCoroutine(SlideAnimation(targetWidth));
    }

    private IEnumerator SlideAnimation(float targetWidth)
    {
        float startWidth = panelRect.sizeDelta.x;
        float elapsed = 0f;

        while (elapsed < moveSpeed)
        {
            elapsed += Time.deltaTime;
            // 부드러운 가속도 (Sin)
            float t = Mathf.Sin(elapsed / moveSpeed * Mathf.PI * 0.5f);

            // 1. 패널 너비 보정
            float currentWidth = Mathf.Lerp(startWidth, targetWidth, t);
            panelRect.sizeDelta = new Vector2(currentWidth, panelRect.sizeDelta.y);

            // 2. 🌟 버튼 위치 실시간 동기화 (겹침 방지 계산 포함)
            float currentButtonX = CalculateButtonX(currentWidth);
            buttonRect.anchoredPosition = new Vector2(currentButtonX, buttonRect.anchoredPosition.y);

            yield return null;
        }

        // 최종 값 고정
        panelRect.sizeDelta = new Vector2(targetWidth, panelRect.sizeDelta.y);
        buttonRect.anchoredPosition = new Vector2(CalculateButtonX(targetWidth), buttonRect.anchoredPosition.y);
    }

    // 🌟 버튼의 X 위치를 계산하는 핵심 공식
    private float CalculateButtonX(float currentPanelWidth)
    {
        // 버튼의 Pivot이 0.5라고 가정할 때:
        // [패널너비의 절반] + [버튼너비의 절반] + [여백] 만큼 왼쪽(-)으로 밀어냅니다.
        float buttonHalfWidth = buttonRect.rect.width * 0.5f;
        return (-currentPanelWidth * 0.5f) - buttonHalfWidth - padding;
    }
}