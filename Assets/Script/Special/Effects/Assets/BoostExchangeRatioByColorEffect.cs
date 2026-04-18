using Special.Runtime;
using UnityEngine;

namespace Special.Effects.Assets
{
    /// <summary>
    /// 예시 c) 구역 내 targetColorID 색 발전소 비율이 threshold 이상이면
    /// 해당 구역의 targetColorID 발전소 finalPower 를 multiplier 만큼 배율 적용.
    /// ResourceManager 의 exchangeRatio 는 전역 상수이므로, 여기선 "색별 출력 배율" 로 근사.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Boost Exchange Ratio By Color")]
    public class BoostExchangeRatioByColorEffect : EffectAsset
    {
        [Tooltip("1=Red, 2=Blue, 3=Yellow")] public int targetColorID = 1;
        [Range(0f, 1f)] public float threshold = 0.5f;
        [Min(1f)] public float multiplier = 1.5f;

        public override void Activate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            runtime.HookPowerCalculation(owner, ctx =>
            {
                if (PowerManager.Instance == null) return;

                GroupInfo probe = new GroupInfo { clusterPositions = new System.Collections.Generic.List<Vector2Int>(ctx.ClusterPositions) };
                if (!ScopeEvaluator.GroupInZone(owner, probe)) return;

                if (!IsColorDominantInZone(owner)) return;

                if (DominantColorOfCluster(ctx.ClusterPositions) == targetColorID)
                {
                    ctx.FinalMultiplier *= multiplier;
                }
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime) { }

        private bool IsColorDominantInZone(SpecialBlockInstance owner)
        {
            int total = 0;
            int match = 0;
            foreach (GroupInfo g in PowerManager.Instance.activeGroups)
            {
                if (!ScopeEvaluator.GroupInZone(owner, g)) continue;
                total++;
                if (g.finalColor == targetColorID) match++;
            }
            if (total == 0) return false;
            return (float)match / total >= threshold;
        }

        private int DominantColorOfCluster(System.Collections.Generic.IReadOnlyList<Vector2Int> cluster)
        {
            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null) return 0;
            System.Collections.Generic.Dictionary<int, int> counts = new System.Collections.Generic.Dictionary<int, int>();
            for (int i = 0; i < cluster.Count; i++)
            {
                BlockData cell = grid.GetBlockAtArrayIndex(cluster[i]);
                if (cell == null) continue;
                int c = cell.attribute.colorID;
                counts[c] = counts.TryGetValue(c, out int n) ? n + 1 : 1;
            }
            int dom = 0, domC = -1;
            foreach (var kv in counts) if (kv.Value > domC) { dom = kv.Key; domC = kv.Value; }
            return dom;
        }
    }
}
