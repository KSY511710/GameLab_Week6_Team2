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
                int add = Mathf.RoundToInt(condition.scalar) * perScalar;
                float before = power.ShapeCompletionRaw + power.ShapeCompletionAdd;
                power.ShapeCompletionAdd += add;
                power.Trace?.RecordAdd(CalcStage.ShapeCompletion, "모양 완성도", SourceName(owner), before, add);
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "모양 완성도 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"모양 완성도 <color=#FFE066>+{add}</color>";
        }
    }
}
