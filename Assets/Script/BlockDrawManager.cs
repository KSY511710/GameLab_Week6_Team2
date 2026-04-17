using UnityEngine;
using System.Collections.Generic;

public class BlockDrawManager : MonoBehaviour
{
    [Header("인벤토리 블록들")]
    // 📌 유니티 인스펙터에서 1칸, 2칸, 3칸짜리 UI 블록을 순서대로 넣어주세요.
    public List<DraggableBlock> inventoryBlocks;

    public void DrawNewBlock()
    {
        // 돈이 충분할 때만 뽑기 진행
        if (ResourceManager.Instance.SpendMoney(ResourceManager.Instance.drawCost))
        {
            // 1. 등록된 인벤토리 블록 중 하나를 랜덤으로 선택
            int randomIndex = Random.Range(0, inventoryBlocks.Count);

            // 2. 해당 블록의 개수(Count)를 +1 증가
            inventoryBlocks[randomIndex].AddBlock();
            

            Debug.Log($"<color=cyan>뽑기 성공!</color> {randomIndex + 1}번째 인벤토리 블록 획득!");
        }
    }
}