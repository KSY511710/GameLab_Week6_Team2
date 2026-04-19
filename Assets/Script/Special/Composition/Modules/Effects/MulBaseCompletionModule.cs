using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 c') PowerCalculationContext.BaseCompletionMul 곱셈.
    /// useScalarAsExponent=true 면 multiplier^condition.scalar 누승.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Mul Base Completion")]
    public class MulBaseCompletionModule : EffectModule
    {
        [Min(0f)] public float multiplier = 1.5f;
        public bool useScalarAsExponent = false;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                float m = useScalarAsExponent ? Mathf.Pow(multiplier, condition.scalar) : multiplier;
                power.BaseCompletionMul *= m;
            }
        }
    }
}
