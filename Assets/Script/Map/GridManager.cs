using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 4방향 확장 방향 enum.
/// 북 / 남 / 서 / 동 버튼에서 사용한다.
/// </summary>
public enum KSM_ExpandDirection
{
    North,
    South,
    West,
    East
}

/// <summary>
/// 방향 확장 시도 결과.
/// 버튼 스크립트에서 성공 / 실패 사유를 구분할 때 사용한다.
/// </summary>
public enum KSM_ExpandResult
{
    Success,
    Busy,
    NotEnoughMoney,
    InvalidSetup
}

/// <summary>
/// GridManager
/// 
/// 담당 역할
/// 1. 시작 구역 생성
/// 2. 블록 배치 가능 여부 검사
/// 3. 블록 실제 배치
/// 4. "열린 구역 좌표 집합" 기반 맵 확장
/// 5. 확장 프리뷰 표시 / 제거
/// 6. RuleTile / AutoTile refresh
/// 7. 카메라 보정
/// 8. 포트 버튼 위치 계산
/// 9. 방향별 최대 확장 범위 제한
/// 
/// 중요한 점
/// - 블록 배치 관련 public API는 기존 흐름을 유지한다.
/// - 이제 맵은 직사각형 전체를 확장하는 것이 아니라,
///   "열린 구역(Vector2Int regionCoord)" 들의 집합으로 관리된다.
/// - 단, PowerManager가 여전히 2차원 배열을 받기 때문에
///   bounding rectangle(boardData / buildingObjects)는 유지한다.
/// - 열린 구역 안의 셀만 실제 배치 가능하다.
/// </summary>
public class GridManager : MonoBehaviour
{
    [Header("Board Settings")]
    [Tooltip("초기 시작 구역 한 변 길이. 예: 5면 한 구역은 5 x 5 셀.")]
    public int StartGrid = 5;

    [Tooltip("현재 bounding rectangle 기준 전체 가로 칸 수.")]
    public int width;

    [Tooltip("현재 bounding rectangle 기준 전체 세로 칸 수.")]
    public int height;

    [Header("Expand Limit Settings")]
    [Tooltip("시작 구역 (0,0) 기준 북쪽으로 최대 몇 구역까지 확장 가능한지.")]
    [SerializeField, Min(0)] private int maxNorthExpandCount = 2;

    [Tooltip("시작 구역 (0,0) 기준 남쪽으로 최대 몇 구역까지 확장 가능한지.")]
    [SerializeField, Min(0)] private int maxSouthExpandCount = 2;

    [Tooltip("시작 구역 (0,0) 기준 서쪽으로 최대 몇 구역까지 확장 가능한지.")]
    [SerializeField, Min(0)] private int maxWestExpandCount = 2;

    [Tooltip("시작 구역 (0,0) 기준 동쪽으로 최대 몇 구역까지 확장 가능한지.")]
    [SerializeField, Min(0)] private int maxEastExpandCount = 2;

    [Header("References")]
    [Tooltip("바닥 타일이 깔리는 Tilemap.")]
    public Tilemap groundTilemap;

    [Tooltip("열린 셀에 사용할 기본 바닥 타일.")]
    public TileBase baseTile;

    [Header("Camera")]
    [Tooltip("현재 맵 전체를 비추는 메인 카메라.")]
    public Camera mainCamera;

    [Tooltip("카메라 보간 속도.")]
    public float zoomSpeed = 3f;

    [Tooltip("일반 상태 카메라 패딩.")]
    public float padding = 2f;

    [Tooltip("카메라 X축 보정.")]
    public float offsetX = 0f;

    [Tooltip("카메라 Y축 보정.")]
    public float offsetY = 0f;

    [Header("Preview Camera")]
    [Tooltip("프리뷰 시 추가로 더 넓게 보여줄 패딩.")]
    [SerializeField] private float previewPadding = 3.5f;

    [Tooltip("Hover 프리뷰 때 카메라도 같이 움직일지 여부. 깜빡임 방지를 위해 기본값 false 권장.")]
    [SerializeField] private bool moveCameraOnHoverPreview = false;

    [Header("Preview Visual")]
    [Tooltip("프리뷰 외곽선 셀 색상.")]
    [SerializeField] private Color previewBorderColor = new Color(0.95f, 0.95f, 0.95f, 0.95f);

    [Tooltip("프리뷰 내부 셀 색상.")]
    [SerializeField] private Color previewFillColor = new Color(0.75f, 0.75f, 0.75f, 0.18f);

    [Tooltip("프리뷰 중앙 가이드 줄 색상.")]
    [SerializeField] private Color previewGuideColor = new Color(1f, 1f, 1f, 0.38f);

    /// <summary>
    /// 현재 boardData[0,0]가 실제 월드 셀 좌표에서 어디인지 나타내는 오프셋.
    /// bounding rectangle의 최소 월드 셀 좌표라고 생각하면 된다.
    /// </summary>
    private Vector2Int currentOffset = Vector2Int.zero;

    /// <summary>
    /// 현재 bounding rectangle 안에 존재하는 블록 데이터 배열.
    /// 열린 구역이 아닌 셀은 null 상태로 남을 수 있다.
    /// </summary>
    private BlockData[,] boardData;

    /// <summary>
    /// 멀티 셀 블록 부모 오브젝트 저장 배열.
    /// </summary>
    private GameObject[,] buildingObjects;

