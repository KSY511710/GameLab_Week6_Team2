using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// InventoryManager
/// 
/// 역할:
/// 1. 특수 블럭 가방 용량과 UI를 관리한다.
/// 2. 일반 블럭 상점 슬롯을 생성하고 채운다.
/// 3. 블럭 사용 후 중력을 적용해 아래 슬롯으로 당긴다.
/// 4. 빈칸을 다시 채운다.
/// 5. 무료 갱신 / 리롤 갱신을 처리한다.
/// 6. 리롤 성공 시 ChangeBlock SFX를 재생한다.
/// 
/// 사운드 규칙:
/// - ChangeBlock: 플레이어가 리롤 버튼을 눌러 상점을 교체할 때만 1회 재생
/// - ProduceBlock: 여기서 재생하지 않음
/// 
/// 주의:
/// - ProduceBlock 은 DraggableBlock 쪽의 "배치 성공 순간"에 재생하는 구조로 분리했다.
/// - 따라서 InventoryManager 내부에서는 레일 소리를 재생하지 않는다.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    /// <summary>
    /// 전역 접근용 싱글톤 인스턴스다.
    /// </summary>
    public static InventoryManager Instance;

    [Header("🌟 가방 (특수 블록)")]
    [Tooltip("가방에 담을 수 있는 특수 블럭 최대 개수.")]
    public int maxCapacity = 12;

    [Tooltip("특수 블럭이 들어갈 가방 UI 부모 Transform.")]
    public Transform inventoryPanel;

    [Tooltip("현재 가방 용량 상태를 보여주는 텍스트.")]
    public TextMeshProUGUI capacityText;

    [Header("🌟 상점 (일반 블록)")]
    [Tooltip("상점 슬롯 개수.")]
    public int maxNormalCapacity = 3;

    [Tooltip("일반 블럭 슬롯들이 생성될 부모 Transform.")]
    public Transform normalInventoryPanel;

    [Tooltip("빈 상점 슬롯 프리팹.")]
    public GameObject emptySlotPrefab;

    [Tooltip("일반 블럭 후보 프리팹 목록.")]
    public List<DraggableBlock> allBlockPrefabs;

    /// <summary>
    /// 실제 생성된 상점 슬롯 Transform 목록이다.
    /// </summary>
    private List<Transform> shopSlots = new List<Transform>();

    /// <summary>
    /// 현재 가방에 들어있는 특수 블럭 개수다.
    /// </summary>
    private int currentBlockCount = 0;

    /// <summary>
    /// 싱글톤 초기화.
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    /// <summary>
    /// 시작 시 슬롯을 만들고 UI를 갱신한 뒤 상점을 초기 채운다.
    /// </summary>
    private void Start()
    {
        InitializeSlots();
        UpdateUI();
        RefillNormalBlocks();
    }

    /// <summary>
    /// 일반 블럭 상점 슬롯을 maxNormalCapacity 만큼 생성한다.
    /// </summary>
    private void InitializeSlots()
    {
        for (int i = 0; i < maxNormalCapacity; i++)
        {
            GameObject newSlot = Instantiate(emptySlotPrefab, normalInventoryPanel);
            newSlot.name = $"Slot_{i}";
            shopSlots.Add(newSlot.transform);
        }
    }

    /// <summary>
    /// 블럭 사용 후 상점 슬롯에 중력을 적용하고, 빈 슬롯을 다시 채운다.
    /// 
    /// 동작:
    /// 1. 아래쪽 빈 슬롯부터 검사한다.
    /// 2. 위쪽에 블럭이 있으면 아래로 당긴다.
    /// 3. 마지막에 남은 빈 슬롯을 새 블럭으로 채운다.
    /// 
    /// 주의:
    /// - ProduceBlock SFX는 여기서 재생하지 않는다.
    /// - 그 소리는 DraggableBlock 배치 성공 순간에 이미 재생된다.
    /// </summary>
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

    /// <summary>
    /// 상점의 빈 슬롯을 일반 블럭으로 채운다.
    /// 
    /// 동작:
    /// 1. 비어 있는 슬롯만 검사한다.
    /// 2. 프리팹 풀에서 랜덤 블럭을 하나 생성한다.
    /// 3. 위쪽 위치에서 시작해 SlideToZero 애니메이션으로 내려오게 한다.
    /// 
    /// 주의:
    /// - ProduceBlock SFX는 여기서 재생하지 않는다.
    /// </summary>
    public void RefillNormalBlocks()
    {
        if (allBlockPrefabs == null || allBlockPrefabs.Count == 0) return;

        foreach (Transform slot in shopSlots)
        {
            if (slot.childCount == 0)
            {
                int randomIndex = Random.Range(0, allBlockPrefabs.Count);
                GameObject newBlock = Instantiate(allBlockPrefabs[randomIndex].gameObject, slot);

                newBlock.transform.localPosition = new Vector3(0f, 500f, 0f);
                StartCoroutine(SlideToZero(newBlock.transform));
            }
        }
    }

    /// <summary>
    /// 블럭이 슬롯 내부의 현재 위치에서 Vector3.zero 까지 부드럽게 이동하도록 한다.
    /// </summary>
    /// <param name="target">이동시킬 블럭 Transform</param>
    private IEnumerator SlideToZero(Transform target)
    {
        Vector3 startPos = target.localPosition;
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (target == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            target.localPosition = Vector3.Lerp(startPos, Vector3.zero, smoothT);
            yield return null;
        }

        if (target != null)
        {
            target.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// 특수 블럭 가방이 꽉 찼는지 반환한다.
    /// </summary>
    /// <returns>가방이 최대치 이상이면 true</returns>
    public bool IsFull()
    {
        return currentBlockCount >= maxCapacity;
    }

    /// <summary>
    /// 특수 블럭 프리팹을 가방에 추가한다.
    /// 
    /// 역할:
    /// 1. null 검사
    /// 2. 용량 초과 검사
    /// 3. 인스턴스 생성
    /// 4. 현재 개수 증가 및 UI 갱신
    /// </summary>
    /// <param name="prefab">가방에 넣을 특수 블럭 프리팹</param>
    /// <returns>생성된 인스턴스, 실패 시 null</returns>
    public GameObject TryAddBlock(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (IsFull())
        {
            return null;
        }

        GameObject instance = Instantiate(prefab, inventoryPanel);
        currentBlockCount++;
        UpdateUI();
        return instance;
    }

    /// <summary>
    /// 특수 블럭 하나를 사용한 뒤 현재 개수를 줄이고 UI를 갱신한다.
    /// </summary>
    public void OnBlockUsed()
    {
        currentBlockCount--;
        UpdateUI();
    }

    /// <summary>
    /// 가방 용량 UI를 갱신한다.
    /// </summary>
    public void UpdateUI()
    {
        if (capacityText != null)
        {
            capacityText.text = $"보유 블록: {currentBlockCount} / {maxCapacity}";
            capacityText.color = IsFull() ? Color.red : Color.white;
        }
    }

    /// <summary>
    /// 무료 상점 갱신 요청.
    /// 
    /// 역할:
    /// - 다음 날 시작 등 무료 갱신 상황에서 사용한다.
    /// - ChangeBlock SFX는 재생하지 않는다.
    /// </summary>
    public void RefreshShopFree()
    {
        Debug.Log("새로운 날이 밝았습니다! 상점을 무료로 갱신합니다.");
        StartCoroutine(RefreshShopRoutine());
    }

    /// <summary>
    /// 유료 리롤 요청.
    /// 
    /// 역할:
    /// 1. 리롤 비용 지불을 시도한다.
    /// 2. 성공 시 ChangeBlock SFX를 재생한다.
    /// 3. 이후 실제 상점 재구성 루틴을 시작한다.
    /// </summary>
    public void RequestReroll()
    {
        if (ResourceManager.Instance != null && ResourceManager.Instance.TryPayForReroll())
        {
            PlayChangeBlockSfx();
            StartCoroutine(RefreshShopRoutine());
        }
        else
        {
            Debug.Log("리롤 비용이 부족합니다!");
        }
    }

    /// <summary>
    /// 현재 상점 블럭을 모두 비우고 다시 채우는 공통 갱신 루틴이다.
    /// 
    /// 동작:
    /// 1. 각 슬롯의 현재 블럭을 파괴한다.
    /// 2. Destroy 반영을 위해 한 프레임 대기한다.
    /// 3. 새 블럭을 다시 채운다.
    /// 
    /// 주의:
    /// - ChangeBlock SFX는 RequestReroll 에서만 재생한다.
    /// - ProduceBlock SFX는 여기서 재생하지 않는다.
    /// </summary>
    private IEnumerator RefreshShopRoutine()
    {
        foreach (Transform slot in shopSlots)
        {
            if (slot.childCount > 0)
            {
                Destroy(slot.GetChild(0).gameObject);
            }
        }

        yield return null;

        RefillNormalBlocks();
    }

    /// <summary>
    /// 리롤 교체음(ChangeBlock)을 안전하게 재생한다.
    /// </summary>
    private void PlayChangeBlockSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlaySfx(KSM_SfxType.ChangeBlock);
        }
    }
}