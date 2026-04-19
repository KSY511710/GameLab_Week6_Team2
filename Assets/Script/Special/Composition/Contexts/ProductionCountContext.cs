namespace Special.Composition.Contexts
{
    /// <summary>
    /// PowerManager.BuildSettlementData 의 그룹 순회에서 그룹별로 1회 발화.
    /// EffectModule 이 ExtraRepeatCount 에 가산하면 최종 effective 전력 = group.groupPower * (1 + ExtraRepeatCount).
    /// 기본값 0 → 훅 없으면 1배(기존 동작).
    /// </summary>
    public class ProductionCountContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnProductionCount;
        public GroupInfo Group;
        public int ExtraRepeatCount;
    }
}