    /// <summary>
    /// 실제로 열린 구역 좌표들.
    /// 예:
    /// (0,0) 시작
    /// (1,0) 동쪽 구역
    /// (1,1) 위쪽 구역
    /// </summary>
    private readonly HashSet<Vector2Int> openedRegions = new HashSet<Vector2Int>();

    /// <summary>
    /// 현재 프리뷰로 임시 표시 중인 셀 목록.
    /// </summary>
    private readonly List<Vector3Int> previewCells = new List<Vector3Int>();

    /// <summary>
    /// 현재 프리뷰 활성 여부.
    /// </summary>
    private bool isPreviewActive;

    /// <summary>
    /// 현재 어떤 targetRegion을 프리뷰 중인지 저장한다.
    /// 같은 프리뷰를 중복으로 다시 그리지 않기 위해 사용한다.
    /// </summary>
    private Vector2Int currentPreviewRegion = new Vector2Int(int.MinValue, int.MinValue);

    /// <summary>
    /// currentPreviewRegion 값이 현재 유효한지 여부.
    /// </summary>
    private bool hasPreviewRegion;

    /// <summary>
    /// 한 구역의 기준 크기.
    /// 현재 설계에서는 StartGrid가 곧 구역 크기다.
    /// </summary>
    public int RegionSize => Mathf.Max(1, StartGrid);

    /// <summary>
    /// 확장 상태가 바뀌었을 때 버튼 / 라벨 / 버튼 매니저가 갱신되도록 알리는 이벤트.
    /// </summary>
    public static event Action OnExpandStateChanged;

    /// <summary>
    /// 시작 시 초기 구역 (0,0)을 연다.
    /// </summary>
    private void Start()
    {
        StartGrid = Mathf.Max(1, StartGrid);

        openedRegions.Clear();
        openedRegions.Add(Vector2Int.zero);

        currentOffset = Vector2Int.zero;
        width = RegionSize;
        height = RegionSize;

        boardData = new BlockData[width, height];
        buildingObjects = new GameObject[width, height];

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        RectInt startRect = GetRegionWorldRect(Vector2Int.zero);
        FillWorldRectWithBaseTile(startRect);
        RefreshTilesAroundRect(startRect);

        UpdateCameraTarget(true);
        RaiseExpandStateChanged();
    }

    /// <summary>
    /// 현재 열린 모든 구역 목록을 복사해서 반환한다.
    /// 버튼 매니저가 사용한다.
    /// </summary>
    /// <returns>열린 구역 좌표 목록</returns>
    public List<Vector2Int> GetOpenedRegions()
    {
        return new List<Vector2Int>(openedRegions);
    }

    /// <summary>
    /// 특정 구역이 열려 있는지 검사한다.
    /// </summary>
    /// <param name="regionCoord">구역 좌표</param>
    /// <returns>열려 있으면 true</returns>
    public bool IsRegionOpened(Vector2Int regionCoord)
    {
        return openedRegions.Contains(regionCoord);
    }

