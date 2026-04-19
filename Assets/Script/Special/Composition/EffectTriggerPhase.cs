namespace Special.Composition
{
    /// <summary>
    /// EffectModule 이 어떤 시점/훅에 동작할지 지정한다.
    /// CompositeEffectAsset.Activate 가 이 값을 보고 EffectRuntime 의 적절한 훅에 콜백을 등록한다.
    /// </summary>
    public enum EffectTriggerPhase
    {
        OnPowerCalculation,   // PowerCalculationContext (그룹별 전력 계산)
        OnGroupFormed,        // 새 그룹 형성 직후
        OnProductionSettle,   // 일일 정산 직전 (특수 기여분 제출 시점)
        OnDailySettle,        // 일일 정산 완료 후
        OnContinuous,         // 매 프레임
        OnSkipSettle,         // 스킵 정산 시 (티켓 보너스 등)
        OnBlockPlacedColor,   // 블럭 설치 직후 색 오버라이드
        OnProductionCount,    // 정산 시 발전소별 생산 반복 횟수 결정
        OnTicketProduction    // 일일 티켓 생산 (ProcessNextDay 시점)
    }
}
