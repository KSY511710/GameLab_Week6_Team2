using System.Collections.Generic;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 다른 브랜치에서 10x10 구역(Zone) 시스템이 머지되면 이 인터페이스 구현체를 교체.
    /// 지금은 SingleZoneFallback 이 전체 보드를 zone 0 으로 리턴한다.
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
