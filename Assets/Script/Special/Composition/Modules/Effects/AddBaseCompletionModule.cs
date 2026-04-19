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
                power.BaseCompletionAdd += Mathf.RoundToInt(condition.scalar) * perScalar;
            }
        }
    }
}
