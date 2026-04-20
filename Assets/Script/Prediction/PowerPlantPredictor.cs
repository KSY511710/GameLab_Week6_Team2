using System.Collections.Generic;
using System.Linq;
using Special.Composition;
using Special.Data;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Prediction
{
    /// <summary>
    /// 드래그 중인 블럭이 배치됐다면 만들어질 발전소 스펙을 "보드를 건드리지 않고" 계산한다.
    /// 경로는 PowerManager.CreateNewGroup 과 동일한 식을 쓴다. 같은 PowerCalculationContext /
    /// EffectRuntime.ApplyPowerHooks / FormationDetector 를 재사용해 라이브값과 바이트 단위로 일치.
    /// </summary>
    public static class PowerPlantPredictor
    {
        /// <summary>그룹 최소 크기. PowerManager 인스펙터 기본값과 동일.</summary>
        private const int GroupMinSize = 9;
        private const int GroupMinPart = 3;

        private static readonly Vector2Int[] Orthogonal =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        /// <summary>
        /// 드래그 중 예측. 실패 사유가 있으면 projection.blockedReason 에 메시지가 담긴다.
        /// </summary>
        /// <param name="grid">현재 GridManager.</param>
        /// <param name="anchorWorldCell">마우스 앵커 월드 타일 좌표.</param>
        /// <param name="shape">블럭 shape coords.</param>
        /// <param name="specialDef">특수 블럭 정의. 일반 블럭이면 null.</param>
        /// <param name="colorID">일반 블럭 색상 ID. 특수 블럭은 definition 에서 해석.</param>
        /// <param name="shapeID">일반 블럭 shape ID. 특수 블럭은 uniqueShapeId.</param>
        public static PowerPlantProjection Predict(GridManager grid, Vector3Int anchorWorldCell, Vector2Int[] shape, SpecialBlockDefinition specialDef, int colorID, int shapeID)
        {
            if (grid == null || shape == null || shape.Length == 0)
                return PowerPlantProjection.Blocked("입력 누락");

            if (!grid.CanPlaceShape(anchorWorldCell, shape, specialDef))
                return PowerPlantProjection.Blocked("여기에는 설치할 수 없습니다");

            int resolvedColorID = ResolveColorID(specialDef, colorID);
            int resolvedShapeID = ResolveShapeID(specialDef, shapeID);
            SpecialBlockRole role = specialDef != null ? specialDef.role : SpecialBlockRole.Grouping;

            Vector2Int anchorArray = grid.WorldCellToArrayIndex(anchorWorldCell);
            List<Vector2Int> virtualCells = BuildVirtualCells(grid, anchorArray, shape);

            float selfContribution = EstimateSelfContribution(specialDef, anchorArray, grid);

            bool participatesInGrouping = specialDef == null || role == SpecialBlockRole.Grouping;
            if (!participatesInGrouping)
            {
                return new PowerPlantProjection
                {
                    isFormed = false,
                    selfContributionPower = selfContribution,
                    currentBlockCount = virtualCells.Count,
                    currentUniquePartCount = resolvedShapeID > 0 ? 1 : 0,
                    clusterPositions = virtualCells,
                    dominantColor = resolvedColorID,
                    dominantRealColor = ColorIDToRealColor(resolvedColorID)
                };
            }

            List<Vector2Int> cluster = GrowClusterFromVirtual(grid, virtualCells);
            HashSet<int> uniqueShapes = new HashSet<int>();
            Dictionary<int, int> colorCounts = new Dictionary<int, int>();

            for (int i = 0; i < cluster.Count; i++)
            {
                Vector2Int p = cluster[i];
                int cellColor;
                int cellShape;
                if (IsVirtualCell(virtualCells, p))
                {
                    cellColor = resolvedColorID;
                    cellShape = resolvedShapeID;
                }
                else
                {
                    BlockData cell = grid.GetBlockAtArrayIndex(p);
                    if (cell == null) continue;
                    cellColor = cell.attribute.colorID;
                    cellShape = cell.attribute.shapeID;
                }

                if (cellShape > 0) uniqueShapes.Add(cellShape);
                if (cellColor > 0)
                {
                    colorCounts[cellColor] = colorCounts.TryGetValue(cellColor, out int n) ? n + 1 : 1;
                }
            }

            bool formed = cluster.Count >= GroupMinSize && uniqueShapes.Count >= GroupMinPart;

            if (!formed)
            {
                return new PowerPlantProjection
                {
                    isFormed = false,
                    selfContributionPower = selfContribution,
                    currentBlockCount = cluster.Count,
                    currentUniquePartCount = uniqueShapes.Count,
                    clusterPositions = cluster,
                    dominantColor = resolvedColorID,
                    dominantRealColor = ColorIDToRealColor(resolvedColorID)
                };
            }

            int baseProduction = cluster.Count;
            int uniquePartsCount = uniqueShapes.Count;
            int shapeBonus = Mathf.Max(0, FormationDetector.GetFormationMultiplier(cluster));

            int maxColorCount = colorCounts.Count > 0 ? colorCounts.Values.Max() : 0;
            int restColorCount = cluster.Count - maxColorCount;
            float colorMultiplier = 1f + (maxColorCount - restColorCount) * 0.2f;

            CalculationTrace trace = new CalculationTrace();
            PowerCalculationContext ctx = new PowerCalculationContext
            {
                BaseProductionRaw = baseProduction,
                UniquePartsRaw = uniquePartsCount,
                BaseCompletionRaw = 2,
                ShapeCompletionRaw = shapeBonus,
                ColorMultiplierRaw = colorMultiplier,
                ClusterPositions = cluster,
                Trace = trace
            };
            SeedTraceRawValues(ctx);
            EffectRuntime.Instance.ApplyPowerHooks(ctx);

            float finalPower = ctx.Compute();
            float baseRatio = ResourceManager.Instance != null ? ResourceManager.Instance.ExchangeRatio : 10f;
            if (baseRatio <= 0f) baseRatio = 1f;

            int dominantColor = 0;
            int dominantCount = -1;
            foreach (var kv in colorCounts)
            {
                if (kv.Value > dominantCount) { dominantColor = kv.Key; dominantCount = kv.Value; }
            }

            return new PowerPlantProjection
            {
                isFormed = true,
                blockSize = baseProduction,
                baseProduction = baseProduction,
                uniqueParts = uniquePartsCount,
                baseCompletion = 2,
                shapeCompletion = shapeBonus,
                colorMultiplier = colorMultiplier,
                finalMultiplier = ctx.FinalMultiplier,
                groupPower = finalPower,
                appliedExchangeRatio = baseRatio,
                estimatedMoneyGen = finalPower / baseRatio,
                dominantColor = dominantColor,
                dominantRealColor = ColorIDToRealColor(dominantColor),
                clusterPositions = cluster,
                trace = trace,
                selfContributionPower = selfContribution,
                currentBlockCount = baseProduction,
                currentUniquePartCount = uniquePartsCount
            };
        }

        private static List<Vector2Int> BuildVirtualCells(GridManager grid, Vector2Int anchorArray, Vector2Int[] shape)
        {
            List<Vector2Int> cells = new List<Vector2Int>(shape.Length);
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2Int p = anchorArray + shape[i];
                if (p.x < 0 || p.x >= grid.width || p.y < 0 || p.y >= grid.height) continue;
                cells.Add(p);
            }
            return cells;
        }

        /// <summary>
        /// 가상 셀들을 시드로 두고 직교 방향으로 BFS. 접근 가능한 cell 이
        /// IsEligibleForGrouping 이고 아직 그룹화되지 않았을 때만 확장한다. board 는 읽기 전용.
        /// </summary>
        private static List<Vector2Int> GrowClusterFromVirtual(GridManager grid, List<Vector2Int> virtualCells)
        {
            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            List<Vector2Int> result = new List<Vector2Int>();

            for (int i = 0; i < virtualCells.Count; i++)
            {
                Vector2Int v = virtualCells[i];
                if (seen.Add(v)) queue.Enqueue(v);
            }

            while (queue.Count > 0)
            {
                Vector2Int curr = queue.Dequeue();
                result.Add(curr);

                for (int d = 0; d < Orthogonal.Length; d++)
                {
                    Vector2Int n = curr + Orthogonal[d];
                    if (n.x < 0 || n.x >= grid.width || n.y < 0 || n.y >= grid.height) continue;
                    if (seen.Contains(n)) continue;

                    if (IsVirtualCell(virtualCells, n))
                    {
                        seen.Add(n);
                        queue.Enqueue(n);
                        continue;
                    }

                    BlockData cell = grid.GetBlockAtArrayIndex(n);
                    if (cell == null || cell.isGrouped) continue;
                    if (!IsEligibleForGrouping(cell)) continue;

                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
            return result;
        }

        private static bool IsVirtualCell(List<Vector2Int> virtualCells, Vector2Int p)
        {
            for (int i = 0; i < virtualCells.Count; i++)
                if (virtualCells[i] == p) return true;
            return false;
        }

        private static bool IsEligibleForGrouping(BlockData cell)
        {
            if (cell.attribute.colorID <= 0) return false;
            SpecialBlockDefinition def = cell.attribute.specialDef;
            if (def != null && def.role != SpecialBlockRole.Grouping) return false;
            return true;
        }

        private static int ResolveColorID(SpecialBlockDefinition def, int fallbackColorID)
        {
            if (def == null) return fallbackColorID;
            return def.colorBinding == SpecialColorBinding.Single ? def.ResolveSingleColorID() : 0;
        }

        private static int ResolveShapeID(SpecialBlockDefinition def, int fallbackShapeID)
        {
            if (def == null) return fallbackShapeID;
            return def.uniqueShapeId;
        }

        private static float EstimateSelfContribution(SpecialBlockDefinition def, Vector2Int anchorArray, GridManager grid)
        {
            if (def == null || def.effectAssets == null) return 0f;
            SpecialBlockInstance preview = SpecialBlockInstance.CreateDragPreview(def, anchorArray, grid.width, grid.height);
            if (preview == null) return 0f;

            float sum = 0f;
            for (int i = 0; i < def.effectAssets.Length; i++)
            {
                EffectAsset eff = def.effectAssets[i];
                if (eff == null) continue;
                sum += eff.EstimateLivePower(preview);
            }
            return sum;
        }

        private static Color ColorIDToRealColor(int colorID)
        {
            if (colorID == 1) return new Color(1f, 0.2f, 0.2f);
            if (colorID == 2) return new Color(0.2f, 0.4f, 1f);
            if (colorID == 3) return new Color(0.2f, 1f, 0.2f);
            return Color.white;
        }

        private static void SeedTraceRawValues(PowerCalculationContext ctx)
        {
            if (ctx.Trace == null) return;
            ctx.Trace.RecordRaw(CalcStage.Base, "기본 생산량(칸)", ctx.BaseProductionRaw);
            ctx.Trace.RecordRaw(CalcStage.UniqueParts, "부품 종류", ctx.UniquePartsRaw);
            ctx.Trace.RecordRaw(CalcStage.BaseCompletion, "기본 완성도", ctx.BaseCompletionRaw);
            ctx.Trace.RecordRaw(CalcStage.ShapeCompletion, "모양 완성도", ctx.ShapeCompletionRaw);
            ctx.Trace.RecordRaw(CalcStage.ColorMultiplier, "색상 순도 배율", ctx.ColorMultiplierRaw);
        }
    }
}
