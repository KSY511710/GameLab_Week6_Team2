using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope 내 발전소들의 finalShape unique 개수를 scalar 로 출력.
    /// minUniqueShapes 미만이면 fail (게이트 겸용). "다양한 모양이 모일수록 강해지는" 효과용.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Shape Variety")]
    public class ShapeVarietyCondition : ConditionModule
    {
        [Tooltip("통과 기준 고유 모양 수. 0 이면 게이트 없이 scalar 만 출력.")]
        [Min(0)] public int minUniqueShapes = 0;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int variety = ScopeQueryService.QueryShapeVariety(owner, scope, range);
            if (variety < minUniqueShapes) return ConditionResult.Fail();
            return ConditionResult.Pass(ApplyCoefficient(variety));
        }
    }
}
