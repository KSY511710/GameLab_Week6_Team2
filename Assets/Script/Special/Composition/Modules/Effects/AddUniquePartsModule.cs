using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 b) PowerCalculationContext.UniquePartsAdd 에 (condition.scalar * perScalar) 가산.
    /// "모양이 다를수록 가산" 류 효과.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Unique Parts")]
    public class AddUniquePartsModule : EffectModule
    {
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                int add = Mathf.RoundToInt(condition.scalar) * perScalar;
                float before = power.UniquePartsRaw + power.UniquePartsAdd;
                power.UniquePartsAdd += add;
                power.Trace?.RecordAdd(CalcStage.UniqueParts, "부품 종류", SourceName(owner), before, add);
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "부품 종류 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"부품 종류 <color=#FFE066>+{add}</color>";
        }
    }
}
