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
    public float offsetX = 0f;

    [Header("Expansion Settings")]
    private Vector2Int currentOffset = Vector2Int.zero;

    private BlockData[,] boardData;
    private GameObject[,] buildingObjects;

    void Start()
    {
        width = StartGrid;
        height = StartGrid;

        boardData = new BlockData[width, height];
        buildingObjects = new GameObject[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                groundTilemap.SetTile(new Vector3Int(x, y, 0), baseTile);

        if (mainCamera == null) mainCamera = Camera.main;
        UpdateCameraTarget(true);
    }

    public Vector3Int GetCellPositionFromMouse(Vector3 worldPos)
    {
        return groundTilemap.WorldToCell(worldPos);
    }

    private Vector2Int TileToArrayIndex(int tileX, int tileY)
    {
        return new Vector2Int(tileX - currentOffset.x, tileY - currentOffset.y);
    }

    public bool CanPlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords)
    {
        foreach (var offset in shapeCoords)
        {
            Vector2Int arrayIdx = TileToArrayIndex(startCell.x + offset.x, startCell.y + offset.y);

            if (arrayIdx.x < 0 || arrayIdx.x >= width || arrayIdx.y < 0 || arrayIdx.y >= height) return false;

            // 빈 칸이 아닌지 검사
            if (boardData[arrayIdx.x, arrayIdx.y] != null && boardData[arrayIdx.x, arrayIdx.y].attribute.colorID > 0) return false;
        }
        return true;
    }

    // 📌 colorID와 shapeID를 모두 받아서 저장합니다.
    public void PlaceShape(Vector3Int startCell, Vector2Int[] shapeCoords, int colorID, int shapeID, GameObject prefab)
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

            boardData[arrayIdx.x, arrayIdx.y] = new BlockData
            {
                attribute = new BlockAttribute(colorID, shapeID),
                isGrouped = false,
                groupID = 0
            };

            buildingObjects[arrayIdx.x, arrayIdx.y] = buildingParent;
        }

        // 설치 후 전력 계산 및 그룹화 요청
        if (PowerManager.Instance != null)
        {
            PowerManager.Instance.CheckAndFormGroups(boardData, width, height);
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
        }
    }

    public void TryExpandBoard()
    {
        if (ResourceManager.Instance.TryPayForExpand())
        {
            ExpandBoard();
        }
    }

    public void ExpandBoard()
    {
        int oldW = width;
        int oldH = height;
        width += 2;
        height += 2;

        BlockData[,] newData = new BlockData[width, height];
        GameObject[,] newObjs = new GameObject[width, height];

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
        currentOffset -= new Vector2Int(1, 1);

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

        if (PowerManager.Instance != null)
            PowerManager.Instance.CalculateTotalPower(boardData, width, height);
    }

    private void UpdateCameraTarget(bool instant)
    {
        float targetSize = (height / 2f) + padding;
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