    /// <summary>
    /// 마우스 월드 좌표를 타일맵 셀 좌표로 변환한다.
    /// 기존 DraggableBlock 흐름 유지용.
    /// </summary>
    /// <param name="worldPos">월드 좌표</param>
    /// <returns>타일 셀 좌표</returns>
    public Vector3Int GetCellPositionFromMouse(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

    /// <summary>
    /// 월드 타일 좌표를 내부 배열 인덱스로 바꾼다.
    /// </summary>
    /// <param name="tileX">월드 타일 X</param>
    /// <param name="tileY">월드 타일 Y</param>
    /// <returns>배열 인덱스</returns>
    private Vector2Int TileToArrayIndex(int tileX, int tileY)
    {
        return new Vector2Int(tileX - currentOffset.x, tileY - currentOffset.y);
    }

    /// <summary>
    /// 월드 셀이 현재 열린 구역 안에 포함되는지 검사한다.
    /// 단순히 bounding rectangle 안에 있는지가 아니라,
    /// 실제 열린 region에 속하는지 검사한다.
    /// </summary>
    /// <param name="worldCell">검사할 셀</param>
    /// <returns>열린 구역 안이면 true</returns>
    public bool IsWorldCellUnlocked(Vector3Int worldCell)
    {
        Vector2Int regionCoord = GetRegionCoordFromWorldCell(worldCell);
        return openedRegions.Contains(regionCoord);
    }

    /// <summary>
    /// 월드 셀이 어느 구역에 속하는지 계산한다.
    /// </summary>
    /// <param name="worldCell">월드 셀</param>
    /// <returns>구역 좌표</returns>
    public Vector2Int GetRegionCoordFromWorldCell(Vector3Int worldCell)
    {
        int regionX = FloorDiv(worldCell.x, RegionSize);
        int regionY = FloorDiv(worldCell.y, RegionSize);
        return new Vector2Int(regionX, regionY);
    }

    /// <summary>
    /// 구역 좌표를 실제 월드 셀 사각형 범위로 바꾼다.
    /// </summary>
    /// <param name="regionCoord">구역 좌표</param>
    /// <returns>구역 RectInt</returns>
    public RectInt GetRegionRect(Vector2Int regionCoord)
    {
        return GetRegionWorldRect(regionCoord);
    }

    /// <summary>
    /// 특정 월드 셀의 BlockData를 반환한다.
    /// 열린 범위 밖이면 null을 반환한다.
    /// </summary>
    /// <param name="worldCell">조회할 셀</param>
    /// <returns>BlockData 또는 null</returns>
    public BlockData GetBlockDataFromWorldCell(Vector3Int worldCell)
    {
        if (!IsWorldCellUnlocked(worldCell))
        {
            return null;
        }

        Vector2Int arrayIndex = TileToArrayIndex(worldCell.x, worldCell.y);

        if (arrayIndex.x < 0 || arrayIndex.x >= width || arrayIndex.y < 0 || arrayIndex.y >= height)
        {
            return null;
        }

        return boardData[arrayIndex.x, arrayIndex.y];
    }

    /// <summary>
    /// sourceRegion의 인접 방향에 있는 targetRegion을 계산한다.
    /// </summary>
    /// <param name="sourceRegion">기준 구역</param>
    /// <param name="direction">방향</param>
    /// <returns>이웃 구역 좌표</returns>
    public Vector2Int GetNeighborRegionCoord(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        switch (direction)
        {
            case KSM_ExpandDirection.North:
                return sourceRegion + Vector2Int.up;

            case KSM_ExpandDirection.South:
                return sourceRegion + Vector2Int.down;

            case KSM_ExpandDirection.West:
                return sourceRegion + Vector2Int.left;

            case KSM_ExpandDirection.East:
            default:
                return sourceRegion + Vector2Int.right;
        }
    }

    /// <summary>
    /// 특정 구역 좌표가 설정된 최대 확장 범위 안에 들어오는지 검사한다.
    /// 
    /// 예:
    /// maxEastExpandCount = 3 이면 x <= 3 까지 허용.
    /// maxWestExpandCount = 2 이면 x >= -2 까지 허용.
    /// </summary>
    /// <param name="regionCoord">검사할 구역 좌표</param>
    /// <returns>허용 범위 안이면 true</returns>
    public bool IsRegionWithinExpandBounds(Vector2Int regionCoord)
    {
        bool inX = regionCoord.x >= -maxWestExpandCount && regionCoord.x <= maxEastExpandCount;
        bool inY = regionCoord.y >= -maxSouthExpandCount && regionCoord.y <= maxNorthExpandCount;
        return inX && inY;
    }

    /// <summary>
    /// 특정 포트가 구조적으로 존재 가능한지 검사한다.
    /// 
    /// 구조적으로 가능하다는 뜻:
    /// - sourceRegion이 열려 있어야 함
    /// - targetRegion이 아직 열려 있지 않아야 함
    /// - targetRegion이 최대 확장 범위 안이어야 함
    /// 
    /// 돈 부족 / 애니메이션 중 여부는 여기서 안 본다.
    /// 버튼 매니저는 이 기준으로 "버튼을 생성할지" 결정한다.
    /// </summary>
    /// <param name="sourceRegion">버튼이 붙을 기준 구역</param>
    /// <param name="direction">방향</param>
    /// <returns>구조적으로 포트가 있으면 true</returns>
    public bool HasStructuralExpansionPort(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (!openedRegions.Contains(sourceRegion))
        {
            return false;
        }

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);

        if (openedRegions.Contains(targetRegion))
        {
            return false;
        }

        if (!IsRegionWithinExpandBounds(targetRegion))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 특정 포트가 실제로 확장 가능한지 검사한다.
    /// 
    /// 실제 가능하다는 뜻:
    /// - 구조적으로 가능
    /// - ResourceManager / Tilemap / BaseTile 유효
    /// - PowerManager 애니메이션 중 아님
    /// - 확장 비용 보유
    /// </summary>
    /// <param name="sourceRegion">버튼이 붙은 기준 구역</param>
    /// <param name="direction">방향</param>
    /// <returns>실제 확장 가능하면 true</returns>
    public bool CanExpandFromRegion(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null || ResourceManager.Instance == null)
        {
            return false;
        }

        if (PowerManager.Instance != null && PowerManager.Instance.IsAnimating)
        {
            return false;
        }

        if (!HasStructuralExpansionPort(sourceRegion, direction))
        {
            return false;
        }

        int cost = ResourceManager.Instance.GetExpandCost();
        return ResourceManager.Instance.HasCurrency(CurrencyType.Money, cost);
    }

    /// <summary>
    /// 기존 호환용.
    /// "이 방향으로 확장 가능한 포트가 하나라도 있는지"를 검사한다.
    /// </summary>
    /// <param name="direction">방향</param>
    /// <returns>가능한 포트가 하나라도 있으면 true</returns>
    public bool CanExpandDirection(KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null || ResourceManager.Instance == null)
        {
            return false;
        }

        if (PowerManager.Instance != null && PowerManager.Instance.IsAnimating)
        {
            return false;
        }

        int cost = ResourceManager.Instance.GetExpandCost();
        bool hasMoney = ResourceManager.Instance.HasCurrency(CurrencyType.Money, cost);

        if (!hasMoney)
        {
            return false;
        }

