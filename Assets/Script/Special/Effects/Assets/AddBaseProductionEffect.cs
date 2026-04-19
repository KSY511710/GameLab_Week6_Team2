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
                if (!ScopeEvaluator.ClusterMatches(owner, scope, rangeInCells, ctx.ClusterPositions)) return;

                var (plantsInScope, _) = CollectAffectedGroups(owner);
                ctx.BaseProductionAdd += plantsInScope * amountPerPlant;
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            /* EffectRuntime.UnhookAll(owner)가 SpecialBlockRegistry.DeactivateEffects에서 호출되며 정리됨. */
        }

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);

            var (plants, cells) = CollectAffectedGroups(owner);
            int bonus = plants * amountPerPlant;

            preview.steps.Add($"<size=20>· 범위 내 발전소 : <color=#FFE066>{plants} 개</color></size>");
            preview.steps.Add($"<size=20>· 가산량 : {plants} × {amountPerPlant} = <color=#FFE066>+{bonus}</color> 기본생산</size>");

            preview.impactCells = cells;
            return preview;
        }

        /// <summary>
        /// scope 에 해당하는 활성 발전소 수와 해당 발전소 셀들을 한 번의 순회로 수집한다.
        /// 훅 경로와 BuildPreview 경로 모두 이걸 공유.
        /// </summary>
        private (int count, List<Vector2Int> cells) CollectAffectedGroups(SpecialBlockInstance owner)
        {
            int count = 0;
            List<Vector2Int> cells = new List<Vector2Int>();
            if (PowerManager.Instance == null) return (0, cells);
            foreach (GroupInfo g in PowerManager.Instance.activeGroups)
            {
                if (!ScopeEvaluator.GroupMatches(owner, scope, rangeInCells, g)) continue;
                count++;
                if (g.clusterPositions != null) cells.AddRange(g.clusterPositions);
            }
            return (count, cells);
        }
    }
}
