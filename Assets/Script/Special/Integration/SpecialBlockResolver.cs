using System.Collections.Generic;
using Special.Data;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// 그룹 확정 직전에 MultiPrimary 특수 블럭의 colorID 를 현재 클러스터의 최다 색상으로 재할당.
    /// OffPalette 는 그룹에 참여하지 않거나(Role=Independent) 별도 ID(>=100) 로 구분되어 여기 로직 밖.
    /// </summary>
    public static class SpecialBlockResolver
    {
        /// <summary>
        /// cluster 에 속한 (arrayIdx) 좌표들에 대해 board[x,y].attribute.specialDef 가 MultiPrimary 면
        /// 해당 cell 의 colorID 를 주변 최다 색으로 교체한다.
        /// </summary>
        public static void ResolveGroupColors(List<Vector2Int> cluster, BlockData[,] board)
        {
            Dictionary<int, int> primaryCounts = new Dictionary<int, int>();
            List<BlockData> multiPrimaryCells = new List<BlockData>();

            foreach (Vector2Int pos in cluster)
            {
                BlockData cell = board[pos.x, pos.y];
                if (cell == null) continue;

                SpecialBlockDefinition def = cell.attribute.specialDef;
                if (def != null && def.colorBinding == SpecialColorBinding.MultiPrimary)
                {
                    multiPrimaryCells.Add(cell);
                    continue;
                }

                int c = cell.attribute.colorID;
                if (c <= 0 || c > 3) continue;
                primaryCounts[c] = primaryCounts.TryGetValue(c, out int n) ? n + 1 : 1;
            }

            if (multiPrimaryCells.Count == 0) return;

            int dominant = 0;
            int dominantCount = -1;
            foreach (var kv in primaryCounts)
            {
                if (kv.Value > dominantCount) { dominant = kv.Key; dominantCount = kv.Value; }
            }

            if (dominant == 0)
            {
                SpecialBlockDefinition firstDef = multiPrimaryCells[0].attribute.specialDef;
                if ((firstDef.includedPrimaries & ColorSet.Red) != 0) dominant = 1;
                else if ((firstDef.includedPrimaries & ColorSet.Blue) != 0) dominant = 2;
                else if ((firstDef.includedPrimaries & ColorSet.Yellow) != 0) dominant = 3;
                else dominant = 1;
            }

            foreach (BlockData cell in multiPrimaryCells)
            {
                SpecialBlockDefinition def = cell.attribute.specialDef;
                int resolved = PickAllowedColor(def.includedPrimaries, dominant);
                cell.attribute.colorID = resolved;
            }
        }

        private static int PickAllowedColor(ColorSet allowed, int desired)
        {
            if (desired == 1 && (allowed & ColorSet.Red) != 0) return 1;
            if (desired == 2 && (allowed & ColorSet.Blue) != 0) return 2;
            if (desired == 3 && (allowed & ColorSet.Yellow) != 0) return 3;
            if ((allowed & ColorSet.Red) != 0) return 1;
            if ((allowed & ColorSet.Blue) != 0) return 2;
            if ((allowed & ColorSet.Yellow) != 0) return 3;
            return 0;
        }
    }
}