        foreach (Vector2Int region in openedRegions)
        {
            if (HasStructuralExpansionPort(region, direction))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 확장 비용을 지불할 수 있는지 검사한다.
    /// 기존 호환용.
    /// </summary>
    /// <returns>확장 비용 지불 가능 여부</returns>
    public bool CanAffordExpand()
    {
        if (ResourceManager.Instance == null)
        {
            return false;
        }

        return ResourceManager.Instance.HasCurrency(CurrencyType.Money, ResourceManager.Instance.GetExpandCost());
    }

    /// <summary>
    /// 월드 버튼 매니저가 사용할 "구역 포트 버튼" 위치를 계산한다.
    /// 
    /// 버튼은 sourceRegion의 북/남/서/동 면 중앙에 놓인다.
    /// marginWorld는 경계에서 얼마나 더 바깥으로 띄울지 결정한다.
    /// </summary>
    /// <param name="sourceRegion">기준 구역</param>
    /// <param name="direction">포트 방향</param>
    /// <param name="marginWorld">경계에서 바깥으로 띄울 거리</param>
    /// <returns>포트 버튼 월드 위치</returns>
    public Vector3 GetExpandPortWorldPosition(Vector2Int sourceRegion, KSM_ExpandDirection direction, float marginWorld)
    {
        RectInt regionRect = GetRegionWorldRect(sourceRegion);

        Vector3 worldMin = groundTilemap.CellToWorld(new Vector3Int(regionRect.xMin, regionRect.yMin, 0));
        Vector3 worldMax = groundTilemap.CellToWorld(new Vector3Int(regionRect.xMax, regionRect.yMax, 0));

        float centerX = (worldMin.x + worldMax.x) * 0.5f;
        float centerY = (worldMin.y + worldMax.y) * 0.5f;

        switch (direction)
        {
            case KSM_ExpandDirection.North:
                return new Vector3(centerX, worldMax.y + marginWorld, 0f);

            case KSM_ExpandDirection.South:
                return new Vector3(centerX, worldMin.y - marginWorld, 0f);

            case KSM_ExpandDirection.West:
                return new Vector3(worldMin.x - marginWorld, centerY, 0f);

            case KSM_ExpandDirection.East:
            default:
                return new Vector3(worldMax.x + marginWorld, centerY, 0f);
        }
    }

    /// <summary>
    /// shape를 현재 셀에 배치할 수 있는지 검사한다.
    /// 
    /// 기존과 차이점:
    /// - bounding rectangle 안에 있는지만 보는 게 아니라
    /// - 해당 셀이 실제로 열린 구역에 속하는지 본다.
    /// </summary>
    /// <param name="startCell">배치 시작 셀</param>
    /// <param name="shapeCoords">shape 좌표 배열</param>
    /// <returns>배치 가능하면 true</returns>
    public bool CanPlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords)
    {
       // Next Day 시퀀스 진행 중이면 placement-eligibility 자체를 거부 (블럭 소진 방지)
    if (PowerManager.Instance != null && PowerManager.Instance.IsAnimating) return false;

    // 🌟 8방향(상하좌우 + 대각선) 검사를 위한 방향 배열
    Vector2Int[] directions = { 
        Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right,
        new Vector2Int(1, 1), new Vector2Int(1, -1),
        new Vector2Int(-1, 1), new Vector2Int(-1, -1)
    };

    foreach (var offset in shapeCoords)
    {
        Vector2Int arrayIdx = TileToArrayIndex(startCell.x + offset.x, startCell.y + offset.y);

        // 1. 격자 밖으로 나가는지 검사
        if (arrayIdx.x < 0 || arrayIdx.x >= width || arrayIdx.y < 0 || arrayIdx.y >= height) return false;

        // 2. 놓으려는 자리에 이미 블럭이 있는지 검사
        if (boardData[arrayIdx.x, arrayIdx.y] != null && boardData[arrayIdx.x, arrayIdx.y].attribute.colorID > 0) return false;

        // 🌟 3. 주변 8방향에 '완성된 발전소'가 있는지 검사
        foreach (Vector2Int dir in directions)
        {
            // 방금 구한 arrayIdx(배열 인덱스)를 기준으로 주변 칸 탐색
            int neighborX = arrayIdx.x + dir.x;
            int neighborY = arrayIdx.y + dir.y;

            // 이웃 칸이 배열 범위 안에 있는지 안전 검사
            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
            {
                BlockData neighborCell = boardData[neighborX, neighborY];

                // 이웃 칸에 블럭이 있고, 그 블럭이 그룹(isGrouped)에 속해 있다면 설치 불가!
                if (neighborCell != null && neighborCell.isGrouped)
                {
                    // (선택) 디버그 로그가 필요하시면 아래 주석을 푸세요
                    // Debug.Log($"<color=red>설치 불가:</color> ({neighborX}, {neighborY}) 위치의 기존 발전소와 인접해 있습니다.");
                    return false; 
                }
            }
        }
    }
    
    // 모든 조건을 무사히 통과하면 설치 허락!
    return true;
    }

