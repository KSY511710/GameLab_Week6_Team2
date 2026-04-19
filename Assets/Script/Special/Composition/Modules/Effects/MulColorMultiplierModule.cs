using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 e) PowerCalculationContext.ColorMultiplierMul 곱셈.
    /// useScalarAsExponent=true 면 condition.scalar 를 지수로 누승.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Mul Color Multiplier")]
    public class MulColorMultiplierModule : EffectModule
    {
        [Min(0f)] public float multiplier = 1.5f;
        public bool useScalarAsExponent = false;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                float m = useScalarAsExponent ? Mathf.Pow(multiplier, condition.scalar) : multiplier;
                power.ColorMultiplierMul *= m;
            }
        }
    }
}
