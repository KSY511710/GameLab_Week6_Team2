using System.Collections;
using System.Collections.Generic;
using Prediction;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class PlacedBlockVisual : MonoBehaviour
{
    /// <summary>시퀀서가 블럭을 강조/감쇠할 때 사용하는 상태. 색 밝기를 변조한다.</summary>
    public enum SpotlightState { Normal, Focused, Dimmed }

    private SpriteRenderer sr;
    private Sprite originalSprite;
    private Color originalColor;

    private bool isGrouped = false;
    private Color groupColor;
    public Sprite blankSprite;

    private List<PlacedBlockVisual> myGroupMembers;

    [Header("외곽선 설정")]
    public Color outlineColor = Color.black;
    public float outlineThickness = 0.05f; // 테두리 두께 조절
    public Material outlineMaterial;

    [Header("Spotlight FX")]
    [Tooltip("Dimmed 상태에서 색상 RGB에 곱해지는 배율. 값이 낮을수록 주변이 더 어두워진다.")]
    [SerializeField, Range(0f, 1f)] private float dimmedBrightness = 0.22f;
    [Tooltip("스포트라이트 상태 전환에 걸리는 시간.")]
    [SerializeField, Min(0.01f)] private float spotlightTransitionDuration = 0.18f;

    private GameObject lineTop, lineBottom, lineLeft, lineRight;
    private static Sprite sharedLineSprite;

    private SpotlightState currentSpotlight = SpotlightState.Normal;
    // 스포트라이트 전환과 플래시가 같은 SpriteRenderer/Transform을 건드리므로,
    // 한 번에 하나의 효과만 돌도록 단일 핸들로 직렬화한다.
    private Coroutine fxCoroutine;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalSprite = sr.sprite;
        originalColor = sr.color;

        CreateOutlineLines();
    }

    private void CreateOutlineLines()
    {
        // 1. 1x1 픽셀 텍스처와 스프라이트를 생성합니다. (tex 에러 방지를 위해 전체 작성)
        if (sharedLineSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            // 마지막 인자 1f가 십자 모양 문제를 해결하는 핵심입니다.
            sharedLineSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

        // 2. 선들을 생성하여 자식 오브젝트로 붙입니다.
        lineTop = CreateSingleLine("Line_Top", new Vector2(0, size.y / 2f), new Vector2(size.x, outlineThickness));
        lineBottom = CreateSingleLine("Line_Bottom", new Vector2(0, -size.y / 2f), new Vector2(size.x, outlineThickness));
        lineLeft = CreateSingleLine("Line_Left", new Vector2(-size.x / 2f, 0), new Vector2(outlineThickness, size.y));
        lineRight = CreateSingleLine("Line_Right", new Vector2(size.x / 2f, 0), new Vector2(outlineThickness, size.y));
    }

    private GameObject CreateSingleLine(string lineName, Vector2 offset, Vector2 scale)
    {
        GameObject lineObj = new GameObject(lineName);
        lineObj.transform.SetParent(this.transform);
        lineObj.transform.localPosition = offset;
        lineObj.transform.localScale = scale;

        SpriteRenderer lineSR = lineObj.AddComponent<SpriteRenderer>();
        lineSR.sprite = sharedLineSprite;
        lineSR.color = outlineColor;
        lineSR.sortingOrder = sr.sortingOrder + 1;

        // 🌟 추가: 인스펙터에 메테리얼을 넣었다면 렌더러에 적용해 줍니다!
        if (outlineMaterial != null)
        {
            lineSR.sharedMaterial = outlineMaterial;
        }

        lineObj.SetActive(false);
        return lineObj;
    }
    // ==========================================
    // 🎛️ 외곽선 끄고 켜기 (그룹 상태 체크 추가)
    // ==========================================
    public void UpdateOutline(bool top, bool bottom, bool left, bool right)
    {
        // 🌟 핵심: 그룹 상태(isGrouped)가 아니면 모든 아웃라인을 끕니다.
        if (!isGrouped)
        {
            lineTop?.SetActive(false);
            lineBottom?.SetActive(false);
            lineLeft?.SetActive(false);
            lineRight?.SetActive(false);
            return;
        }

        // 그룹 상태일 때만 인접 상태에 따라 선을 켭니다.
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

            // 그룹이 해제될 때 아웃라인도 즉시 초기화
            UpdateOutline(false, false, false, false);
        }

        // 그룹 전이 시 진행 중이던 스포트라이트 효과가 남아 있으면 새 베이스 색과 충돌하므로 정리.
        StopFx();
        currentSpotlight = SpotlightState.Normal;
    }

    // (기존 RevealOriginal, HideToGroupColor, OnMouseEnter, OnMouseExit 함수 유지...)
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
        BroadcastHover();
    }

    private void OnMouseExit()
    {
        if (isGrouped && myGroupMembers != null)
        {
            foreach (var member in myGroupMembers) if (member != null) member.HideToGroupColor();
        }
        PlacementInteractionHub.BroadcastHoverChanged(null);
    }

    private void BroadcastHover()
    {
        GridManager grid = Object.FindFirstObjectByType<GridManager>();
        if (grid == null || grid.groundTilemap == null) return;
        Vector3Int worldCell = grid.groundTilemap.WorldToCell(transform.position);
        Vector2Int arrayCell = grid.WorldCellToArrayIndex(worldCell);
        PlacementInteractionHub.BroadcastHoverChanged(HoverTarget.FromArrayCell(arrayCell));
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

    /// <summary>짧은 펄스(색). 현재 스포트라이트 상태 위에 겹쳐서 튀게 하고, 끝나면 스포트라이트 기준으로 복귀.</summary>
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

    private void ApplySpotlightImmediate()
    {
        if (sr != null) sr.color = ApplySpotlightToColor(GetBaselineColor());
    }

    private IEnumerator AnimateToSpotlight()
    {
        Color startColor = sr != null ? sr.color : Color.white;
        Color targetColor = ApplySpotlightToColor(GetBaselineColor());

        float t = 0f;
        float duration = spotlightTransitionDuration;
        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
            if (sr != null) sr.color = Color.Lerp(startColor, targetColor, ease);
            yield return null;
        }
        if (sr != null) sr.color = targetColor;
        fxCoroutine = null;
    }

    private IEnumerator FlashRoutine(Color highlight, float totalDuration)
    {
        if (sr == null) yield break;

        Color baselineColor = ApplySpotlightToColor(GetBaselineColor());
        float half = Mathf.Max(0.01f, totalDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            sr.color = Color.Lerp(baselineColor, highlight, k);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / half);
            sr.color = Color.Lerp(highlight, baselineColor, k);
            yield return null;
        }

        sr.color = baselineColor;
        fxCoroutine = null;
    }
}
