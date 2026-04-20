using System;
using System.Collections.Generic;
using Special.Runtime;
using UnityEngine;

namespace Special.Integration
{
    /// <summary>
    /// GridManager 의 "열린 구역(region)" 구조를 특수 블럭 시스템이 쓰는 <see cref="IZoneService"/> 로 노출하는 어댑터.
    ///
    /// 좌표계 계약
    /// - 입력 셀은 배열 인덱스(0..W-1, 0..H-1). <see cref="IZoneService"/> 호출자(ScopeEvaluator / GridManager.PlaceShape 등)가
    ///   배열 인덱스로 부른다. GridManager 내부 좌표계와 일치.
    /// - zoneId 는 "구역 좌표(Vector2Int)" 를 정수로 stable 하게 인코딩한 값. 구역이 열린 순서와 무관하게
    ///   같은 regionCoord 에는 항상 같은 zoneId 가 배정된다. 배열 offset 이 밀려도 ID 는 유효.
    ///
    /// 왜 이렇게 분리했나
    /// - GridManager 는 <see cref="IZoneService"/> 구체 구현을 몰라야 특수 블럭 시스템 없이도 빌드 가능.
    /// - 특수 블럭 쪽은 <see cref="ZoneServiceLocator"/> 만 알면 되어 맵 구조가 바뀌어도 어댑터만 교체하면 됨.
    ///
    /// 확장 포인트
    /// - "구역별 메타데이터(이름, 테마 등)" 가 생기면 zoneId → 메타 조회를 여기에 추가.
    /// - "맵이 완전히 다른 형태(예: 그래프형 타일)" 로 변하면 이 어댑터만 교체.
    /// </summary>
    public sealed class GridZoneService : IZoneService
    {
        private readonly GridManager grid;

        // regionCoord ↔ zoneId 양방향 매핑. 최초 조회 시 lazy-assign.
        // "0 번 zone 이 시작 구역(0,0)" 이라는 관례를 유지하기 위해 시작 구역을 미리 배정해둔다.
        private readonly Dictionary<Vector2Int, int> regionToZoneId = new();
        private readonly Dictionary<int, Vector2Int> zoneIdToRegion = new();
        private int nextZoneId;

        public GridZoneService(GridManager grid)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

            // (0,0) 시작 구역을 zoneId=0 으로 고정. SingleZoneFallback 과의 관례 유지.
            AssignZoneId(Vector2Int.zero);
        }

        /// <summary>
        /// 배열 인덱스가 속한 구역의 zoneId 를 돌려준다.
        /// 아직 열리지 않은 구역 셀이 들어와도 안정적인 id 를 배정해 반환한다
        /// (드래그 프리뷰가 구역 밖을 짚을 때도 crash 하지 않도록).
        /// </summary>
        public int GetZoneIdFromCell(Vector2Int arrayCell)
        {
            if (grid == null) return 0;

            Vector3Int worldCell = grid.ArrayIndexToWorldCell(arrayCell);
            Vector2Int region = grid.GetRegionCoordFromWorldCell(worldCell);
            return AssignZoneId(region);
        }

        /// <summary>
        /// zoneId 에 해당하는 구역의 현재 배열 인덱스 셀 목록을 반환.
        /// 배열 offset 이 변경된 뒤에도 호출 시점의 offset 으로 라이브 계산하므로 항상 일관.
        /// 맵이 확장되지 않은 빈 zoneId 이거나 현재 보드 밖이면 빈 목록.
        /// </summary>
        public IReadOnlyList<Vector2Int> GetCellsInZone(int zoneId)
        {
            if (grid == null) return Array.Empty<Vector2Int>();
            if (!zoneIdToRegion.TryGetValue(zoneId, out Vector2Int region)) return Array.Empty<Vector2Int>();

            RectInt rect = grid.GetRegionRect(region);
            List<Vector2Int> cells = new List<Vector2Int>(rect.width * rect.height);

            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int y = rect.yMin; y < rect.yMax; y++)
                {
                    Vector2Int idx = grid.WorldCellToArrayIndex(new Vector3Int(x, y, 0));
                    if (idx.x < 0 || idx.x >= grid.width || idx.y < 0 || idx.y >= grid.height) continue;
                    cells.Add(idx);
                }
            }

            return cells;
        }

        private int AssignZoneId(Vector2Int region)
        {
            if (regionToZoneId.TryGetValue(region, out int existing)) return existing;

            int id = nextZoneId++;
            regionToZoneId[region] = id;
            zoneIdToRegion[id] = region;
            return id;
        }
    }
}
