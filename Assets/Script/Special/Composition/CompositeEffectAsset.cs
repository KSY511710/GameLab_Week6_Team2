using System.Collections.Generic;
using Special.Composition.Contexts;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition
{
    /// <summary>
    /// Scope × Conditions × Effects 의 3축 조합으로 정의되는 특수 블럭 효과.
    /// 기존 EffectAsset 을 상속하므로 SpecialBlockDefinition.effectAssets[] 슬롯에 그대로 들어가
    /// SpecialBlockRegistry / 가챠 / UI 코드는 한 줄도 수정할 필요가 없다.
    ///
    /// 신규 효과 추가 = 본 에셋 인스턴스 + ConditionModule SO + EffectModule SO 를 인스펙터에서 조립.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Composite Effect", fileName = "Composite_")]
    public class CompositeEffectAsset : EffectAsset
    {
        [Header("Composition")]
        [Tooltip("AND 결합. 모든 조건이 passed=true 여야 효과들이 적용된다. scalar 는 곱셈 누적.")]
        public List<ConditionModule> conditions = new List<ConditionModule>();
        [Tooltip("적용할 효과(값 수정자) 목록. 각 효과의 Phase 가 등록될 훅을 결정한다.")]
        public List<EffectModule> effects = new List<EffectModule>();

        public override void Activate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            if (owner == null || runtime == null || effects == null) return;

            // Phase 별로 묶어서 등록 (같은 phase 의 effects 는 한 콜백에서 일괄 처리)
            Dictionary<EffectTriggerPhase, List<EffectModule>> byPhase = new Dictionary<EffectTriggerPhase, List<EffectModule>>();
            for (int i = 0; i < effects.Count; i++)
            {
                EffectModule eff = effects[i];
                if (eff == null) continue;
                if (!byPhase.TryGetValue(eff.Phase, out List<EffectModule> list))
                {
                    list = new List<EffectModule>();
                    byPhase[eff.Phase] = list;
                }
                list.Add(eff);
            }

            foreach (KeyValuePair<EffectTriggerPhase, List<EffectModule>> kv in byPhase)
            {
                RegisterPhase(owner, runtime, kv.Key, kv.Value);
            }
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            // SpecialBlockRegistry 가 UnhookAll(owner) 로 일괄 해제하므로 여기서 별도 처리 불필요.
        }

        /// <summary>PowerPlant role 의 라이브 파워 합산. 모든 effect 의 EstimateLivePower 를 누적.</summary>
        public override float EstimateLivePower(SpecialBlockInstance owner)
        {
            if (effects == null) return 0f;
            ConditionResult cond = EvaluateAllConditions(owner);
            if (!cond.passed) return 0f;
            float total = 0f;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null) continue;
                total += effects[i].EstimateLivePower(owner, cond);
            }
            return total;
        }

        // =========================================================
        // Phase → EffectRuntime 훅 등록
        // =========================================================

        private void RegisterPhase(SpecialBlockInstance owner, EffectRuntime runtime, EffectTriggerPhase phase, List<EffectModule> phaseEffects)
        {
            switch (phase)
            {
                case EffectTriggerPhase.OnPowerCalculation:
                    runtime.HookPowerCalculation(owner, ctx =>
                    {
                        if (!ScopeEvaluator.ClusterMatches(owner, scope, rangeInCells, ctx.ClusterPositions)) return;
                        Dispatch(owner, phaseEffects, ctx);
                    });
                    break;

                case EffectTriggerPhase.OnGroupFormed:
                    runtime.HookOnGroupFormed(owner, group =>
                    {
                        if (!ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, group)) return;
                        // GroupInfo 자체는 IEffectContext 가 아니므로 가벼운 어댑터로 감싼다.
                        Dispatch(owner, phaseEffects, new GroupFormedContext { Group = group });
                    });
                    break;

                case EffectTriggerPhase.OnProductionSettle:
                    runtime.HookProductionSettle(owner, () => Dispatch(owner, phaseEffects, new ProductionSettleContext()));
                    break;

                case EffectTriggerPhase.OnDailySettle:
                    runtime.HookDailySettle(owner, () => Dispatch(owner, phaseEffects, new DailySettleContext()));
                    break;

                case EffectTriggerPhase.OnContinuous:
                    runtime.HookContinuous(owner, () => Dispatch(owner, phaseEffects, new ContinuousContext()));
                    break;

                case EffectTriggerPhase.OnTicketProduction:
                    runtime.HookTicketProduction(owner, ctx => Dispatch(owner, phaseEffects, ctx));
                    break;

                case EffectTriggerPhase.OnSkipSettle:
                    runtime.HookSkipSettle(owner, ctx => Dispatch(owner, phaseEffects, ctx));
                    break;

                case EffectTriggerPhase.OnBlockPlacedColor:
                    runtime.HookColorOverride(owner, ctx => Dispatch(owner, phaseEffects, ctx));
                    break;

                case EffectTriggerPhase.OnProductionCount:
                    runtime.HookProductionCount(owner, ctx =>
                    {
                        if (!ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, ctx.Group)) return;
                        Dispatch(owner, phaseEffects, ctx);
                    });
                    break;

                default:
                    Debug.LogWarning($"[CompositeEffectAsset] Phase {phase} 는 아직 등록 핸들러가 없습니다. ({name})");
                    break;
            }
        }

        // =========================================================
        // Condition 결합 + Effect Apply
        // =========================================================

        private void Dispatch(SpecialBlockInstance owner, List<EffectModule> phaseEffects, IEffectContext ctx)
        {
            ConditionResult cond = EvaluateAllConditions(owner);
            if (!cond.passed) return;
            for (int i = 0; i < phaseEffects.Count; i++)
            {
                if (phaseEffects[i] == null) continue;
                try { phaseEffects[i].Apply(owner, cond, ctx); }
                catch (System.Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>모든 조건을 AND 로 결합. scalar 는 곱셈 누적, targets 는 첫 non-null 채택.</summary>
        private ConditionResult EvaluateAllConditions(SpecialBlockInstance owner)
        {
            ConditionResult agg = ConditionResult.Pass(1f);
            if (conditions == null) return agg;

            for (int i = 0; i < conditions.Count; i++)
            {
                ConditionModule c = conditions[i];
                if (c == null) continue;
                ConditionResult r = c.Evaluate(owner, scope, rangeInCells);
                if (!r.passed) return ConditionResult.Fail();
                agg.scalar *= r.scalar;
                if (agg.targets == null && r.targets != null) agg.targets = r.targets;
            }
            return agg;
        }

        // =========================================================
        // Preview (시퀀서 표시)
        // =========================================================

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    if (conditions[i] != null) preview.steps.Add($"· {conditions[i].name}");
                }
            }
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    if (effects[i] != null) preview.steps.Add($"→ {effects[i].name}");
                }
            }
            return preview;
        }
    }

    // =========================================================
    // 가벼운 컨텍스트 어댑터들 (Phase 1 에서만 필요한 것들)
    // 신규 phase 컨텍스트는 Composition/Contexts/ 에 별도 파일로 존재.
    // =========================================================

    public class GroupFormedContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnGroupFormed;
        public GroupInfo Group;
    }

    public class ProductionSettleContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnProductionSettle;
    }

    public class DailySettleContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnDailySettle;
    }

    public class ContinuousContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnContinuous;
    }
}
