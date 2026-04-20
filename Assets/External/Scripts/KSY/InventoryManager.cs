using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InventoryManager
///
/// 역할:
/// 1. 특수 블럭 가방 용량과 UI를 관리한다.
/// 2. 일반 블럭 상점 슬롯을 생성하고 채운다.
/// 3. 블럭 사용 후 중력을 적용해 아래 슬롯으로 당긴다.
/// 4. 빈 슬롯을 다시 채운다.
/// 5. 무료 갱신 / 리롤 갱신을 처리한다.
/// 6. 리롤 성공 시 ChangeBlock SFX를 1회 재생한다.
///
/// 병합 기준:
/// - 기능 유지 기준은 "현재 동작하던 버전"을 따른다.
/// - 즉, TryAddBlock 의 빈 슬롯 탐색 / RectTransform 보정 / 숫자만 표시되는 UI / OnButtonPressed 이벤트는 유지한다.
/// - 추가 변경은 RequestReroll 에서 ChangeBlock 사운드를 재생하는 부분만 넣는다.
/// </summary>
public class InventoryManager : MonoBehaviour
{
    /// <summary>
    /// 전역 접근용 싱글톤 인스턴스.
    /// 다른 스크립트에서 InventoryManager.Instance 로 접근할 때 사용한다.
    /// </summary>
    public static InventoryManager Instance;

    /// <summary>
    /// 리롤 버튼 성공 처리 시 외부 구독자에게 알리는 이벤트.
    /// 기존 프로젝트에서 이미 이 이벤트를 사용하고 있을 수 있으므로 유지한다.
    /// </summary>
    public static event Action OnButtonPressed;

    [Header("🌟 가방 (특수 블록)")]

    /// <summary>
    /// 가방에 담을 수 있는 특수 블럭 최대 개수.
    /// </summary>
    public int maxCapacity = 12;

    /// <summary>
    /// 특수 블럭 UI 슬롯들의 부모 Transform.
    /// inventoryPanel 아래 자식들을 빈 슬롯으로 간주한다.
    /// </summary>
    public Transform inventoryPanel;

    /// <summary>
    /// 현재 가방 보유량을 표시하는 텍스트.
    /// 현재 프로젝트 동작 유지 기준으로 숫자만 표시한다.
    /// </summary>
    public TextMeshProUGUI capacityText;

    [Header("🌟 상점 (일반 블록)")]

    /// <summary>
    /// 상점 슬롯 개수.
    /// </summary>
    public int maxNormalCapacity = 3;

    /// <summary>
    /// 일반 블럭 슬롯들이 생성될 부모 Transform.
    /// </summary>
    public Transform normalInventoryPanel;

    /// <summary>
    /// 빈 상점 슬롯 프리팹.
    /// 상점 시작 시 maxNormalCapacity 개수만큼 생성된다.
    /// </summary>
    public GameObject emptySlotPrefab;

    /// <summary>
    /// 상점에서 랜덤 생성할 일반 블럭 프리팹 목록.
    /// </summary>
    public List<DraggableBlock> allBlockPrefabs;

    /// <summary>
    /// 실제 생성된 상점 슬롯 Transform 목록.
    /// ProcessGravityAndRefill / RefillNormalBlocks 에서 사용한다.
    /// </summary>
    private readonly List<Transform> shopSlots = new List<Transform>();

    /// <summary>
    /// 현재 가방에 들어있는 특수 블럭 개수.
    /// TryAddBlock / OnBlockUsed 에서 증감한다.
    /// </summary>
    private int currentBlockCount = 0;
    [Header("🌟 UI 참조")]
    [Tooltip("상점 전체를 포함하고 있는 UI 패널 오브젝트를 드래그해서 넣어주세요.")]
    public GameObject shopPanel;
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
    /// 시작 시 상점 슬롯을 만들고, UI를 갱신한 뒤, 상점을 초기 채운다.
    /// </summary>
    private void Start()
    {
        InitializeSlots();
        UpdateUI();
        RefillNormalBlocks();
    }

    private void Update()
    {
        // 1) 아무도 드래그 중이지 않고
        // 3) 키보드가 연결되어 있고
        // 4) R키를 방금 눌렀다면
        if (!DraggableBlock.IsAnyBlockDragging &&
            shopPanel != null && !shopPanel.activeInHierarchy &&
            Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            RequestReroll();
        }
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
    /// 2. 위쪽에 블럭이 있으면 아래 슬롯으로 이동시킨다.
    /// 3. 이동 후 남은 빈 슬롯은 새 블럭으로 채운다.
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
    /// - 비어 있는 슬롯만 검사한다.
    /// - 프리팹 목록 중 랜덤 하나를 생성한다.
    /// - 위쪽에서 내려오는 연출을 위해 localPosition 을 위로 잡은 뒤 SlideToZero 를 실행한다.
    /// </summary>
    public void RefillNormalBlocks()
    {
        if (allBlockPrefabs == null || allBlockPrefabs.Count == 0)
        {
            return;
        }

        foreach (Transform slot in shopSlots)
        {
            if (slot.childCount == 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, allBlockPrefabs.Count);
                GameObject newBlock = Instantiate(allBlockPrefabs[randomIndex].gameObject, slot);

                newBlock.transform.localPosition = new Vector3(0f, 500f, 0f);
                StartCoroutine(SlideToZero(newBlock.transform));
            }
        }
    }

