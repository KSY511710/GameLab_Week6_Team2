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
                float before = power.ColorMultiplierMul;
                power.ColorMultiplierMul *= m;
                power.Trace?.RecordMul(CalcStage.ColorMultiplier, "색상 순도 배율", SourceName(owner), before, m);
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "색상 순도 배율 <color=#888888>효과 미발동</color>";
            float m = useScalarAsExponent ? Mathf.Pow(multiplier, condition.scalar) : multiplier;
            return $"색상 순도 배율 <color=#FF99CC>×{m:0.##}</color>";
        }
    }
}
