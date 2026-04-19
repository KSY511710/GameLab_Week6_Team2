using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// 특정 재화(Electricity/Money/Ticket) 가 threshold 이상이면 passed.
    /// scalar = 재화 현재 보유량 (계수 적용 후). 미달 시 fail.
    /// 예: 돈 100 이상이면 전력 ×1.5 조건의 게이트.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Currency Threshold")]
    public class CurrencyThresholdCondition : ConditionModule
    {
        public CurrencyType currency = CurrencyType.Money;
        [Min(0)] public int threshold = 0;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int amount = ResourceManager.Instance != null
                ? ResourceManager.Instance.GetCurrency(currency)
                : 0;
            if (amount < threshold) return ConditionResult.Fail();
            return ConditionResult.Pass(ApplyCoefficient(amount));
        }
    }
}
