using System;
using System.Collections.Generic;
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
        private readonly List<Hook<Action>> dailySettleHooks = new();
        private readonly List<Hook<Action>> continuousHooks = new();

        // ============ Registration ============

        public void HookPowerCalculation(SpecialBlockInstance owner, Action<PowerCalculationContext> cb)
            => powerHooks.Add(new Hook<Action<PowerCalculationContext>> { Owner = owner, Callback = cb });

        public void HookOnGroupFormed(SpecialBlockInstance owner, Action<GroupInfo> cb)
            => groupFormedHooks.Add(new Hook<Action<GroupInfo>> { Owner = owner, Callback = cb });

        public void HookDailySettle(SpecialBlockInstance owner, Action cb)
            => dailySettleHooks.Add(new Hook<Action> { Owner = owner, Callback = cb });

        public void HookContinuous(SpecialBlockInstance owner, Action cb)
            => continuousHooks.Add(new Hook<Action> { Owner = owner, Callback = cb });

        /// <summary>Deactivate 시 호출. owner 의 모든 훅을 일괄 제거.</summary>
        public void UnhookAll(SpecialBlockInstance owner)
        {
            powerHooks.RemoveAll(h => h.Owner == owner);
            groupFormedHooks.RemoveAll(h => h.Owner == owner);
            dailySettleHooks.RemoveAll(h => h.Owner == owner);
            continuousHooks.RemoveAll(h => h.Owner == owner);
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
    }
}
