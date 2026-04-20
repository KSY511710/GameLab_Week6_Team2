using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class PlacedBlockVisual : MonoBehaviour
{
    /// <summary>
    /// 시퀀서가 블럭을 강조/감쇠할 때 사용하는 상태.
    /// 크기와 색 밝기를 함께 변조한다.
    /// </summary>
    public enum SpotlightState
    {
        Normal,
        Focused,
        Dimmed
    }

    [Header("Group Visual")]
    [Tooltip("그룹 상태에서 원본 스프라이트 대신 보여줄 단색용 스프라이트.")]
    public Sprite blankSprite;

    [Header("Spotlight FX")]
    [Tooltip("Focused 상태에서 블럭이 커지는 비율.")]
    [SerializeField, Range(1f, 2f)] private float focusedScaleMultiplier = 1.25f;

    [Tooltip("Dimmed 상태에서 색상 RGB에 곱해지는 배율.")]
    [SerializeField, Range(0f, 1f)] private float dimmedBrightness = 0.22f;

    [Tooltip("스포트라이트 상태 전환에 걸리는 시간.")]
    [SerializeField, Min(0.01f)] private float spotlightTransitionDuration = 0.18f;

    [Tooltip("플래시 펄스 피크에서 적용되는 추가 크기 배율.")]
    [SerializeField, Range(1f, 2f)] private float flashScaleMultiplier = 1.35f;

    /// <summary>
    /// 현재 셀의 SpriteRenderer.
    /// 원본/그룹/강조 색상 변경을 모두 이 렌더러를 통해 처리한다.
    /// </summary>
    private SpriteRenderer sr;

    /// <summary>
    /// 배치 직후 원래 가지고 있던 스프라이트.
    /// 그룹 내부 구성 표시(원본 복원) 시 사용한다.
    /// </summary>
    private Sprite originalSprite;

    /// <summary>
    /// 배치 직후 원래 가지고 있던 색상.
    /// 그룹 내부 구성 표시(원본 복원) 시 사용한다.
    /// </summary>
    private Color originalColor;

    /// <summary>
    /// 현재 이 셀이 발전소 그룹 상태인지 여부.
    /// true면 hover/zoom 조건에 따라 원본 또는 그룹 표현을 토글한다.
    /// </summary>
    private bool isGrouped = false;

    /// <summary>
    /// 그룹 상태일 때 기본적으로 보여줄 단색 발전소 색상.
    /// </summary>
    private Color groupColor;

    /// <summary>
    /// 같은 그룹에 속한 모든 셀의 PlacedBlockVisual 목록.
    /// hover 시 전체 그룹을 함께 reveal/hide 하기 위해 사용한다.
    /// </summary>
    private List<PlacedBlockVisual> myGroupMembers;

    /// <summary>
    /// 현재 마우스 hover 때문에 원본 구성 표시가 켜져 있는지 여부.
    /// </summary>
    private bool isHoverReveal;

    /// <summary>
    /// 현재 카메라 줌 임계값 때문에 자동 원본 표시가 켜져 있는지 여부.
    /// </summary>
    private bool isZoomReveal;

    /// <summary>
    /// 프리팹 자식으로 들어 있는 위/아래/좌/우 외곽선 오브젝트.
    /// 그룹 외곽선 계산 결과를 그대로 반영한다.
    /// </summary>
    private GameObject lineTop;
    private GameObject lineBottom;
    private GameObject lineLeft;
    private GameObject lineRight;

    /// <summary>
    /// 기본 스케일.
    /// Focused/Flash 연출 후 원상 복귀할 때 사용한다.
    /// </summary>
    private Vector3 baseLocalScale;

    /// <summary>
    /// 현재 지속 중인 spotlight 상태.
    /// </summary>
    private SpotlightState currentSpotlight = SpotlightState.Normal;

    /// <summary>
    /// Spotlight / Flash 코루틴 핸들.
    /// </summary>
    private Coroutine fxCoroutine;

    /// <summary>
    /// 컴포넌트 캐시와 초기 상태를 세팅한다.
    /// </summary>
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalSprite = sr.sprite;
        originalColor = sr.color;
        baseLocalScale = transform.localScale;

        Transform tLineU = transform.Find("Line_U");
        Transform tLineD = transform.Find("Line_D");
        Transform tLineL = transform.Find("Line_L");
        Transform tLineR = transform.Find("Line_R");

        lineTop = tLineU != null ? tLineU.gameObject : null;
        lineBottom = tLineD != null ? tLineD.gameObject : null;
        lineLeft = tLineL != null ? tLineL.gameObject : null;
        lineRight = tLineR != null ? tLineR.gameObject : null;

        // 시작 시에는 외곽선을 모두 꺼둔다.
        UpdateOutline(false, false, false, false);
    }

    /// <summary>
    /// 활성화 시 카메라 줌 기반 자동 표시 이벤트를 구독한다.
    /// 이미 카메라가 근접 상태라면 즉시 현재 상태를 반영한다.
    /// </summary>
    private void OnEnable()
    {
        KSM_CameraController.OnAutoRevealStateChanged += HandleAutoRevealStateChanged;
        isZoomReveal = KSM_CameraController.IsAutoRevealActive;
        RefreshPresentationImmediate();
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        KSM_CameraController.OnAutoRevealStateChanged -= HandleAutoRevealStateChanged;
    }

    /// <summary>
    /// 그룹 외곽선 표시를 갱신한다.
    /// 그룹 상태가 아니면 외곽선은 항상 꺼진다.
    /// </summary>
    public void UpdateOutline(bool top, bool bottom, bool left, bool right)
    {
        if (!isGrouped)
        {
            if (lineTop != null) lineTop.SetActive(false);
            if (lineBottom != null) lineBottom.SetActive(false);
            if (lineLeft != null) lineLeft.SetActive(false);
            if (lineRight != null) lineRight.SetActive(false);
            return;
        }

        if (lineTop != null) lineTop.SetActive(top);
        if (lineBottom != null) lineBottom.SetActive(bottom);
        if (lineLeft != null) lineLeft.SetActive(left);
        if (lineRight != null) lineRight.SetActive(right);
    }

    /// <summary>
    /// PowerManager가 그룹 확정/해제 시 호출한다.
    /// 그룹 상태, 그룹 색상, 그룹 멤버 목록을 저장하고 기본 표현을 즉시 맞춘다.
    /// </summary>
    public void SetGroupState(bool grouped, Color dominantColor, List<PlacedBlockVisual> groupList = null)
    {
        isGrouped = grouped;
        groupColor = dominantColor;
        myGroupMembers = groupList;

        // hover 상태는 그룹 상태가 재설정될 때 초기화한다.
        isHoverReveal = false;

        // 줌 기반 자동 표시 상태는 현재 카메라 상태를 바로 따라간다.
        isZoomReveal = grouped && KSM_CameraController.IsAutoRevealActive;

        if (!isGrouped)
        {
            myGroupMembers = null;
            UpdateOutline(false, false, false, false);
        }

        StopFx();
        currentSpotlight = SpotlightState.Normal;
        transform.localScale = baseLocalScale;
        RefreshPresentationImmediate();
    }

    /// <summary>
    /// 기존 공개 API 유지용.
    /// 이제는 단순히 hover reveal 상태를 켠다.
    /// </summary>
    public void RevealOriginal()
    {
        SetHoverReveal(true);
    }

    /// <summary>
    /// 기존 공개 API 유지용.
    /// 이제는 hover reveal 상태를 끈다.
    /// 줌 reveal이 켜져 있으면 계속 원본 상태가 유지된다.
    /// </summary>
    public void HideToGroupColor()
    {
        SetHoverReveal(false);
    }

    /// <summary>
    /// 그룹 표현 대신 원본 구성 표시를 해야 하는지 반환한다.
    /// hover 또는 zoom 중 하나라도 켜져 있으면 원본을 보여준다.
    /// </summary>
    private bool ShouldRevealOriginal()
    {
        return isGrouped && (isHoverReveal || isZoomReveal);
    }

    /// <summary>
    /// 현재 presentation 상태(원본 표시 / 그룹 표시)를 즉시 반영한다.
    /// spotlight 중이 아니면 색과 스케일까지 함께 현재 상태에 맞춘다.
    /// </summary>
    private void RefreshPresentationImmediate()
    {
        if (sr == null)
        {
            return;
        }

        if (!isGrouped)
        {
            sr.sprite = originalSprite;
            sr.color = ApplySpotlightToColor(originalColor);
            transform.localScale = baseLocalScale * SpotlightScale();
            return;
        }

        if (ShouldRevealOriginal())
        {
            sr.sprite = originalSprite;
        }
        else
        {
            sr.sprite = blankSprite != null ? blankSprite : originalSprite;
        }

        sr.color = ApplySpotlightToColor(GetBaselineColor());
        transform.localScale = baseLocalScale * SpotlightScale();
    }

    /// <summary>
    /// hover reveal 상태를 갱신한다.
    /// 그룹 전체가 함께 바뀌도록 외부에서는 RevealOriginal/HideToGroupColor를 통해 호출한다.
    /// </summary>
    private void SetHoverReveal(bool value)
    {
        if (isHoverReveal == value)
        {
            return;
        }

        isHoverReveal = value;
        RefreshPresentationImmediate();
    }

    /// <summary>
    /// 카메라 줌 임계값 변경 이벤트를 받으면 자동 표시 상태를 갱신한다.
    /// </summary>
    private void HandleAutoRevealStateChanged(bool value)
    {
        if (isZoomReveal == value)
        {
            return;
        }

        isZoomReveal = value;
        RefreshPresentationImmediate();
    }

    /// <summary>
    /// 마우스가 그룹 위에 올라오면 그룹 전체의 hover reveal을 켠다.
    /// </summary>
    private void OnMouseEnter()
    {
        if (!isGrouped || myGroupMembers == null)
        {
            return;
        }

        foreach (PlacedBlockVisual member in myGroupMembers)
        {
            if (member != null)
            {
                member.RevealOriginal();
            }
        }
    }

    /// <summary>
    /// 마우스가 그룹에서 벗어나면 그룹 전체의 hover reveal을 끈다.
    /// 줌 reveal이 켜져 있다면 원본 상태는 그대로 유지된다.
    /// </summary>
    private void OnMouseExit()
    {
        if (!isGrouped || myGroupMembers == null)
        {
            return;
        }

        foreach (PlacedBlockVisual member in myGroupMembers)
        {
            if (member != null)
            {
                member.HideToGroupColor();
            }
        }
    }

    /// <summary>
    /// 지속되는 강조/감쇠 상태를 설정한다.
    /// </summary>
    public void SetSpotlight(SpotlightState state)
    {
        if (currentSpotlight == state)
        {
            return;
        }

        currentSpotlight = state;

        StopFx();
        if (!gameObject.activeInHierarchy)
        {
            ApplySpotlightImmediate();
            return;
        }

        fxCoroutine = StartCoroutine(AnimateToSpotlight());
    }

    /// <summary>
    /// 짧은 펄스(색 + 크기) 연출을 재생한다.
    /// </summary>
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

    /// <summary>
    /// spotlight 상태를 Normal로 되돌린다.
    /// </summary>
    public void ResetSpotlight()
    {
        SetSpotlight(SpotlightState.Normal);
    }

    /// <summary>
    /// 현재 실행 중인 FX 코루틴을 정지한다.
    /// </summary>
    private void StopFx()
    {
        if (fxCoroutine != null)
        {
            StopCoroutine(fxCoroutine);
            fxCoroutine = null;
        }
    }

    /// <summary>
    /// 현재 presentation 기준의 기본 색상을 반환한다.
    /// 그룹 상태이면서 reveal이 꺼져 있으면 groupColor, 아니면 originalColor를 사용한다.
    /// </summary>
    private Color GetBaselineColor()
    {
        if (!isGrouped)
        {
            return originalColor;
        }

        return ShouldRevealOriginal() ? originalColor : groupColor;
    }

    /// <summary>
    /// spotlight 상태를 색상에 반영한다.
    /// Dimmed일 때만 RGB를 어둡게 만든다.
    /// </summary>
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

    /// <summary>
    /// spotlight 상태가 반영된 최종 스케일 배율을 반환한다.
    /// </summary>
    private float SpotlightScale()
    {
        return currentSpotlight == SpotlightState.Focused ? focusedScaleMultiplier : 1f;
    }

    /// <summary>
    /// 현재 spotlight 상태를 즉시 적용한다.
    /// presentation 상태(원본/그룹)도 함께 다시 맞춘다.
    /// </summary>
    private void ApplySpotlightImmediate()
    {
        RefreshPresentationImmediate();
    }

    /// <summary>
    /// spotlight 상태 전환을 부드럽게 보간한다.
    /// </summary>
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
            if (sr != null)
            {
                sr.color = Color.Lerp(startColor, targetColor, ease);
            }
            transform.localScale = Vector3.Lerp(startScale, targetScale, ease);
            yield return null;
        }

        if (sr != null)
        {
            sr.color = targetColor;
        }
        transform.localScale = targetScale;
        fxCoroutine = null;
    }

    /// <summary>
    /// highlight 색으로 짧게 번쩍였다가 현재 spotlight/presentation 기준 상태로 돌아오는 펄스 연출.
    /// </summary>
    private IEnumerator FlashRoutine(Color highlight, float totalDuration)
    {
        if (sr == null)
        {
            yield break;
        }

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