    /// <summary>
    /// shape를 실제로 보드에 배치한다.
    /// 기존 배치 흐름 유지.
    /// </summary>
    /// <param name="startCell">배치 시작 셀</param>
    /// <param name="shapeCoords">shape 좌표 배열</param>
    /// <param name="colorID">색상 ID</param>
    /// <param name="shapeID">기호 ID</param>
    /// <param name="prefab">생성할 프리팹</param>
    public void PlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords, int colorID, int shapeID, GameObject prefab)
    {
        if (PowerManager.Instance != null && PowerManager.Instance.IsAnimating)
        {
            return;
        }

        GameObject buildingParent = new GameObject("MultiCell_Building");
        buildingParent.transform.position = groundTilemap.GetCellCenterWorld(startCell);

        foreach (Vector2Int offset in shapeCoords)
        {
            int tx = startCell.x + offset.x;
            int ty = startCell.y + offset.y;
            Vector2Int arrayIdx = TileToArrayIndex(tx, ty);

            Vector3 cellWorldPos = groundTilemap.GetCellCenterWorld(new Vector3Int(tx, ty, 0));
            GameObject cellPart = Instantiate(prefab, cellWorldPos, Quaternion.identity);
            cellPart.transform.SetParent(buildingParent.transform);

            boardData[arrayIdx.x, arrayIdx.y] = new BlockData
            {
                attribute = new BlockAttribute(colorID, shapeID),
                isGrouped = false,
                groupID = 0,
                blockObject = cellPart
            };

            buildingObjects[arrayIdx.x, arrayIdx.y] = buildingParent;
        }

        if (PowerManager.Instance != null)
        {
            PowerManager.Instance.CheckAndFormGroups(boardData, width, height);
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
            PowerManager.Instance.UpdateAllOutlines(boardData, width, height);
        }
    }

    /// <summary>
    /// 특정 sourceRegion의 특정 방향으로 확장을 시도한다.
    /// 새 구조의 핵심 함수다.
    /// </summary>
    /// <param name="sourceRegion">버튼이 붙은 기준 구역</param>
    /// <param name="direction">확장 방향</param>
    /// <returns>확장 결과</returns>
    public KSM_ExpandResult TryExpandFromRegion(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null || ResourceManager.Instance == null)
        {
            return KSM_ExpandResult.InvalidSetup;
        }

        if (PowerManager.Instance != null && PowerManager.Instance.IsAnimating)
        {
            return KSM_ExpandResult.Busy;
        }

        if (!HasStructuralExpansionPort(sourceRegion, direction))
        {
            return KSM_ExpandResult.InvalidSetup;
        }

        if (!CanExpandFromRegion(sourceRegion, direction))
        {
            return KSM_ExpandResult.NotEnoughMoney;
        }

        if (!ResourceManager.Instance.TryPayForExpand())
        {
            return KSM_ExpandResult.NotEnoughMoney;
        }

        ClearExpansionPreview(false);

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);
        OpenRegion(targetRegion);

        RaiseExpandStateChanged();
        return KSM_ExpandResult.Success;
    }

    /// <summary>
    /// 기존 호환용.
    /// 해당 방향으로 가능한 첫 번째 포트를 찾아 확장한다.
    /// </summary>
    /// <param name="direction">확장 방향</param>
    /// <returns>확장 결과</returns>
    public KSM_ExpandResult TryExpandDirectional(KSM_ExpandDirection direction)
    {
        foreach (Vector2Int region in openedRegions)
        {
            if (HasStructuralExpansionPort(region, direction))
            {
                return TryExpandFromRegion(region, direction);
            }
        }

        return KSM_ExpandResult.InvalidSetup;
    }

    /// <summary>
    /// 기존 단일 확장 버튼 호환용.
    /// 현재는 North 방향에서 가능한 첫 포트를 시도한다.
    /// </summary>
    public void TryExpandBoard()
    {
        TryExpandDirectional(KSM_ExpandDirection.North);
    }

    /// <summary>
    /// 북쪽 확장 버튼 호환용.
    /// </summary>
    public void TryExpandNorth()
    {
        TryExpandDirectional(KSM_ExpandDirection.North);
    }

    /// <summary>
    /// 남쪽 확장 버튼 호환용.
    /// </summary>
    public void TryExpandSouth()
    {
        TryExpandDirectional(KSM_ExpandDirection.South);
    }

    /// <summary>
    /// 서쪽 확장 버튼 호환용.
    /// </summary>
    public void TryExpandWest()
    {
        TryExpandDirectional(KSM_ExpandDirection.West);
    }

    /// <summary>
    /// 동쪽 확장 버튼 호환용.
    /// </summary>
    public void TryExpandEast()
    {
        TryExpandDirectional(KSM_ExpandDirection.East);
    }

    /// <summary>
    /// 특정 sourceRegion의 특정 방향 포트를 프리뷰한다.
    /// 
    /// 프리뷰는 "새로 열릴 targetRegion 1개"를 보여준다.
    /// 즉 예전처럼 전체 strip가 아니라,
    /// 실제 확장될 구역 하나만 보여준다.
    /// 
    /// 추가 수정 포인트:
    /// - 같은 targetRegion을 이미 프리뷰 중이면 다시 그리지 않는다.
    /// - hover 프리뷰 시 카메라 이동 여부는 옵션으로 분리한다.
    /// </summary>
    /// <param name="sourceRegion">기준 구역</param>
    /// <param name="direction">방향</param>
    public void PreviewExpansionAreaFromRegion(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null)
        {
            return;
        }

        if (!CanExpandFromRegion(sourceRegion, direction))
        {
            ClearExpansionPreview(moveCameraOnHoverPreview);
            return;
        }

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);

        // 이미 같은 target region을 프리뷰 중이면
        // 다시 지우고 다시 그리지 않는다.
        if (isPreviewActive && hasPreviewRegion && currentPreviewRegion == targetRegion)
        {
            return;
        }

        ClearExpansionPreviewVisualOnly();
        isPreviewActive = true;
        hasPreviewRegion = true;
        currentPreviewRegion = targetRegion;

        RectInt previewRect = GetRegionWorldRect(targetRegion);

        int centerX = previewRect.xMin + (previewRect.width / 2);
        int centerY = previewRect.yMin + (previewRect.height / 2);

        for (int x = previewRect.xMin; x < previewRect.xMax; x++)
        {
            for (int y = previewRect.yMin; y < previewRect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                bool isBorder = (x == previewRect.xMin || x == previewRect.xMax - 1 ||
                                 y == previewRect.yMin || y == previewRect.yMax - 1);

                bool isGuide;

                if (direction == KSM_ExpandDirection.North || direction == KSM_ExpandDirection.South)
                {
                    isGuide = (x == centerX);
                }
                else
                {
                    isGuide = (y == centerY);
                }

                Color targetColor = previewFillColor;

                if (isBorder)
                {
                    targetColor = previewBorderColor;
                }
                else if (isGuide)
                {
                    targetColor = previewGuideColor;
                }

                groundTilemap.SetTile(cell, baseTile);
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, targetColor);
                previewCells.Add(cell);
            }
        }

        RefreshTilesAroundRect(previewRect);

        // 프리뷰는 유지하되, hover 중 카메라가 움직이면
        // 포인터 판정과 버튼 재배치가 겹쳐 깜빡임이 생길 수 있으므로
        // 기본값은 false 권장.
        if (moveCameraOnHoverPreview)
        {
            UpdatePreviewCameraTarget(previewRect, false);
        }
    }

    /// <summary>
    /// 기존 호환용.
    /// 방향만 들어오면 가능한 첫 번째 포트를 찾아 프리뷰한다.
    /// </summary>
    /// <param name="direction">방향</param>
    public void PreviewExpansionArea(KSM_ExpandDirection direction)
    {
        foreach (Vector2Int region in openedRegions)
        {
            if (HasStructuralExpansionPort(region, direction))
            {
                PreviewExpansionAreaFromRegion(region, direction);
                return;
            }
        }

        ClearExpansionPreview(moveCameraOnHoverPreview);
    }

    /// <summary>
    /// 현재 확장 프리뷰를 제거한다.
    /// </summary>
    public void ClearExpansionPreview()
    {
        ClearExpansionPreview(moveCameraOnHoverPreview);
    }

    /// <summary>
    /// 프리뷰 제거 내부 처리.
    /// </summary>
    /// <param name="restoreCamera">카메라 원복 여부</param>
    private void ClearExpansionPreview(bool restoreCamera)
    {
        bool hadPreview = isPreviewActive;

        ClearExpansionPreviewVisualOnly();
        isPreviewActive = false;
        hasPreviewRegion = false;
        currentPreviewRegion = new Vector2Int(int.MinValue, int.MinValue);

        if (restoreCamera && hadPreview)
        {
            UpdateCameraTarget(false);
        }
    }

    /// <summary>
    /// 프리뷰 타일만 제거한다.
    /// 카메라는 건드리지 않는다.
    /// </summary>
    private void ClearExpansionPreviewVisualOnly()
    {
        if (groundTilemap == null)
        {
            previewCells.Clear();
            return;
        }

        if (previewCells.Count == 0)
        {
            return;
        }

        List<Vector3Int> oldPreviewCells = new List<Vector3Int>(previewCells);

        for (int i = 0; i < previewCells.Count; i++)
        {
            Vector3Int cell = previewCells[i];

            if (!IsWorldCellUnlocked(cell))
            {
                groundTilemap.SetTile(cell, null);
            }
            else
            {
                ResetTileAppearance(cell);
            }
        }

        previewCells.Clear();
        RefreshTilesAroundCells(oldPreviewCells);
    }

    /// <summary>
    /// 실제로 새 구역 하나를 연다.
    /// 
    /// 핵심:
    /// - openedRegions에 targetRegion 추가
    /// - bounding rectangle이 넓어지면 배열을 재할당하고 복사
    /// - 새 구역 타일만 깐다
    /// </summary>
    /// <param name="targetRegion">새로 열 구역</param>
    private void OpenRegion(Vector2Int targetRegion)
    {
        if (openedRegions.Contains(targetRegion))
        {
            return;
        }

        RectInt targetRect = GetRegionWorldRect(targetRegion);

        EnsureBoardBoundsIncludeRect(targetRect);

        openedRegions.Add(targetRegion);

        FillWorldRectWithBaseTile(targetRect);
        RefreshTilesAroundRect(targetRect);

        UpdateCameraTarget(false);

        if (PowerManager.Instance != null)
        {
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
            PowerManager.Instance.UpdateAllOutlines(boardData, width, height);
        }
    }

    /// <summary>
    /// 새 구역을 열 때 bounding rectangle이 넓어져야 하면
    /// boardData / buildingObjects를 새 크기로 재배치한다.
    /// </summary>
    /// <param name="includeRect">반드시 포함해야 할 새 구역 Rect</param>
    private void EnsureBoardBoundsIncludeRect(RectInt includeRect)
    {
        int oldOffsetX = currentOffset.x;
        int oldOffsetY = currentOffset.y;
        int oldWidth = width;
        int oldHeight = height;

        int newMinX = Mathf.Min(currentOffset.x, includeRect.xMin);
        int newMinY = Mathf.Min(currentOffset.y, includeRect.yMin);
        int newMaxX = Mathf.Max(currentOffset.x + width, includeRect.xMax);
        int newMaxY = Mathf.Max(currentOffset.y + height, includeRect.yMax);

        int newWidth = newMaxX - newMinX;
        int newHeight = newMaxY - newMinY;

        bool needsResize = (newMinX != currentOffset.x) ||
                           (newMinY != currentOffset.y) ||
                           (newWidth != width) ||
                           (newHeight != height);

        if (!needsResize)
        {
            return;
        }

        BlockData[,] newBoardData = new BlockData[newWidth, newHeight];
        GameObject[,] newBuildingObjects = new GameObject[newWidth, newHeight];

        for (int x = 0; x < oldWidth; x++)
        {
            for (int y = 0; y < oldHeight; y++)
            {
                int worldX = oldOffsetX + x;
                int worldY = oldOffsetY + y;

                int newX = worldX - newMinX;
                int newY = worldY - newMinY;

                newBoardData[newX, newY] = boardData[x, y];
                newBuildingObjects[newX, newY] = buildingObjects[x, y];
            }
        }

        currentOffset = new Vector2Int(newMinX, newMinY);
        width = newWidth;
        height = newHeight;
        boardData = newBoardData;
        buildingObjects = newBuildingObjects;
    }

    /// <summary>
    /// 구역 좌표를 실제 월드 셀 사각형 범위로 바꾼다.
    /// </summary>
    /// <param name="regionCoord">구역 좌표</param>
    /// <returns>구역 RectInt</returns>
    private RectInt GetRegionWorldRect(Vector2Int regionCoord)
    {
        int startX = regionCoord.x * RegionSize;
        int startY = regionCoord.y * RegionSize;
        return new RectInt(startX, startY, RegionSize, RegionSize);
    }

    /// <summary>
    /// 특정 월드 셀 사각형 범위를 baseTile로 채운다.
    /// </summary>
    /// <param name="rect">채울 영역</param>
    private void FillWorldRectWithBaseTile(RectInt rect)
    {
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                groundTilemap.SetTile(cell, baseTile);
                ResetTileAppearance(cell);
            }
        }
    }

    /// <summary>
    /// 특정 Rect 주변 1칸까지 포함해서 RefreshTile 한다.
    /// RuleTile / 오토타일 경계 깨짐 방지용.
    /// </summary>
    /// <param name="rect">기준 Rect</param>
    private void RefreshTilesAroundRect(RectInt rect)
    {
        HashSet<Vector3Int> refreshSet = new HashSet<Vector3Int>();

        for (int x = rect.xMin - 1; x <= rect.xMax; x++)
        {
            for (int y = rect.yMin - 1; y <= rect.yMax; y++)
            {
                refreshSet.Add(new Vector3Int(x, y, 0));
            }
        }

        RefreshTileSet(refreshSet);
    }

    /// <summary>
    /// 특정 셀 목록 주변 1칸까지 포함해서 RefreshTile 한다.
    /// 프리뷰 제거 시 경계 복구용.
    /// </summary>
    /// <param name="cells">기준 셀 목록</param>
    private void RefreshTilesAroundCells(List<Vector3Int> cells)
    {
        HashSet<Vector3Int> refreshSet = new HashSet<Vector3Int>();

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int baseCell = cells[i];

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    refreshSet.Add(new Vector3Int(baseCell.x + dx, baseCell.y + dy, 0));
                }
            }
        }

        RefreshTileSet(refreshSet);
    }

    /// <summary>
    /// HashSet에 담긴 셀들을 실제로 RefreshTile 한다.
    /// </summary>
    /// <param name="refreshSet">리프레시할 셀 집합</param>
    private void RefreshTileSet(HashSet<Vector3Int> refreshSet)
    {
        if (groundTilemap == null)
        {
            return;
        }

        foreach (Vector3Int cell in refreshSet)
        {
            groundTilemap.RefreshTile(cell);
        }
    }

    /// <summary>
    /// 셀의 타일 색상 / 플래그를 기본 상태로 되돌린다.
    /// </summary>
    /// <param name="cell">복구할 셀</param>
    private void ResetTileAppearance(Vector3Int cell)
    {
        groundTilemap.SetTileFlags(cell, TileFlags.None);
        groundTilemap.SetColor(cell, Color.white);
    }

    /// <summary>
    /// 현재 열린 모든 구역을 포함하는 bounding rectangle이 화면 안에 들어오도록 카메라를 맞춘다.
    /// 가로 / 세로를 모두 고려한다.
    /// </summary>
    /// <param name="instant">즉시 적용 여부</param>
    private void UpdateCameraTarget(bool instant)
    {
        if (mainCamera == null)
        {
            return;
        }

        float boardCenterX = currentOffset.x + (width * 0.5f);
        float boardCenterY = currentOffset.y + (height * 0.5f);

        float cameraAspect = mainCamera.aspect;

        float halfHeightFit = (height * 0.5f) + padding;
        float halfWidthFit = ((width * 0.5f) / Mathf.Max(0.01f, cameraAspect)) + padding;

        float targetSize = Mathf.Max(halfHeightFit, halfWidthFit);

        Vector3 targetPos = new Vector3(
            boardCenterX + (targetSize * offsetX * 0.5f),
            boardCenterY + (targetSize * offsetY * 0.5f),
            -10f
        );

        MoveCamera(targetPos, targetSize, instant);
    }

    /// <summary>
    /// 프리뷰 시 현재 보드 + 새 구역 프리뷰가 함께 보이도록 카메라를 조정한다.
    /// </summary>
    /// <param name="previewRect">프리뷰 대상 구역 Rect</param>
    /// <param name="instant">즉시 적용 여부</param>
    private void UpdatePreviewCameraTarget(RectInt previewRect, bool instant)
    {
        if (mainCamera == null)
        {
            return;
        }

        RectInt boardRect = new RectInt(currentOffset.x, currentOffset.y, width, height);

        int unionMinX = Mathf.Min(boardRect.xMin, previewRect.xMin);
        int unionMinY = Mathf.Min(boardRect.yMin, previewRect.yMin);
        int unionMaxX = Mathf.Max(boardRect.xMax, previewRect.xMax);
        int unionMaxY = Mathf.Max(boardRect.yMax, previewRect.yMax);

        int unionWidth = unionMaxX - unionMinX;
        int unionHeight = unionMaxY - unionMinY;

        float centerX = unionMinX + (unionWidth * 0.5f);
        float centerY = unionMinY + (unionHeight * 0.5f);

        float cameraAspect = mainCamera.aspect;

        float halfHeightFit = (unionHeight * 0.5f) + previewPadding;
        float halfWidthFit = ((unionWidth * 0.5f) / Mathf.Max(0.01f, cameraAspect)) + previewPadding;

        float targetSize = Mathf.Max(halfHeightFit, halfWidthFit);

        Vector3 targetPos = new Vector3(
            centerX + (targetSize * offsetX * 0.5f),
            centerY + (targetSize * offsetY * 0.5f),
            -10f
        );

        MoveCamera(targetPos, targetSize, instant);
    }

    /// <summary>
    /// 카메라 이동 공통 처리.
    /// </summary>
    /// <param name="targetPos">목표 위치</param>
    /// <param name="targetSize">목표 orthographic size</param>
    /// <param name="instant">즉시 적용 여부</param>
    private void MoveCamera(Vector3 targetPos, float targetSize, bool instant)
    {
        if (instant)
        {
            mainCamera.transform.position = targetPos;
            mainCamera.orthographicSize = targetSize;
        }
        else
        {
            StopAllCoroutines();
            StartCoroutine(SmoothCam(targetPos, targetSize));
        }
    }

    /// <summary>
    /// 카메라를 목표 위치 / 크기로 부드럽게 이동시킨다.
    /// </summary>
    /// <param name="targetPos">목표 위치</param>
    /// <param name="targetSize">목표 사이즈</param>
    /// <returns>코루틴</returns>
    private IEnumerator SmoothCam(Vector3 targetPos, float targetSize)
    {
        while (Mathf.Abs(mainCamera.orthographicSize - targetSize) > 0.01f ||
               Vector3.Distance(mainCamera.transform.position, targetPos) > 0.01f)
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, targetPos, Time.deltaTime * zoomSpeed);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, targetSize, Time.deltaTime * zoomSpeed);
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        mainCamera.orthographicSize = targetSize;
    }

    /// <summary>
    /// 드래그 중인 블록의 고스트 프리뷰를 만든다.
    /// 기존 DraggableBlock 흐름 유지.
    /// </summary>
    /// <param name="shapeCoords">shape 좌표 배열</param>
    /// <param name="prefab">고스트용 프리팹</param>
    /// <param name="tint">고스트 색상</param>
    /// <returns>고스트 부모 오브젝트</returns>
    public GameObject CreateModularPreview(Vector2Int[] shapeCoords, GameObject prefab, Color tint)
    {
        GameObject parent = new GameObject("ModularPreviewGhost");
        parent.transform.position = Vector3.zero;

        foreach (Vector2Int offset in shapeCoords)
        {
            Vector3 localPos = groundTilemap.layoutGrid.CellToLocal((Vector3Int)offset);
            GameObject part = Instantiate(prefab, localPos, Quaternion.identity);
            part.transform.SetParent(parent.transform, false);

            SpriteRenderer sr = part.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = tint;
                sr.sortingOrder = 10;
            }
        }

        return parent;
    }

    /// <summary>
    /// 상태 변경 이벤트 발행.
    /// </summary>
    private void RaiseExpandStateChanged()
    {
        OnExpandStateChanged?.Invoke();
    }

    /// <summary>
    /// 음수 좌표도 안전하게 처리하는 바닥 나눗셈.
    /// 구역 계산에 사용한다.
    /// </summary>
    /// <param name="value">피제수</param>
    /// <param name="divisor">제수</param>
    /// <returns>바닥 나눗셈 결과</returns>
    private int FloorDiv(int value, int divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("FloorDiv divisor is zero.");
        }

        int quotient = value / divisor;
        int remainder = value % divisor;

        if (remainder != 0 && ((value < 0) ^ (divisor < 0)))
        {
            quotient--;
        }

        return quotient;
    }
}