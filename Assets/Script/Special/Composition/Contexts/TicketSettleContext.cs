namespace Special.Composition.Contexts
{
    /// <summary>
    /// ResourceManager.ProcessNextDay 시점에 발화되는 컨텍스트.
    /// 등록된 EffectModule 들이 BonusTickets 에 가산하여 일일 티켓을 추가 지급한다.
    /// 기본값 0. 훅이 없거나 모두 0 가산이면 게임 동작은 기존과 동일.
    /// </summary>
    public class TicketSettleContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnTicketProduction;
        public int BonusTickets;
    }
}
