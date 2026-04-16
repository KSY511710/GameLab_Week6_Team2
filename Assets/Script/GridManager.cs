using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridManager : MonoBehaviour
{
    [Header("Board Settings")]
    public int StartGrid = 5;
    public int width;
    public int height;

    [Header("References")]
    public Tilemap groundTilemap;
    public TileBase baseTile;

    [Header("Camera Custom Layout")]
    public Camera mainCamera;
    public float zoomSpeed = 3f;
    public float padding = 2f;
    public float offsetX = 0f; // 0으로 하면 중앙 정렬

    [Header("Expansion Settings")]
    private Vector2Int currentOffset = Vector2Int.zero; // 배열 [0,0]이 타일맵의 어디인지 저장

    private int[,] boardData;
    private GameObject[,] buildingObjects;

    void Start()
    {
        width = StartGrid;
        height = StartGrid;
        boardData = new int[width, height];
        buildingObjects = new GameObject[width, height];

        // 초기 부지 생성
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                groundTilemap.SetTile(new Vector3Int(x, y, 0), baseTile);

        if (mainCamera == null) mainCamera = Camera.main;
        UpdateCameraTarget(true);
    }

    // 📌 [에러 해결] 이 메서드는 하나만 존재해야 합니다.
    public Vector3Int GetCellPositionFromMouse(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

    // 📌 타일맵 좌표를 배열 인덱스로 변환하는 핵심 헬퍼
    private Vector2Int TileToArrayIndex(int tileX, int tileY)
    {
        return new Vector2Int(tileX - currentOffset.x, tileY - currentOffset.y);
    }

    public bool CanPlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords)
    {
        foreach (var offset in shapeCoords)
        {
            Vector2Int arrayIdx = TileToArrayIndex(startCell.x + offset.x, startCell.y + offset.y);

            // 배열 범위 밖이거나 이미 건물이 있는 경우
            if (arrayIdx.x < 0 || arrayIdx.x >= width || arrayIdx.y < 0 || arrayIdx.y >= height) return false;
            if (boardData[arrayIdx.x, arrayIdx.y] != 0) return false;
        }
        return true;
    }

    public void PlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords, int blockValue, GameObject prefab)
    {
        GameObject buildingParent = new GameObject("MultiCell_Building");
        buildingParent.transform.position = groundTilemap.GetCellCenterWorld(startCell);

        foreach (var offset in shapeCoords)
        {
            int tx = startCell.x + offset.x;
            int ty = startCell.y + offset.y;
            Vector2Int arrayIdx = TileToArrayIndex(tx, ty);

            Vector3 cellWorldPos = groundTilemap.GetCellCenterWorld(new Vector3Int(tx, ty, 0));
            GameObject cellPart = Instantiate(prefab, cellWorldPos, Quaternion.identity);
            cellPart.transform.SetParent(buildingParent.transform);

            // 데이터 기록 (오프셋 적용된 인덱스 사용)
            boardData[arrayIdx.x, arrayIdx.y] = blockValue;
            buildingObjects[arrayIdx.x, arrayIdx.y] = buildingParent;
        }

        // 전력 계산 요청
        if (PowerManager.Instance != null)
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
    }

    public void TryExpandBoard()
    {
        int cost = ResourceManager.Instance.expandCost;

        // 1. 전기가 충분해서 지불에 성공했다면
        if (ResourceManager.Instance.SpendElectric(cost))
        {
            // 2. 실제 보드 확장 로직 실행
            ExpandBoard();

            // 3. 다음 확장 비용 증가
            ResourceManager.Instance.IncreaseExpandCost();
        }
    }

    public void ExpandBoard()
    {
        int oldW = width;
        int oldH = height;
        width += 2;
        height += 2;

        int[,] newData = new int[width, height];
        GameObject[,] newObjs = new GameObject[width, height];

        // 📌 기존 데이터를 새 배열의 [1, 1] 위치로 옮기기 (상하좌우 확장)
        for (int x = 0; x < oldW; x++)
        {
            for (int y = 0; y < oldH; y++)
            {
                newData[x + 1, y + 1] = boardData[x, y];
                newObjs[x + 1, y + 1] = buildingObjects[x, y];
            }
        }

        boardData = newData;
        buildingObjects = newObjs;

        // 오프셋을 (-1, -1) 만큼 이동
        currentOffset -= new Vector2Int(1, 1);

        // 새로운 테두리 타일 그리기
        for (int y = 0; y < height; y++)
        {
            groundTilemap.SetTile(new Vector3Int(currentOffset.x, currentOffset.y + y, 0), baseTile);
            groundTilemap.SetTile(new Vector3Int(currentOffset.x + width - 1, currentOffset.y + y, 0), baseTile);
        }
        for (int x = 0; x < width; x++)
        {
            groundTilemap.SetTile(new Vector3Int(currentOffset.x + x, currentOffset.y, 0), baseTile);
            groundTilemap.SetTile(new Vector3Int(currentOffset.x + x, currentOffset.y + height - 1, 0), baseTile);
        }

        UpdateCameraTarget(false);

        // 확장 후 전력 재계산 (덩어리 개수가 변하지 않아도 합계 확인용)
        if (PowerManager.Instance != null)
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
    }

    private void UpdateCameraTarget(bool instant)
    {
        float targetSize = (height / 2f) + padding;
        // 중앙 정렬을 위해 currentOffset을 반영한 중심점 계산
        float centerX = currentOffset.x + (width / 2f);
        float centerY = currentOffset.y + (height / 2f);

        Vector3 targetPos = new Vector3(centerX + (targetSize * offsetX * 0.5f), centerY, -10f);

        if (instant) { mainCamera.transform.position = targetPos; mainCamera.orthographicSize = targetSize; }
        else { StopAllCoroutines(); StartCoroutine(SmoothCam(targetPos, targetSize)); }
    }

    private IEnumerator SmoothCam(Vector3 pos, float size)
    {
        while (Mathf.Abs(mainCamera.orthographicSize - size) > 0.01f)
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, pos, Time.deltaTime * zoomSpeed);
            mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, size, Time.deltaTime * zoomSpeed);
            yield return null;
        }
    }

    public GameObject CreateModularPreview(Vector2Int[] shapeCoords, GameObject prefab, Color tint)
    {
        GameObject parent = new GameObject("ModularPreviewGhost");
        parent.transform.position = Vector3.zero;

        foreach (var offset in shapeCoords)
        {
            Vector3 localPos = groundTilemap.layoutGrid.CellToLocal((Vector3Int)offset);
            GameObject part = Instantiate(prefab, localPos, Quaternion.identity);
            part.transform.SetParent(parent.transform, false);

            var sr = part.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.color = tint; sr.sortingOrder = 10; }
        }
        return parent;
    }
}