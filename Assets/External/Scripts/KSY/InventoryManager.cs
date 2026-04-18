using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("인벤토리 설정")]
    public int maxCapacity = 12;
    public Transform inventoryPanel;
    public TextMeshProUGUI capacityText;

    // 🌟 복잡한 입력창 싹 다 없애고, 프리팹만 쏙쏙 넣는 리스트로 변경!
    [Header("블록 프리팹 목록 (여기다 다 던져넣으세요!)")]
    public List<DraggableBlock> allBlockPrefabs;

    private int currentBlockCount = 0;

    private void Awake() { if (Instance == null) Instance = this; }

    private void OnEnable() { GachaConnector.OnBlockDrawn += HandleBlockDrawn; }
    private void OnDisable() { GachaConnector.OnBlockDrawn -= HandleBlockDrawn; }

    private void Start() { UpdateUI(); }

    public bool IsFull() { return currentBlockCount >= maxCapacity; }

    // 가챠에서 블록이 뽑혔을 때
    private void HandleBlockDrawn(KSM_GATCHA.CompanyColor drawnCompany, KSM_GATCHA.BlockSymbolType drawnSymbol, int drawnSize)
    {
        if (IsFull()) return;

        // 🌟 1. 조건에 맞는 프리팹들을 담아둘 빈 바구니(후보군)를 만듭니다.
        List<DraggableBlock> candidates = new List<DraggableBlock>();

        // 2. 리스트를 끝까지 돌면서 조건(색, 기호, 크기)이 맞는 애들을 전부 바구니에 담습니다.
        foreach (DraggableBlock prefab in allBlockPrefabs)
        {
            if (prefab.companyColor == drawnCompany &&
                prefab.symbolType == drawnSymbol &&
                prefab.blockSize == drawnSize)
            {
                candidates.Add(prefab); // 조건 맞으면 바구니에 쏙!
            }
        }

        // 3. 바구니에 담긴 후보가 있다면?
        if (candidates.Count > 0)
        {
            // 🌟 4. 후보들 중에서 무작위(Random)로 딱 하나만 뽑습니다!
            int randomIndex = Random.Range(0, candidates.Count);
            DraggableBlock targetPrefab = candidates[randomIndex];

            // 뽑힌 프리팹을 가방에 생성합니다.
            Instantiate(targetPrefab.gameObject, inventoryPanel);
            currentBlockCount++;
            UpdateUI();
        }
        else
        {
            Debug.LogError($"[가챠 에러] {drawnCompany}, {drawnSymbol}, 크기{drawnSize} 에 해당하는 프리팹을 찾을 수 없습니다!");
        }
    }

    public void OnBlockUsed()
    {
        currentBlockCount--;
        UpdateUI();
    }

    public void UpdateUI()
    {
        if (capacityText != null)
        {
            capacityText.text = $"보유 블록: {currentBlockCount} / {maxCapacity}";
            capacityText.color = IsFull() ? Color.red : Color.white;
        }
    }
}