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
    }
}
