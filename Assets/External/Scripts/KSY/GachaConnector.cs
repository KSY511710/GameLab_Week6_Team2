using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 📌 가챠 결과(회사, 부품, 크기)와 실제 UI 인벤토리(DraggableBlock)를 짝지어주는 데이터 구조
[System.Serializable]
public class InventorySlotMapping
{
    public KSM_GATCHA.CompanyColor gachaCompany;   // 회사(색상): Red, Blue, Yellow
    public KSM_GATCHA.BlockSymbolType gachaSymbol; // 부품(기호): Symbol01 ~ 09
    public int gachaSize;                          // 크기: 1, 2, 3칸
    public DraggableBlock targetUIBlock;           // 개수를 올려줄 UI 블록
}

public class GachaConnector : MonoBehaviour
{
    [Header("연결할 시스템")]
    public KSM_GATCHA gachaSystem;

    [Header("드롭다운 UI 연결")]
    public TMP_Dropdown companyDropdown; // 유니티에서 기업 선택 드롭다운 연결
    public TMP_Dropdown partDropdown;    // 유니티에서 부품 선택 드롭다운 연결

    [Header("상점 패널")]
    public GameObject Shop;

    public static event Action<KSM_GATCHA.CompanyColor, KSM_GATCHA.BlockSymbolType, int> OnBlockDrawn;
    public void OnClickCompanyDrawFromDropdown()
    {
        if (gachaSystem == null || companyDropdown == null) return;

        // 드롭다운 순서: 0(첫번째 항목), 1(두번째), 2(세번째)...
        // 동료분 코드의 인덱스는 1(빨강), 2(파랑), 3(노랑)이므로 +1을 해줍니다.
        int selectedIndex = companyDropdown.value + 1;

        gachaSystem.SetSelectedCompanyByIndex(selectedIndex);
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawCompanyBlock();

        if (result != null) BroadcastDrawResult(result);
    }

    public void OnClickPartDrawFromDropdown()
    {
        if (gachaSystem == null || partDropdown == null) return;

        // 부품 역시 0번 항목부터 시작하므로 +1을 해서 1~9번 부품으로 맞춰줍니다.
        int selectedIndex = partDropdown.value + 1;

        gachaSystem.SetSelectedPartSymbol(selectedIndex);
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawPartBlock();

        if (result != null) BroadcastDrawResult(result);
    }

    // ==========================================
    // 🎲 1. 일반 뽑기
    // ==========================================
    public void OnClickGeneralDraw()
    {
        if (gachaSystem == null) return;

        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawGeneralBlock();
        if (result != null) BroadcastDrawResult(result);
    }

    // ==========================================
    // 🏢 2. 색깔(기업) 선택 후 뽑기
    // ==========================================
    // 버튼의 OnClick () 에서 숫자를 적어주세요 -> 1(빨강), 2(파랑), 3(노랑)
    public void OnClickCompanyDraw(int companyIndex)
    {
        if (gachaSystem == null) return;
        Debug.Log(companyIndex);
        // 1. 가챠 기계에 뽑을 회사를 먼저 알려줍니다.
        gachaSystem.SetSelectedCompanyByIndex(companyIndex);

        // 2. 기업 확정 뽑기 레버를 당깁니다!
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawCompanyBlock();

        if (result != null) BroadcastDrawResult(result);
    }

    // ==========================================
    // ⚙️ 3. 부품(기호) 선택 후 뽑기
    // ==========================================
    // 버튼의 OnClick () 에서 숫자를 적어주세요 -> 1 ~ 9
    public void OnClickPartDraw(int partIndex)
    {
        if (gachaSystem == null) return;

        // 1. 가챠 기계에 뽑을 부품을 먼저 알려줍니다.
        gachaSystem.SetSelectedPartSymbol(partIndex);

        // 2. 부품 확정 뽑기 레버를 당깁니다!
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawPartBlock();

        if (result != null) BroadcastDrawResult(result);
    }

    // ==========================================
    // 🛒 상점 UI 켜고 끄기
    // ==========================================
    public void OnOffShop(bool ShopState)
    {
        if (Shop == null) return;
        Shop.SetActive(ShopState);
    }
    private void BroadcastDrawResult(KSM_GATCHA.GatchaBlockEntry entry)
    {
        Debug.Log($"[방송] 가챠 당첨! : {entry.companyColor} / {entry.symbolType} / {entry.blockSize}칸");

        // 확성기에 구독자(듣고 있는 블록)가 있다면 결과를 쏴줍니다!
        OnBlockDrawn?.Invoke(entry.companyColor, entry.symbolType, entry.blockSize);
    }

}