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
                float before = power.BaseCompletionMul;
                power.BaseCompletionMul *= m;
                power.Trace?.RecordMul(CalcStage.BaseCompletion, "기본 완성도 배율", SourceName(owner), before, m);
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "기본 완성도 배율 <color=#888888>효과 미발동</color>";
            float m = useScalarAsExponent ? Mathf.Pow(multiplier, condition.scalar) : multiplier;
            return $"기본 완성도 배율 <color=#66D9FF>×{m:0.##}</color>";
        }
    }
}
