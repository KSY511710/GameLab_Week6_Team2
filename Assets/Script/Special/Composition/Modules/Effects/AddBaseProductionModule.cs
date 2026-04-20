using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 a) PowerCalculationContext.BaseProductionAdd 에 (condition.scalar * perScalar) 가산.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Base Production")]
    public class AddBaseProductionModule : EffectModule
    {
        [Tooltip("condition.scalar 1 단위당 가산할 기본 생산량.")]
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                int add = Mathf.RoundToInt(condition.scalar) * perScalar;
                float before = power.BaseProductionRaw + power.BaseProductionAdd;
                power.BaseProductionAdd += add;
                power.Trace?.RecordAdd(CalcStage.Base, "기본 생산량", SourceName(owner), before, add);
            }
        }

        /// <summary>PowerPlant role 라이브 파워에도 동등 기여 — 솔로 그룹의 자체 생산으로 보임.</summary>
        public override float EstimateLivePower(SpecialBlockInstance owner, ConditionResult condition)
            => Mathf.RoundToInt(condition.scalar) * perScalar;

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "기본 생산량 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"기본 생산량 <color=#FFE066>+{add}</color>";
        }
    }
}
