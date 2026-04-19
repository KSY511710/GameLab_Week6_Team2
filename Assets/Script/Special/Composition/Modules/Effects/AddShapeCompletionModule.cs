using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 d) PowerCalculationContext.ShapeCompletionAdd 에 가산.
    /// 모양 완성도(shapeBonus) 쪽을 증폭한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Shape Completion")]
    public class AddShapeCompletionModule : EffectModule
    {
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                power.ShapeCompletionAdd += Mathf.RoundToInt(condition.scalar) * perScalar;
            }
        }
    }
}
