using System.Collections.Generic;
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

                if (!ScopeEvaluator.ClusterInZone(owner, ctx.ClusterPositions)) return;

                if (!IsColorDominantInZone(owner)) return;

                if (DominantColorOfCluster(ctx.ClusterPositions) == targetColorID)
                {
                    ctx.FinalMultiplier *= multiplier;
                }
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            /* EffectRuntime.UnhookAll(owner)가 SpecialBlockRegistry.DeactivateEffects에서 호출되며 정리됨. */
        }

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);

            int total = 0;
            int match = 0;
            List<Vector2Int> impact = new List<Vector2Int>();
            if (PowerManager.Instance != null)
            {
                foreach (GroupInfo g in PowerManager.Instance.activeGroups)
                {
                    if (!ScopeEvaluator.GroupInZone(owner, g)) continue;
                    total++;
                    if (g.finalColor == targetColorID)
                    {
                        match++;
                        if (g.clusterPositions != null) impact.AddRange(g.clusterPositions);
                    }
                }
            }

            float ratio = total > 0 ? (float)match / total : 0f;
            bool active = ratio >= threshold;
            string colorName = ColorIdToName(targetColorID);

            preview.steps.Add($"<size=20>· 같은 구역 내 발전소 : {total} 개 (그 중 {colorName} {match} 개)</size>");
            preview.steps.Add($"<size=20>· 점유율 : <color=#FFE066>{ratio * 100f:F0}%</color> / 기준 {threshold * 100f:F0}%</size>");
            preview.steps.Add(active
                ? $"<size=20>→ 조건 충족, {colorName} 발전소 출력에 <color=#FFE066>x{multiplier:F2}</color> 적용 대기</size>"
                : $"<size=20>→ 조건 미달, 효과 대기 중</size>");

            preview.impactCells = impact;
            return preview;
        }

        private bool IsColorDominantInZone(SpecialBlockInstance owner)
        {
            if (PowerManager.Instance == null) return false;

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

        private int DominantColorOfCluster(IReadOnlyList<Vector2Int> cluster)
        {
            // teardown 중 grid null 허용.
            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null) return 0;
            Dictionary<int, int> counts = new Dictionary<int, int>();
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

        private static string ColorIdToName(int id)
        {
            switch (id)
            {
                case 1: return "<color=#FF6666>빨</color>";
                case 2: return "<color=#6699FF>파</color>";
                case 3: return "<color=#66CC66>노</color>";
                default: return "?";
            }
        }
    }
}
