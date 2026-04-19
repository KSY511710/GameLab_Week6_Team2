using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// 항상 passed 이고 scalar = value * multiplier / divisor 를 출력.
    /// 조건 없이 "무조건 +N / ×N" 계열 효과의 입력, 혹은 스냅/게이트가 필요 없을 때 사용.
    /// 예: ConstantValueCondition{value=1} + MulFinalPowerModule{multiplier=1.2}
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Constant Value")]
    public class ConstantValueCondition : ConditionModule
    {
        public float value = 1f;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            return ConditionResult.Pass(ApplyCoefficient(value));
        }
    }
}
