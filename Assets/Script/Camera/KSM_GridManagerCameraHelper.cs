using UnityEngine;

/// <summary>
/// GridManager의 기존 본문은 최대한 건드리지 않고,
/// 카메라가 안전하게 참조할 수 있는 read-only 헬퍼만 추가하는 partial 파일.
///
/// 추가 기능
/// 1. 현재 열린 보드 기준 월드 Rect 반환
/// 2. 최대 확장 가능 범위 전체 기준 월드 Rect 반환
///
/// 주의
/// - 배치 로직 / 확장 로직 / 프리뷰 로직은 수정하지 않는다.
/// - 외부 스크립트는 이 helper를 조회만 하고, GridManager 내부 상태를 직접 바꾸지 않는다.
/// </summary>
public partial class GridManager
{
    /// <summary>
    /// 현재 실제로 열린 보드 bounding rectangle의 월드 Rect를 반환한다.
    /// currentOffset + width/height 기준이라, 구매 완료된 영역만 포함한다.
    /// </summary>
    public bool KSM_TryGetOpenedBoardWorldRect(out Rect worldRect)
    {
        worldRect = default;

        if (groundTilemap == null || width <= 0 || height <= 0)
        {
            return false;
        }

        Vector3 worldMin = groundTilemap.CellToWorld(new Vector3Int(currentOffset.x, currentOffset.y, 0));
        Vector3 worldMax = groundTilemap.CellToWorld(new Vector3Int(currentOffset.x + width, currentOffset.y + height, 0));

        float xMin = Mathf.Min(worldMin.x, worldMax.x);
        float yMin = Mathf.Min(worldMin.y, worldMax.y);
        float xMax = Mathf.Max(worldMin.x, worldMax.x);
        float yMax = Mathf.Max(worldMin.y, worldMax.y);

        worldRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }

    /// <summary>
    /// 현재 프로젝트에서 탐색 가능한 전체 맵 범위(최대 확장 제한 기준)의 월드 Rect를 반환한다.
    /// 잠긴 지역까지 미리 보여주는 오버레이/후보 타일과 같은 전체 시야 clamp에 사용한다.
    /// </summary>
    public bool KSM_TryGetCameraNavigationWorldRect(out Rect worldRect)
    {
        worldRect = default;

        if (groundTilemap == null || RegionSize <= 0)
        {
            return false;
        }

        int minRegionX = -maxWestExpandCount;
        int maxRegionX = maxEastExpandCount;
        int minRegionY = -maxSouthExpandCount;
        int maxRegionY = maxNorthExpandCount;

        int startCellX = minRegionX * RegionSize;
        int startCellY = minRegionY * RegionSize;
        int endCellX = (maxRegionX + 1) * RegionSize;
        int endCellY = (maxRegionY + 1) * RegionSize;

        Vector3 worldMin = groundTilemap.CellToWorld(new Vector3Int(startCellX, startCellY, 0));
        Vector3 worldMax = groundTilemap.CellToWorld(new Vector3Int(endCellX, endCellY, 0));

        float xMin = Mathf.Min(worldMin.x, worldMax.x);
        float yMin = Mathf.Min(worldMin.y, worldMax.y);
        float xMax = Mathf.Max(worldMin.x, worldMax.x);
        float yMax = Mathf.Max(worldMin.y, worldMax.y);

        worldRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }
}