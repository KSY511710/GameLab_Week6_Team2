using Special.Runtime;
using UnityEngine;

namespace Special.Effects.Assets
{
    /// <summary>
    /// 예시 a) 범위 내 발전소 기본 생산량에
    /// (범위 내 발전소 수) * amountPerPlant 를 더한다.
    /// PowerCalculationContext 훅을 사용 — 정산 시마다 재평가.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Add Base Production")]
    public class AddBaseProductionEffect : EffectAsset
    {
        [Min(0)] public int amountPerPlant = 1;

        public override void Activate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            runtime.HookPowerCalculation(owner, ctx =>
            {
                GroupInfo groupBeingCalculated = BuildTransientGroupInfo(ctx);
                if (!ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, groupBeingCalculated)) return;

                int plantsInScope = CountPlantsInScope(owner);
                ctx.BaseProductionAdd += plantsInScope * amountPerPlant;
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime) { /* UnhookAll 이 정리 */ }

        private GroupInfo BuildTransientGroupInfo(PowerCalculationContext ctx)
        {
            return new GroupInfo { clusterPositions = new System.Collections.Generic.List<Vector2Int>(ctx.ClusterPositions) };
        }

        private int CountPlantsInScope(SpecialBlockInstance owner)
        {
            if (PowerManager.Instance == null) return 0;
            int count = 0;
            foreach (GroupInfo g in PowerManager.Instance.activeGroups)
            {
                if (ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, g)) count++;
            }
            return count;
        }
    }
}
