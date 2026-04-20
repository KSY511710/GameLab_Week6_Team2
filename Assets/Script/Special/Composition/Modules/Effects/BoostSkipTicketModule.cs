using Special.Composition.Contexts;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 j) SkipSettleContext.BonusTicketsPerDay 를 수정. ResourceManager.TrySkip 중 발화.
    /// mode=Add 면 기본 일당 티켓에 (condition.scalar * perScalar) 가산.
    /// mode=Mul 이면 기존 값에 multiplier 곱.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Boost Skip Ticket")]
    public class BoostSkipTicketModule : EffectModule
    {
        public enum ModifyMode { Add, Mul }
        public ModifyMode mode = ModifyMode.Add;

        [Min(0)] public int perScalar = 1;
        [Min(0f)] public float multiplier = 2f;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnSkipSettle;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is SkipSettleContext skip)
            {
                switch (mode)
                {
                    case ModifyMode.Add:
                        skip.BonusTicketsPerDay += Mathf.RoundToInt(condition.scalar) * perScalar;
                        break;
                    case ModifyMode.Mul:
                        skip.BonusTicketsPerDay = Mathf.RoundToInt(skip.BonusTicketsPerDay * multiplier);
                        break;
                }
            }
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "스킵 티켓 <color=#888888>효과 미발동</color>";
            if (mode == ModifyMode.Add)
            {
                int add = Mathf.RoundToInt(condition.scalar) * perScalar;
                return $"스킵 티켓 <color=#FFE066>+{add}/일</color>";
            }
            return $"스킵 티켓 <color=#66D9FF>×{multiplier:0.##}</color>";
        }
    }
}
