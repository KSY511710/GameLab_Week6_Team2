using System.Collections.Generic;
using UnityEngine;

// 📌 가챠 결과(색상, 크기)와 실제 UI 인벤토리(DraggableBlock)를 짝지어주는 데이터 구조
[System.Serializable]
public class InventorySlotMapping
{
    public KSM_GATCHA.BlockColorTheme colorTheme; // 빨강, 파랑, 노랑
    public int blockSize;                         // 1, 2, 3칸
    public DraggableBlock targetUIBlock;          // 개수를 올려줄 UI 블록
}

public class GachaConnector : MonoBehaviour
{
    [Header("연결할 시스템")]
    public KSM_GATCHA gachaSystem;

    [Header("인벤토리 매핑 (인스펙터에서 연결)")]
    // 여기에 빨강1칸, 빨강2칸 ... 총 9개의 매핑을 인스펙터에서 등록합니다.
    public List<InventorySlotMapping> slotMappings = new List<InventorySlotMapping>();

    [Header("상점 패널")]
    public GameObject Shop;

    // UI에서 "뽑기 버튼"을 클릭했을 때 이 함수를 실행하게 연결합니다.
    public void OnClickDrawButton()
    {
        if (gachaSystem == null) return;

        // 1. 동료의 가챠 시스템에서 블록을 하나 뽑아옵니다.
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawBasicBlock();

        // 2. 뽑기 성공 시, 매핑된 인벤토리를 찾아 개수를 올려줍니다.
        if (result != null)
        {
            AddBlockToInventory(result);
        }
    }
    public void OnClickRedDrawButton()
    {
        if (gachaSystem == null) return;
        // 동료 코드의 '빨강 테마 뽑기'를 실행하고 결과를 받아옵니다.
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawRedTheme();

        if (result != null) AddBlockToInventory(result);
    }

    public void OnClickBlueDrawButton()
    {
        if (gachaSystem == null) return;
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawBlueTheme();
        if (result != null) AddBlockToInventory(result);
    }

    public void OnClickYellowDrawButton()
    {
        if (gachaSystem == null) return;
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawYellowTheme();
        if (result != null) AddBlockToInventory(result);
    }
    public void OnClickSizeDrawButton(int size)
    {
        if (gachaSystem == null) return;

        // 버튼에서 넘겨준 숫자(size)를 그대로 가챠 시스템에 전달합니다!
        KSM_GATCHA.GatchaBlockEntry result = gachaSystem.DrawBasicBlockBySize(size);

        if (result != null) AddBlockToInventory(result);
    }
    public void OnOffShop(bool ShopState)
    {
        if(Shop == null) return;
        Shop.SetActive(ShopState);
    }
    private void AddBlockToInventory(KSM_GATCHA.GatchaBlockEntry entry)
    {
        foreach (var slot in slotMappings)
        {
            // 가챠에서 나온 색상과 크기가 일치하는 UI 블록 슬롯을 찾습니다.
            if (slot.colorTheme == entry.colorTheme && slot.blockSize == entry.blockSize)
            {
                // 찾았다면 해당 DraggableBlock의 개수를 증가시킵니다!
                slot.targetUIBlock.AddBlock();
                Debug.Log($"✅ 인벤토리에 추가됨! [{entry.colorTheme} 색상 / {entry.blockSize}칸]");
                return;
            }
        }

        Debug.LogWarning("⚠️ 해당하는 인벤토리 슬롯을 찾지 못했습니다. 인스펙터 매핑을 확인하세요.");
    }
}