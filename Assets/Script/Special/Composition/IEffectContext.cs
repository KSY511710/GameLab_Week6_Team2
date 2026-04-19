namespace Special.Composition
{
    /// <summary>
    /// EffectModule.Apply 가 받는 컨텍스트의 마커 인터페이스.
    /// 구체 타입(PowerCalculationContext, TicketSettleContext 등)은 Phase 별로 다르며,
    /// 각 모듈이 자신이 처리하는 Phase 의 컨텍스트로 다운캐스트해 사용한다.
    /// </summary>
    public interface IEffectContext
    {
        EffectTriggerPhase Phase { get; }
    }
}
