using System.Collections.Generic;
using Special.Effects;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 효과 콜백이 자신의 Scope 가 현재 이벤트에 해당하는지 질의할 수 있는 헬퍼.
    /// 좌표는 전부 "배열 인덱스"(0..W-1, 0..H-1) 기준.
    /// </summary>
    public static class ScopeEvaluator
    {
        public static bool GroupIncludesOwner(SpecialBlockInstance owner, GroupInfo group)
        {
            if (owner == null || group == null) return false;
            return group.groupID == owner.groupId && owner.groupId > 0;
        }

        public static bool GroupWithinRange(SpecialBlockInstance owner, GroupInfo group, int rangeInCells)
        {
            if (owner == null || group == null || group.clusterPositions == null) return false;
            for (int i = 0; i < group.clusterPositions.Count; i++)
            {
                Vector2Int gc = group.clusterPositions[i];
                for (int j = 0; j < owner.footprint.Count; j++)
                {
                    if (ManhattanDistance(gc, owner.footprint[j]) <= rangeInCells) return true;
                }
            }
            return false;
        }

        public static bool GroupInZone(SpecialBlockInstance owner, GroupInfo group)
        {
            if (owner == null || group == null || group.clusterPositions == null) return false;
            IZoneService zones = ZoneServiceLocator.Current;
            for (int i = 0; i < group.clusterPositions.Count; i++)
            {
                if (zones.GetZoneIdFromCell(group.clusterPositions[i]) == owner.zoneId) return true;
            }
            return false;
        }

        public static bool GroupAdjacentToOwner(SpecialBlockInstance owner, GroupInfo group)
        {
            if (owner == null || group == null || group.clusterPositions == null) return false;
            for (int i = 0; i < group.clusterPositions.Count; i++)
            {
                Vector2Int gc = group.clusterPositions[i];
                for (int j = 0; j < owner.footprint.Count; j++)
                {
                    if (ManhattanDistance(gc, owner.footprint[j]) == 1) return true;
                }
            }
            return false;
        }

        /// <summary>Scope + 범위로 단일 판정.</summary>
        public static bool GroupMatches(SpecialBlockInstance owner, EffectScope scope, int rangeInCells, GroupInfo group)
        {
            switch (scope)
            {
                case EffectScope.Range: return GroupWithinRange(owner, group, rangeInCells);
                case EffectScope.Global: return true;
                case EffectScope.OwnPowerPlant: return GroupIncludesOwner(owner, group);
                case EffectScope.Zone: return GroupInZone(owner, group);
                case EffectScope.AdjacentPowerPlant: return GroupAdjacentToOwner(owner, group);
                default: return false;
            }
        }

        public static int ManhattanDistance(Vector2Int a, Vector2Int b)
            => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>Range scope 에서 owner footprint 주변에 있는 모든 셀을 반환.</summary>
        public static IEnumerable<Vector2Int> CellsInRange(SpecialBlockInstance owner, int rangeInCells, int width, int height)
        {
            HashSet<Vector2Int> emitted = new HashSet<Vector2Int>();
            for (int i = 0; i < owner.footprint.Count; i++)
            {
                Vector2Int origin = owner.footprint[i];
                for (int dx = -rangeInCells; dx <= rangeInCells; dx++)
                {
                    int remain = rangeInCells - Mathf.Abs(dx);
                    for (int dy = -remain; dy <= remain; dy++)
                    {
                        Vector2Int cell = new Vector2Int(origin.x + dx, origin.y + dy);
                        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) continue;
                        if (emitted.Add(cell)) yield return cell;
                    }
                }
            }
        }
    }
}
