using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope 내 특정 색 블럭(그룹 여부 무관) 개수를 scalar 로 출력. 항상 passed.
    /// "빨강 블럭 개수만큼 전력 +N" 같은 효과의 스케일 입력으로 사용.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Count Blocks By Color")]
    public class CountBlocksByColorCondition : ConditionModule
    {
        [Tooltip("대상 색상: 1=Red, 2=Blue, 3=Yellow")]
        [Range(1, 3)] public int targetColorId = 1;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int count = ScopeQueryService.QueryBlocksByColor(owner, scope, range, targetColorId).Count;
            return ConditionResult.Pass(ApplyCoefficient(count));
        }
    }
}
