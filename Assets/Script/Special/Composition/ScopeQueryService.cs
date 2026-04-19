using System.Collections.Generic;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition
{
    /// <summary>
    /// ConditionModule / EffectModule 들이 "범위 내 발전소/빈칸/색상 분포" 등의 데이터를
    /// 매번 직접 수집하지 않도록 중앙화한 헬퍼. 기존 ScopeEvaluator 위에 도메인 질의 API 만 얹는다.
    ///
    /// GridManager 는 싱글턴이 아니므로 한번 찾은 결과를 약식 캐시한다 (씬 전환 시 무효화는 호출자 책임).
    /// </summary>
    public static class ScopeQueryService
    {
        private static GridManager cachedGrid;

        public static GridManager Grid
        {
            get
            {
                if (cachedGrid == null) cachedGrid = Object.FindFirstObjectByType<GridManager>();
                return cachedGrid;
            }
        }

        public static void InvalidateCache() { cachedGrid = null; }

        // ===== 발전소 (그룹) 질의 =====

        /// <summary>scope/range 에 일치하는 모든 GroupInfo 목록.</summary>
        public static List<GroupInfo> QueryPowerPlants(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            List<GroupInfo> result = new List<GroupInfo>();
            if (PowerManager.Instance == null || owner == null) return result;
            List<GroupInfo> groups = PowerManager.Instance.activeGroups;
            for (int i = 0; i < groups.Count; i++)
            {
                if (ScopeEvaluator.GroupMatches(owner, scope, range, groups[i])) result.Add(groups[i]);
            }
            return result;
        }

        /// <summary>특정 색상(1=R,2=B,3=Y) 의 발전소만 추려 반환.</summary>
        public static List<GroupInfo> QueryPowerPlantsOfColor(SpecialBlockInstance owner, EffectScope scope, int range, int colorId)
        {
            List<GroupInfo> result = new List<GroupInfo>();
            List<GroupInfo> all = QueryPowerPlants(owner, scope, range);
            for (int i = 0; i < all.Count; i++) if (all[i].finalColor == colorId) result.Add(all[i]);
            return result;
        }

        /// <summary>scope 내 발전소 finalColor 별 개수 분포.</summary>
        public static Dictionary<int, int> QueryColorCounts(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            List<GroupInfo> plants = QueryPowerPlants(owner, scope, range);
            for (int i = 0; i < plants.Count; i++)
            {
                int c = plants[i].finalColor;
                counts.TryGetValue(c, out int n);
                counts[c] = n + 1;
            }
            return counts;
        }

        /// <summary>scope 내 finalColor==colorId 발전소 비율 (0..1). 발전소 0개면 0 반환.</summary>
        public static float QueryDominantColorRatio(SpecialBlockInstance owner, EffectScope scope, int range, int colorId)
        {
            List<GroupInfo> plants = QueryPowerPlants(owner, scope, range);
            if (plants.Count == 0) return 0f;
            int hit = 0;
            for (int i = 0; i < plants.Count; i++) if (plants[i].finalColor == colorId) hit++;
            return (float)hit / plants.Count;
        }

        /// <summary>scope 내 발전소들의 finalShape 의 unique 개수 (0=무모양 제외).</summary>
        public static int QueryShapeVariety(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            HashSet<int> shapes = new HashSet<int>();
            List<GroupInfo> plants = QueryPowerPlants(owner, scope, range);
            for (int i = 0; i < plants.Count; i++)
            {
                int s = plants[i].finalShape;
                if (s > 0) shapes.Add(s);
            }
            return shapes.Count;
        }

        // ===== 셀 단위 질의 =====

        /// <summary>scope/range 가 가리키는 모든 셀(배열 인덱스) 열거.</summary>
        public static IEnumerable<Vector2Int> EnumerateScopeCells(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            GridManager grid = Grid;
            if (grid == null || owner == null) yield break;

            switch (scope)
            {
                case EffectScope.Range:
                    foreach (var c in ScopeEvaluator.CellsInRange(owner, range, grid.width, grid.height)) yield return c;
                    break;
                case EffectScope.AdjacentPowerPlant:
                    foreach (var c in ScopeEvaluator.CellsInRange(owner, 1, grid.width, grid.height)) yield return c;
                    break;
                case EffectScope.OwnPowerPlant:
                    if (owner.footprint != null)
                        for (int i = 0; i < owner.footprint.Count; i++) yield return owner.footprint[i];
                    break;
                case EffectScope.Zone:
                    {
                        IReadOnlyList<Vector2Int> cells = ZoneServiceLocator.Current.GetCellsInZone(owner.zoneId);
                        if (cells != null && cells.Count > 0)
                        {
                            for (int i = 0; i < cells.Count; i++) yield return cells[i];
                        }
                        else
                        {
                            // SingleZoneFallback: zone 미구현 단계. Global 과 동등하게 보드 전체.
                            for (int x = 0; x < grid.width; x++)
                                for (int y = 0; y < grid.height; y++) yield return new Vector2Int(x, y);
                        }
                    }
                    break;
                case EffectScope.Global:
                    for (int x = 0; x < grid.width; x++)
                        for (int y = 0; y < grid.height; y++) yield return new Vector2Int(x, y);
                    break;
            }
        }

        /// <summary>scope 내 빈 셀 (블럭이 없거나 colorID&lt;=0) 의 좌표 목록.</summary>
        public static List<Vector2Int> QueryEmptyCells(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            GridManager grid = Grid;
            if (grid == null) return result;
            foreach (Vector2Int cell in EnumerateScopeCells(owner, scope, range))
            {
                if (grid.IsEmptyCell(cell)) result.Add(cell);
            }
            return result;
        }

        /// <summary>scope 내 미그룹 블럭(colorID&gt;0 && !isGrouped) 좌표 목록.</summary>
        public static List<Vector2Int> QueryUngroupedBlocks(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            GridManager grid = Grid;
            if (grid == null) return result;
            foreach (Vector2Int cell in EnumerateScopeCells(owner, scope, range))
            {
                BlockData b = grid.GetBlockAtArrayIndex(cell);
                if (b == null || b.attribute == null) continue;
                if (b.attribute.colorID > 0 && !b.isGrouped) result.Add(cell);
            }
            return result;
        }

        /// <summary>scope 내 colorID 일치 블럭 좌표 목록 (그룹 여부 무관).</summary>
        public static List<Vector2Int> QueryBlocksByColor(SpecialBlockInstance owner, EffectScope scope, int range, int colorId)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            GridManager grid = Grid;
            if (grid == null) return result;
            foreach (Vector2Int cell in EnumerateScopeCells(owner, scope, range))
            {
                BlockData b = grid.GetBlockAtArrayIndex(cell);
                if (b == null || b.attribute == null) continue;
                if (b.attribute.colorID == colorId) result.Add(cell);
            }
            return result;
        }

        /// <summary>특정 그룹 내부의 색상별 블럭 카운트 (cell.attribute.colorID 기준).</summary>
        public static Dictionary<int, int> QueryGroupInternalColorCounts(GroupInfo group)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            GridManager grid = Grid;
            if (grid == null || group == null || group.clusterPositions == null) return counts;
            for (int i = 0; i < group.clusterPositions.Count; i++)
            {
                BlockData b = grid.GetBlockAtArrayIndex(group.clusterPositions[i]);
                if (b == null || b.attribute == null) continue;
                int c = b.attribute.colorID;
                counts.TryGetValue(c, out int n);
                counts[c] = n + 1;
            }
            return counts;
        }
    }
}
