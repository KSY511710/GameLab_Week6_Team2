using System;
using System.Collections.Generic;
using Special.Data;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// 티켓 기반 특수 블럭 뽑기 진입점.
    /// ResourceManager 의 티켓 재화(CurrencyType.Ticket) + specialDrawCost 를 사용.
    ///
    /// 확장 원칙:
    /// 추후 새로운 특수 블럭 종류를 추가할 때는 해당 UI 프리팹(SpecialDraggableBlock)을
    /// specialBlockPrefabs 리스트에 등록하고, 대응하는 SpecialBlockDefinition 을 drawTable 에 넣기만 하면
    /// InventoryManager 의 공통 가방 파이프라인(TryAddBlock)을 통해 자동으로 연동된다.
    ///
    /// 이벤트 규약:
    /// OnSpecialBlockDrawn 은 뽑기 성공 후 가방 슬롯 생성까지 모두 끝난 시점에 발화한다.
    /// 효과 애니메이션·스킵 버튼 UX 훅 등 외부 구독자가 "확정된 획득"을 기준으로 동작할 수 있도록 보장.
    /// </summary>
    public class SpecialGachaController : MonoBehaviour
    {
        [Header("Draw Table")]
        public SpecialDrawTable drawTable;

        [Header("Inventory UI Prefabs")]
        [Tooltip("가방에 생성될 특수 블럭 UI 프리팹 목록. 각 프리팹의 SpecialDraggableBlock.definition 과 매칭된다.")]
        public List<SpecialDraggableBlock> specialBlockPrefabs = new List<SpecialDraggableBlock>();

        [Header("Log")]
        public bool verboseLog = true;

        public static event Action<SpecialBlockDefinition> OnSpecialBlockDrawn;
        public static event Action OnButtonPressed;

        public void OnClickSpecialDraw()
        {
            if (drawTable == null)
            {
                Debug.LogWarning("[SpecialGacha] drawTable 이 비어 있음.");
                return;
            }
            if (ResourceManager.Instance == null) return;

            // 1. 용량 게이트: 일반 가챠(GachaConnector)와 동일 규약으로 티켓 차감 전에 차단.
            if (InventoryManager.Instance != null && InventoryManager.Instance.IsFull())
            {
                Debug.LogWarning("[SpecialGacha] 가방이 꽉 차서 뽑기를 할 수 없습니다.");
                return;
            }

            // 2. 티켓 차감.
            int cost = ResourceManager.Instance.specialDrawCost;
            if (!ResourceManager.Instance.SpendTicket(cost))
            {
                Debug.Log("[SpecialGacha] 티켓 부족으로 뽑기 실패.");
                return;
            }

            // 3. 추첨.
            SpecialBlockDefinition rolled = drawTable.RollRandom();
            if (rolled == null)
            {
                Debug.LogWarning("[SpecialGacha] 당첨 후보가 없어 티켓 환급.");
                ResourceManager.Instance.AddTicket(cost);
                return;
            }
            OnButtonPressed?.Invoke();
            // 4. 정의 → UI 프리팹 매핑. 누락 시 상태를 되돌리고 실패 보고.
            SpecialDraggableBlock prefab = FindPrefabFor(rolled);
            if (prefab == null)
            {
                Debug.LogError($"[SpecialGacha] id={rolled.id} 에 대응하는 UI 프리팹이 specialBlockPrefabs 에 없습니다. 티켓 환급.");
                ResourceManager.Instance.AddTicket(cost);
                return;
            }

            // 5. 가방에 인스턴스 생성. 공통 진입점이 용량·UI 갱신을 책임진다.
            GameObject instance = null;
            if (InventoryManager.Instance != null)
            {
                instance = InventoryManager.Instance.TryAddBlock(prefab.gameObject);
                if (instance == null)
                {
                    // 1번 게이트 이후 용량이 소진된 레이스 케이스. 티켓 환급으로 불변식 유지.
                    Debug.LogWarning("[SpecialGacha] 가방 용량이 소진되어 티켓 환급.");
                    ResourceManager.Instance.AddTicket(cost);
                    return;
                }
            }

            // 6. 동일 id 에 대해 여러 프리팹 변형이 있더라도 런타임에서 실제 롤된 definition 을 강제 주입.
            if (instance != null)
            {
                SpecialDraggableBlock runtimeBlock = instance.GetComponent<SpecialDraggableBlock>();
                if (runtimeBlock != null) runtimeBlock.definition = rolled;
            }

            if (verboseLog)
            {
                Debug.Log($"[SpecialGacha] 당첨! → {rolled.displayName} ({rolled.id})");
            }

            OnSpecialBlockDrawn?.Invoke(rolled);
        }

        private SpecialDraggableBlock FindPrefabFor(SpecialBlockDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return null;

            for (int i = 0; i < specialBlockPrefabs.Count; i++)
            {
                SpecialDraggableBlock prefab = specialBlockPrefabs[i];
                if (prefab == null || prefab.definition == null) continue;
                if (prefab.definition.id == def.id) return prefab;
            }
            return null;
        }
    }
}
