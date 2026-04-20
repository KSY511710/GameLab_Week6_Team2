using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 c) PowerCalculationContext.BaseCompletionAdd 에 (condition.scalar * perScalar) 가산.
    /// 기본 완성도 보너스(=기존 상수 2 부분)에 가산되는 효과.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Base Completion")]
    public class AddBaseCompletionModule : EffectModule
    {
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                int add = Mathf.RoundToInt(condition.scalar) * perScalar;
                float before = power.BaseCompletionRaw + power.BaseCompletionAdd;
                power.BaseCompletionAdd += add;
                power.Trace?.RecordAdd(CalcStage.BaseCompletion, "기본 완성도", SourceName(owner), before, add);
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "기본 완성도 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"기본 완성도 <color=#FFE066>+{add}</color>";
        }
    }
}
