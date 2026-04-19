using System;
using System.Collections.Generic;
using Special.Composition.Contexts;
using Special.Effects;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 모든 IEffect 인스턴스가 훅을 구독/해제하는 중앙 디스패처.
    /// 효과는 Activate 시 Hook* 을 호출해 콜백을 등록하고, Deactivate 시 자동 해제된다.
    /// 이 클래스는 MonoBehaviour 가 아니라 프로세스 단일 인스턴스이며, 씬 독립적으로 살아남는다.
    /// </summary>
    public class EffectRuntime
    {
        public static EffectRuntime Instance { get; } = new EffectRuntime();

        private class Hook<T>
        {
            public SpecialBlockInstance Owner;
            public T Callback;
        }

        private readonly List<Hook<Action<PowerCalculationContext>>> powerHooks = new();
        private readonly List<Hook<Action<GroupInfo>>> groupFormedHooks = new();
        private readonly List<Hook<Action>> productionSettleHooks = new();
        private readonly List<Hook<Action>> dailySettleHooks = new();
        private readonly List<Hook<Action>> continuousHooks = new();
        private readonly List<Hook<Action<TicketSettleContext>>> ticketHooks = new();
        private readonly List<Hook<Action<SkipSettleContext>>> skipHooks = new();
        private readonly List<Hook<Action<ColorOverrideContext>>> colorOverrideHooks = new();
        private readonly List<Hook<Action<ProductionCountContext>>> productionCountHooks = new();

        // ============ Registration ============

        public void HookPowerCalculation(SpecialBlockInstance owner, Action<PowerCalculationContext> cb)
            => powerHooks.Add(new Hook<Action<PowerCalculationContext>> { Owner = owner, Callback = cb });

        public void HookOnGroupFormed(SpecialBlockInstance owner, Action<GroupInfo> cb)
            => groupFormedHooks.Add(new Hook<Action<GroupInfo>> { Owner = owner, Callback = cb });

        /// <summary>
        /// 일일 정산 애니메이션 '직전' 에 발화. 효과는 여기서
        /// PowerManager.SubmitSpecialContribution 을 호출해 자신의 일일 생산량을 등록한다.
        /// DailySettle 과 달리 이 단계의 결과는 SettlementUIController 의 막대그래프에 반영된다.
        /// </summary>
        public void HookProductionSettle(SpecialBlockInstance owner, Action cb)
            => productionSettleHooks.Add(new Hook<Action> { Owner = owner, Callback = cb });

        public void HookDailySettle(SpecialBlockInstance owner, Action cb)
            => dailySettleHooks.Add(new Hook<Action> { Owner = owner, Callback = cb });

        public void HookContinuous(SpecialBlockInstance owner, Action cb)
            => continuousHooks.Add(new Hook<Action> { Owner = owner, Callback = cb });

        /// <summary>기획 효과 k) 일일 정산 후 보너스 티켓 지급. ResourceManager.ProcessNextDay 직후 발화.</summary>
        public void HookTicketProduction(SpecialBlockInstance owner, Action<TicketSettleContext> cb)
            => ticketHooks.Add(new Hook<Action<TicketSettleContext>> { Owner = owner, Callback = cb });

        /// <summary>기획 효과 j) 스킵 정산 시 일수당 티켓 보너스 수정. ResourceManager.TrySkip 중 발화.</summary>
        public void HookSkipSettle(SpecialBlockInstance owner, Action<SkipSettleContext> cb)
            => skipHooks.Add(new Hook<Action<SkipSettleContext>> { Owner = owner, Callback = cb });

        /// <summary>기획 효과 h/i) 블록 설치 시 특정 셀 색을 강제 변경. GridManager.PlaceShape 직후 발화.</summary>
        public void HookColorOverride(SpecialBlockInstance owner, Action<ColorOverrideContext> cb)
            => colorOverrideHooks.Add(new Hook<Action<ColorOverrideContext>> { Owner = owner, Callback = cb });

        /// <summary>기획 효과 g) 그룹 생산 횟수 증가. PowerManager.BuildSettlementData 그룹 순회 중 발화.</summary>
        public void HookProductionCount(SpecialBlockInstance owner, Action<ProductionCountContext> cb)
            => productionCountHooks.Add(new Hook<Action<ProductionCountContext>> { Owner = owner, Callback = cb });

        /// <summary>Deactivate 시 호출. owner 의 모든 훅을 일괄 제거.</summary>
        public void UnhookAll(SpecialBlockInstance owner)
        {
            powerHooks.RemoveAll(h => h.Owner == owner);
            groupFormedHooks.RemoveAll(h => h.Owner == owner);
            productionSettleHooks.RemoveAll(h => h.Owner == owner);
            dailySettleHooks.RemoveAll(h => h.Owner == owner);
            continuousHooks.RemoveAll(h => h.Owner == owner);
            ticketHooks.RemoveAll(h => h.Owner == owner);
            skipHooks.RemoveAll(h => h.Owner == owner);
            colorOverrideHooks.RemoveAll(h => h.Owner == owner);
            productionCountHooks.RemoveAll(h => h.Owner == owner);
        }

        // ============ Dispatch (called by PowerManager / ResourceManager) ============

        public void ApplyPowerHooks(PowerCalculationContext ctx)
        {
            for (int i = 0; i < powerHooks.Count; i++)
            {
                try { powerHooks[i].Callback?.Invoke(ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void NotifyGroupFormed(GroupInfo group)
        {
            for (int i = 0; i < groupFormedHooks.Count; i++)
            {
                try { groupFormedHooks[i].Callback?.Invoke(group); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>
        /// PowerManager.ProceedToNextDay 가 SettlementData 를 빌드하기 직전에 호출.
        /// 이 시점에 등록된 모든 효과는 자신의 오늘자 생산 기여분을 PowerManager 에 제출해야 한다.
        /// </summary>
        public void NotifyProductionSettle()
        {
            for (int i = 0; i < productionSettleHooks.Count; i++)
            {
                try { productionSettleHooks[i].Callback?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void NotifyDailySettle()
        {
            for (int i = 0; i < dailySettleHooks.Count; i++)
            {
                try { dailySettleHooks[i].Callback?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void NotifyContinuous()
        {
            for (int i = 0; i < continuousHooks.Count; i++)
            {
                try { continuousHooks[i].Callback?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void ApplyTicketHooks(TicketSettleContext ctx)
        {
            for (int i = 0; i < ticketHooks.Count; i++)
            {
                try { ticketHooks[i].Callback?.Invoke(ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void ApplySkipHooks(SkipSettleContext ctx)
        {
            for (int i = 0; i < skipHooks.Count; i++)
            {
                try { skipHooks[i].Callback?.Invoke(ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void ApplyColorOverrideHooks(ColorOverrideContext ctx)
        {
            for (int i = 0; i < colorOverrideHooks.Count; i++)
            {
                try { colorOverrideHooks[i].Callback?.Invoke(ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public void ApplyProductionCountHooks(ProductionCountContext ctx)
        {
            for (int i = 0; i < productionCountHooks.Count; i++)
            {
                try { productionCountHooks[i].Callback?.Invoke(ctx); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
