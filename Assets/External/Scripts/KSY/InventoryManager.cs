using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("🌟 가방 (특수 블록)")]
    public int maxCapacity = 12;
    public Transform inventoryPanel;
    public TextMeshProUGUI capacityText;

    [Header("🌟 상점 (일반 블록)")]
    public int maxNormalCapacity = 3;
    public Transform normalInventoryPanel;
    public GameObject emptySlotPrefab;
    public List<DraggableBlock> allBlockPrefabs;

    private List<Transform> shopSlots = new List<Transform>();
    private int currentBlockCount = 0;

    private void Awake() { if (Instance == null) Instance = this; }

    private void Start()
    {
        InitializeSlots();
        UpdateUI();
        RefillNormalBlocks();
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < maxNormalCapacity; i++)
        {
            GameObject newSlot = Instantiate(emptySlotPrefab, normalInventoryPanel);
            newSlot.name = $"Slot_{i}";
            shopSlots.Add(newSlot.transform);
        }
    }

    public void ProcessGravityAndRefill()
    {

        for (int i = shopSlots.Count - 1; i > 0; i--)
        {
            if (shopSlots[i].childCount == 0)
            {

                for (int j = i - 1; j >= 0; j--)
                {
                    if (shopSlots[j].childCount > 0)
                    {
                        Transform blockToMove = shopSlots[j].GetChild(0);

                        blockToMove.SetParent(shopSlots[i], true);

                        StartCoroutine(SlideToZero(blockToMove));
                        break; 
                    }
                }
            }
        }

        RefillNormalBlocks();
    }

    public void RefillNormalBlocks()
    {
        if (allBlockPrefabs == null || allBlockPrefabs.Count == 0) return;

        foreach (Transform slot in shopSlots)
        {
            if (slot.childCount == 0)
            {
                int randomIndex = Random.Range(0, allBlockPrefabs.Count);
                GameObject newBlock = Instantiate(allBlockPrefabs[randomIndex].gameObject, slot);

                newBlock.transform.localPosition = new Vector3(0, 500f, 0);
                StartCoroutine(SlideToZero(newBlock.transform));
            }
        }
    }

    private IEnumerator SlideToZero(Transform target)
    {
        Vector3 startPos = target.localPosition;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f); // Ease-Out 효과

            target.localPosition = Vector3.Lerp(startPos, Vector3.zero, smoothT);
            yield return null;
        }
        if (target != null) target.localPosition = Vector3.zero;
    }

    // (나머지 TryAddBlock, OnBlockUsed, UpdateUI 함수는 기존과 동일하게 유지)
    public bool IsFull() { return currentBlockCount >= maxCapacity; }

    public GameObject TryAddBlock(GameObject prefab)
    {
        if (prefab == null) return null;
        if (IsFull()) return null;
        GameObject instance = Instantiate(prefab, inventoryPanel);
        currentBlockCount++;
        UpdateUI();
        return instance;
    }

    public void OnBlockUsed() { currentBlockCount--; UpdateUI(); }

    public void UpdateUI()
    {
        if (capacityText != null)
        {
            capacityText.text = $"보유 블록: {currentBlockCount} / {maxCapacity}";
            capacityText.color = IsFull() ? Color.red : Color.white;
        }
    }
    public void RefreshShopFree()
    {
        Debug.Log("새로운 날이 밝았습니다! 상점을 무료로 갱신합니다.");
        StartCoroutine(RefreshShopRoutine());
    }

    // 실제 상점을 싹 비우고 채우는 핵심 로직 (공통 사용)
    private IEnumerator RefreshShopRoutine()
    {
        // 현재 슬롯의 모든 블록 파괴
        foreach (Transform slot in shopSlots)
        {
            if (slot.childCount > 0)
            {
                Destroy(slot.GetChild(0).gameObject);
            }
        }

        // Destroy가 완료될 때까지 한 프레임 대기
        yield return null;

        // 새로운 블록 채우기
        RefillNormalBlocks();
    }
}