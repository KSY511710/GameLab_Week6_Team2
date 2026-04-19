namespace Special.Composition.Contexts
{
    /// <summary>
    /// ResourceManager.TrySkip 의 "보상 계산 직전" 에 발화.
    /// SkippedDays = 이번 스킵으로 건너뛸 일수 (read-only).
    /// BonusTicketsPerDay = 일당 지급 티켓 수. 기본값 = ResourceManager.ticketsPerSkippedDay (기본 1).
    ///   각 EffectModule 이 가산/대체하면 최종 보상 = BonusTicketsPerDay * SkippedDays 로 적용된다.
    /// </summary>
    public class SkipSettleContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnSkipSettle;
        public int SkippedDays;
        public int BonusTicketsPerDay;
    }
}
