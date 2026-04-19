using Special.Composition.Contexts;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 k) TicketSettleContext.BonusTickets 에 가산. ResourceManager.ProcessNextDay 직후 발화.
    /// 일일 정산 시점에 티켓 추가 지급. amount = condition.scalar * perScalar.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Ticket Bonus")]
    public class AddTicketBonusModule : EffectModule
    {
        [Min(0)] public int perScalar = 1;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnTicketProduction;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is TicketSettleContext ticket)
            {
                ticket.BonusTickets += Mathf.RoundToInt(condition.scalar) * perScalar;
            }
        }
    }
}
