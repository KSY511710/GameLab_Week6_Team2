using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class PlacedBlockVisual : MonoBehaviour
{
    /// <summary>시퀀서가 블럭을 강조/감쇠할 때 사용하는 상태. 크기와 색 밝기를 함께 변조한다.</summary>
    public enum SpotlightState { Normal, Focused, Dimmed }

    private SpriteRenderer sr;
    private Sprite originalSprite;
    private Color originalColor;

    private bool isGrouped = false;
    private Color groupColor;
    public Sprite blankSprite;

    private List<PlacedBlockVisual> myGroupMembers;

    // 🗑️ [삭제] 외곽선을 코드로 만들 때 쓰던 변수들 (color, thickness, material 등) 전부 삭제!

    [Header("Spotlight FX")]
    [Tooltip("Focused 상태에서 블럭이 커지는 비율. 크기 대비로 주목도를 더한다.")]
    [SerializeField, Range(1f, 2f)] private float focusedScaleMultiplier = 1.25f;
    [Tooltip("Dimmed 상태에서 색상 RGB에 곱해지는 배율. 값이 낮을수록 주변이 더 어두워진다.")]
    [SerializeField, Range(0f, 1f)] private float dimmedBrightness = 0.22f;
    [Tooltip("스포트라이트 상태 전환에 걸리는 시간.")]
    [SerializeField, Min(0.01f)] private float spotlightTransitionDuration = 0.18f;
    [Tooltip("플래시 펄스 피크에서 적용되는 추가 크기 배율. Focused 크기 위에 곱해진다.")]
    [SerializeField, Range(1f, 2f)] private float flashScaleMultiplier = 1.35f;

    // 🌟 프리팹에 있는 기존 선들을 담을 변수
    private GameObject lineTop, lineBottom, lineLeft, lineRight;

    private Vector3 baseLocalScale;
    private SpotlightState currentSpotlight = SpotlightState.Normal;
    private Coroutine fxCoroutine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalSprite = sr.sprite;
        originalColor = sr.color;
        baseLocalScale = transform.localScale;

        // 🌟 [핵심 변경] 텍스처를 새로 만들지 않고, 프리팹 자식으로 있는 Line_U, D, L, R을 찾아 연결합니다!
        Transform tLineU = transform.Find("Line_U");
        Transform tLineD = transform.Find("Line_D");
        Transform tLineL = transform.Find("Line_L");
        Transform tLineR = transform.Find("Line_R");

        lineTop = tLineU != null ? tLineU.gameObject : null;
        lineBottom = tLineD != null ? tLineD.gameObject : null;
        lineLeft = tLineL != null ? tLineL.gameObject : null;
        lineRight = tLineR != null ? tLineR.gameObject : null;

        // 시작할 때는 선을 모두 꺼둡니다.
        UpdateOutline(false, false, false, false);
    }

    // ==========================================
    // 🎛️ 외곽선 끄고 켜기 (PowerManager가 호출함)
    // ==========================================
    public void UpdateOutline(bool top, bool bottom, bool left, bool right)
    {
        // 🌟 그룹 상태가 아니면 무조건 다 끕니다. (PowerManager의 지시를 따름)
        if (!isGrouped)
        {
            if (lineTop != null) lineTop.SetActive(false);
            if (lineBottom != null) lineBottom.SetActive(false);
            if (lineLeft != null) lineLeft.SetActive(false);
            if (lineRight != null) lineRight.SetActive(false);
            return;
        }

        // 그룹 상태라면 PowerManager가 계산해준 대로 켭니다.
        if (lineTop != null) lineTop.SetActive(top);
        if (lineBottom != null) lineBottom.SetActive(bottom);
        if (lineLeft != null) lineLeft.SetActive(left);
        if (lineRight != null) lineRight.SetActive(right);
    }

    public void SetGroupState(bool grouped, Color dominantColor, List<PlacedBlockVisual> groupList = null)
    {
        isGrouped = grouped;
        groupColor = dominantColor;
        myGroupMembers = groupList;

        if (isGrouped)
        {
            if (blankSprite != null) sr.sprite = blankSprite;
            sr.color = groupColor;
        }
        else
        {
            sr.sprite = originalSprite;
            sr.color = originalColor;
            myGroupMembers = null;

            // 그룹 해제 시 아웃라인도 즉시 끄기
            UpdateOutline(false, false, false, false);
        }

        StopFx();
        currentSpotlight = SpotlightState.Normal;
        transform.localScale = baseLocalScale;
    }

    // (아래 기존 함수들은 변경 없이 그대로 유지)
    public void RevealOriginal()
    {
        if (isGrouped)
        {
            sr.sprite = originalSprite;
            sr.color = originalColor;
        }
    }

    public void HideToGroupColor()
    {
        if (isGrouped)
        {
            if (blankSprite != null) sr.sprite = blankSprite;
            sr.color = groupColor;
        }
    }

    private void OnMouseEnter()
    {
        if (isGrouped && myGroupMembers != null)
        {
            foreach (var member in myGroupMembers) if (member != null) member.RevealOriginal();
        }
    }

    private void OnMouseExit()
    {
        if (isGrouped && myGroupMembers != null)
        {
            foreach (var member in myGroupMembers) if (member != null) member.HideToGroupColor();
        }
    }

    // ==========================================
    // Spotlight & Flash API
    // ==========================================

    /// <summary>지속되는 강조/감쇠 상태. 스포트라이트처럼 "지금 계산 중인 그룹"을 표현할 때 사용.</summary>
    public void SetSpotlight(SpotlightState state)
    {
        if (currentSpotlight == state) return;
        currentSpotlight = state;

        StopFx();
        if (!gameObject.activeInHierarchy)
        {
            ApplySpotlightImmediate();
            return;
        }
        fxCoroutine = StartCoroutine(AnimateToSpotlight());
    }

    /// <summary>짧은 펄스(색 + 크기). 현재 스포트라이트 상태 위에 겹쳐서 튀게 하고, 끝나면 스포트라이트 기준으로 복귀.</summary>
    public void PlayFlash(Color color, float duration)
    {
        StopFx();
        if (!gameObject.activeInHierarchy)
        {
            ApplySpotlightImmediate();
            return;
        }
        fxCoroutine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.02f, duration)));
    }

    /// <summary>시퀀서 종료/인터럽트 시 모든 FX 상태를 Normal로 되돌린다.</summary>
    public void ResetSpotlight()
    {
        SetSpotlight(SpotlightState.Normal);
    }

    private void StopFx()
    {
        if (fxCoroutine != null)
        {
            StopCoroutine(fxCoroutine);
            fxCoroutine = null;
        }
    }

    private Color GetBaselineColor()
    {
        return isGrouped ? groupColor : originalColor;
    }

    private Color ApplySpotlightToColor(Color baseline)
    {
        if (currentSpotlight == SpotlightState.Dimmed)
        {
            return new Color(
                baseline.r * dimmedBrightness,
                baseline.g * dimmedBrightness,
                baseline.b * dimmedBrightness,
                baseline.a);
        }
        return baseline;
    }

    private float SpotlightScale()
    {
        return currentSpotlight == SpotlightState.Focused ? focusedScaleMultiplier : 1f;
    }

    private void ApplySpotlightImmediate()
    {
        if (sr != null) sr.color = ApplySpotlightToColor(GetBaselineColor());
        transform.localScale = baseLocalScale * SpotlightScale();
    }

    private IEnumerator AnimateToSpotlight()
    {
        Color startColor = sr != null ? sr.color : Color.white;
        Vector3 startScale = transform.localScale;
        Color targetColor = ApplySpotlightToColor(GetBaselineColor());
        Vector3 targetScale = baseLocalScale * SpotlightScale();

        float t = 0f;
        float duration = spotlightTransitionDuration;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            if (sr != null) sr.color = Color.Lerp(startColor, targetColor, ease);
            transform.localScale = Vector3.Lerp(startScale, targetScale, ease);
            yield return null;
        }
        if (sr != null) sr.color = targetColor;
        transform.localScale = targetScale;
        fxCoroutine = null;
    }

    private IEnumerator FlashRoutine(Color highlight, float totalDuration)
    {
        if (sr == null) yield break;

        Color baselineColor = ApplySpotlightToColor(GetBaselineColor());
        Vector3 baseScale = baseLocalScale * SpotlightScale();
        Vector3 peakScale = baseScale * flashScaleMultiplier;
        float half = Mathf.Max(0.01f, totalDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            sr.color = Color.Lerp(baselineColor, highlight, k);
            transform.localScale = Vector3.Lerp(baseScale, peakScale, k);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            sr.color = Color.Lerp(highlight, baselineColor, k);
            transform.localScale = Vector3.Lerp(peakScale, baseScale, k);
            yield return null;
        }

        sr.color = baselineColor;
        transform.localScale = baseScale;
        fxCoroutine = null;
    }
}
