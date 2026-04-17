using System.Collections.Generic; // 📌 List를 쓰기 위해 추가
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

    // 🌟 추가: 우리 그룹에 속한 모든 팀원들의 명단!
    private List<PlacedBlockVisual> myGroupMembers;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        originalSprite = sr.sprite;
        originalColor = sr.color;
    }

    // 📌 SetGroupState가 이제 '팀원 명단(groupList)'도 함께 받습니다!
    public void SetGroupState(bool grouped, Color dominantColor, List<PlacedBlockVisual> groupList = null)
    {
        isGrouped = grouped;
        groupColor = dominantColor;
        myGroupMembers = groupList; // 명단 저장!

        if (isGrouped)
        {
            if (blankSprite != null) sr.sprite = blankSprite;
            sr.color = groupColor;
        }
        else
        {
            sr.sprite = originalSprite;
            sr.color = originalColor;
            myGroupMembers = null; // 그룹 깨지면 명단 파기
        }
    }

    // ==========================================
    // 📢 팀원들에게 명령을 내리기 위한 함수들
    // ==========================================
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

    // ==========================================
    // 🖱️ 마우스 이벤트 (나 혼자 변하는 게 아니라 모두를 변하게 함!)
    // ==========================================
    private void OnMouseEnter()
    {
        if (isGrouped && myGroupMembers != null)
        {
            // 명단에 있는 모든 팀원들에게 "본모습 드러내!" 명령
            foreach (var member in myGroupMembers)
            {
                if (member != null) member.RevealOriginal();
            }
        }
    }

    private void OnMouseExit()
    {
        if (isGrouped && myGroupMembers != null)
        {
            // 명단에 있는 모든 팀원들에게 "다시 숨어!" 명령
            foreach (var member in myGroupMembers)
            {
                if (member != null) member.HideToGroupColor();
            }
        }
    }
}