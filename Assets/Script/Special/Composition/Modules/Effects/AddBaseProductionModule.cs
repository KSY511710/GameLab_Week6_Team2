using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 a) PowerCalculationContext.BaseProductionAdd 에 (condition.scalar * perScalar) 가산.
    /// 레거시 AddBaseProductionEffect 동등 재현 (perScalar = amountPerPlant).
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
                power.BaseProductionAdd += add;
            }
        }

        /// <summary>PowerPlant role 라이브 파워에도 동등 기여 — 솔로 그룹의 자체 생산으로 보임.</summary>
        public override float EstimateLivePower(SpecialBlockInstance owner, ConditionResult condition)
            => Mathf.RoundToInt(condition.scalar) * perScalar;
    }
}
