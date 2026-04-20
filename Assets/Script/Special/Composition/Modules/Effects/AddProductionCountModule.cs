using Special.Composition.Contexts;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 g) ProductionCountContext.ExtraRepeatCount 에 가산.
    /// 최종 effective 전력 = group.groupPower * (1 + ExtraRepeatCount).
    /// 예: 빨강 과반 조건 + 이 효과 → 해당 그룹 생산량 ×2.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Production Count")]
    public class AddProductionCountModule : EffectModule
    {
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnProductionCount;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is ProductionCountContext pc)
            {
                pc.ExtraRepeatCount += Mathf.RoundToInt(condition.scalar) * perScalar;
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "생산 횟수 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"생산 횟수 <color=#FFD35A>+{add}</color>";
        }
    }
}
