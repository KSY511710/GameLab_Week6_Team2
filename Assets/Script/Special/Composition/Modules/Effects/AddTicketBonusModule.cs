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

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "보너스 티켓 <color=#888888>효과 미발동</color>";
            int add = Mathf.RoundToInt(condition.scalar) * perScalar;
            return $"보너스 티켓 <color=#FFE066>+{add}</color>";
        }
    }
}
