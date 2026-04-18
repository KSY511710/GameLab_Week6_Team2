using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class PlacedBlockVisual : MonoBehaviour
{
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

    private GameObject lineTop, lineBottom, lineLeft, lineRight;
    private static Sprite sharedLineSprite;

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
    }

    private void OnMouseExit()
    {
        if (isGrouped && myGroupMembers != null)
        {
            foreach (var member in myGroupMembers) if (member != null) member.HideToGroupColor();
        }
    }

    /// <summary>
    /// 시퀀서가 그룹/블럭 강조 시 호출. 끝나면 항상 원래(그룹/비그룹) 상태 색으로 복귀.
    /// 상위 호출자가 동시에 여러 멤버에 대해 StartCoroutine으로 돌려야 정확히 동기화됨.
    /// </summary>
    public IEnumerator FlashHighlight(Color highlight, float totalDuration)
    {
        if (sr == null) yield break;

        Color baseline = isGrouped ? groupColor : originalColor;
        float half = Mathf.Max(0.01f, totalDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            sr.color = Color.Lerp(baseline, highlight, t / half);
            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            sr.color = Color.Lerp(highlight, baseline, t / half);
            yield return null;
        }

        sr.color = baseline;
    }
}