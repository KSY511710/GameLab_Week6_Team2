using System;
using Special.Data;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// 티켓 기반 특수 블럭 뽑기 진입점.
    /// ResourceManager 의 티켓 재화(CurrencyType.Ticket) + specialDrawCost 를 사용.
    /// 당첨 시 OnSpecialBlockDrawn 이벤트를 발화해 인벤토리 UI 가 수신.
    /// </summary>
    public class SpecialGachaController : MonoBehaviour
    {
        [Header("Draw Table")]
        public SpecialDrawTable drawTable;

        [Header("Log")]
        public bool verboseLog = true;

        public static event Action<SpecialBlockDefinition> OnSpecialBlockDrawn;

        public void OnClickSpecialDraw()
        {
            if (drawTable == null)
            {
                Debug.LogWarning("[SpecialGacha] drawTable 이 비어 있음.");
                return;
            }
            if (ResourceManager.Instance == null) return;

            int cost = ResourceManager.Instance.specialDrawCost;
            if (!ResourceManager.Instance.SpendTicket(cost))
            {
                Debug.Log("[SpecialGacha] 티켓 부족으로 뽑기 실패.");
                return;
            }

            SpecialBlockDefinition rolled = drawTable.RollRandom();
            if (rolled == null)
            {
                Debug.LogWarning("[SpecialGacha] 당첨 후보가 없어 티켓 환급.");
                ResourceManager.Instance.AddTicket(cost);
                return;
            }

            if (verboseLog)
            {
                Debug.Log($"[SpecialGacha] 당첨! → {rolled.displayName} ({rolled.id})");
            }

            OnSpecialBlockDrawn?.Invoke(rolled);
        }
    }
}
