using System.Collections.Generic;
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

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);
            int plants = CountPlantsInScope(owner);
            int bonus = plants * amountPerPlant;

            preview.steps.Add($"<size=20>· 범위 내 발전소 : <color=#FFE066>{plants} 개</color></size>");
            preview.steps.Add($"<size=20>· 가산량 : {plants} × {amountPerPlant} = <color=#FFE066>+{bonus}</color> 기본생산</size>");

            preview.impactCells = CollectAffectedClusterCells(owner);
            return preview;
        }

        private GroupInfo BuildTransientGroupInfo(PowerCalculationContext ctx)
        {
            return new GroupInfo { clusterPositions = new List<Vector2Int>(ctx.ClusterPositions) };
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

        private List<Vector2Int> CollectAffectedClusterCells(SpecialBlockInstance owner)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (PowerManager.Instance == null) return cells;
            foreach (GroupInfo g in PowerManager.Instance.activeGroups)
            {
                if (!ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, g)) continue;
                if (g.clusterPositions == null) continue;
                cells.AddRange(g.clusterPositions);
            }
            return cells;
        }
    }
}
