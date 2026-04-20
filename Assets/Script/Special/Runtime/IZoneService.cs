using System.Collections.Generic;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 보드 셀 ↔ 구역 매핑을 제공하는 seam. 좌표는 배열 인덱스(0..W-1, 0..H-1) 기준.
    /// 기본 런타임 구현은 <c>Special.Integration.GridZoneService</c> 로 GridManager.Awake 에서 주입되며
    /// 실제 열린 구역(region) 구조를 반영한다.
    /// 테스트/씬 초기화 전에는 <see cref="SingleZoneFallback"/> 이 기본값으로 응답한다.
    /// </summary>
    public interface IZoneService
    {
        int GetZoneIdFromCell(Vector2Int cell);
        IReadOnlyList<Vector2Int> GetCellsInZone(int zoneId);
    }

    public class SingleZoneFallback : IZoneService
    {
        public int GetZoneIdFromCell(Vector2Int cell) => 0;
        public IReadOnlyList<Vector2Int> GetCellsInZone(int zoneId) => System.Array.Empty<Vector2Int>();
    }

    public static class ZoneServiceLocator
    {
        private static IZoneService _current;
        public static IZoneService Current => _current ??= new SingleZoneFallback();
        public static void SetService(IZoneService service) { _current = service; }
    }
}