    /// <summary>
    /// 슬롯 내부에서 현재 위치 → Vector3.zero 까지 부드럽게 이동시킨다.
    /// </summary>
    /// <param name="target">이동시킬 대상 Transform</param>
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
    /// <returns>현재 개수가 최대 용량 이상이면 true</returns>
    public bool IsFull()
    {
        return currentBlockCount >= maxCapacity;
    }

    /// <summary>
    /// 특수 블럭 프리팹을 가방에 추가한다.
    ///
    /// 기능 유지 기준으로 아래 로직을 살린다:
    /// - inventoryPanel 자식 슬롯 중 비어 있는 슬롯을 먼저 찾는다.
    /// - 빈 슬롯이 있으면 그 슬롯에 넣는다.
    /// - 없으면 inventoryPanel 바로 아래에 넣는다.
    /// - RectTransform 크기/위치/스케일을 강제로 초기화해 UI 틀어짐을 막는다.
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

        Transform targetEmptySlot = null;

        if (inventoryPanel != null)
        {
            for (int i = 0; i < inventoryPanel.childCount; i++)
            {
                Transform slot = inventoryPanel.GetChild(i);

                if (slot.childCount == 0)
                {
                    targetEmptySlot = slot;
                    break;
                }
            }
        }

        Transform parentTransform = (targetEmptySlot != null) ? targetEmptySlot : inventoryPanel;
        GameObject instance = Instantiate(prefab, parentTransform);

        RectTransform rect = instance.GetComponent<RectTransform>();
        if (rect != null)
        {
            /// <summary>
            /// UI 프리팹이 슬롯에 들어갈 때 크기/위치가 틀어지는 문제를 막기 위한 보정.
            /// 기존 동작 유지 기준으로 남긴다.
            /// </summary>
            rect.sizeDelta = new Vector2(100f, 100f);
            rect.localPosition = Vector3.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

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

        /// <summary>
        /// 방어 코드.
        /// 예외 상황으로 음수가 되는 것을 막는다.
        /// 기존 기능을 해치지 않는 안전장치다.
        /// </summary>
        if (currentBlockCount < 0)
        {
            currentBlockCount = 0;
        }

        UpdateUI();
    }

    /// <summary>
    /// 가방 용량 UI를 갱신한다.
    ///
    /// 기능 유지 기준:
    /// - 현재 프로젝트는 숫자만 표시하는 형태를 사용 중인 것으로 보이므로 그 형식을 유지한다.
    /// </summary>
    public void UpdateUI()
    {
        if (capacityText != null)
        {
            capacityText.text = $"{currentBlockCount}";
            capacityText.color = IsFull() ? Color.red : Color.white;
        }
    }

    /// <summary>
    /// 무료 상점 갱신 요청.
    /// 다음 날 시작 등 무료 갱신 상황에서 사용한다.
    /// </summary>
    public void RefreshShopFree()
    {
        Debug.Log("새로운 날이 밝았습니다! 상점을 무료로 갱신합니다.");
        StartCoroutine(RefreshShopRoutine());
    }

    /// <summary>
    /// 유료 리롤 요청.
    ///
    /// 병합 결과:
    /// - 기존 기능 유지: OnButtonPressed 이벤트 호출 유지
    /// - 추가 기능: 리롤 성공 시 ChangeBlock 사운드 1회 재생
    /// </summary>
    public void RequestReroll()
    {
        if (ResourceManager.Instance != null && ResourceManager.Instance.TryPayForReroll())
        {
            /// <summary>
            /// 기존 프로젝트에서 사용하던 성공 이벤트.
            /// 외부에서 애니메이션 / 버튼 효과 / 다른 처리에 물려 있을 수 있으므로 유지한다.
            /// </summary>
            OnButtonPressed?.Invoke();

            /// <summary>
            /// 이번 병합에서 추가되는 사운드 재생.
            /// 리롤이 실제로 성공했을 때만 1회 재생한다.
            /// </summary>
            PlayChangeBlockSfx();

            StartCoroutine(RefreshShopRoutine());
        }
        else
        {
            Debug.Log("리롤 비용이 부족합니다!");
        }
    }

    /// <summary>
    /// 현재 상점 블럭을 모두 비우고 다시 채우는 공통 갱신 루틴.
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
    /// 사운드 매니저가 없을 때 NullReference 가 나지 않도록 방어한다.
    /// </summary>
    private void PlayChangeBlockSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlaySfx(KSM_SfxType.ChangeBlock);
        }
    }
}