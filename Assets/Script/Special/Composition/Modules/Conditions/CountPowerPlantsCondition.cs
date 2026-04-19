using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope/range 내 발전소(GroupInfo) 개수를 scalar 로 출력. 항상 passed.
    /// 레거시 AddBaseProductionEffect 의 "범위 내 발전소 수" 카운트 대응.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Count Power Plants")]
    public class CountPowerPlantsCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int count = ScopeQueryService.QueryPowerPlants(owner, scope, range).Count;
            return ConditionResult.Pass(ApplyCoefficient(count));
        }
    }
}
