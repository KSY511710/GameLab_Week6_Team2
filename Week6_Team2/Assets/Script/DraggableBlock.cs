using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro; // 📌 UI 텍스트 사용을 위해 추가

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Block Info")]
    public int blockValue = 1;
    public GameObject blockPrefab;

    [Header("Inventory Settings")]
    public int blockCount = 0; // 📌 현재 보유한 블록 개수
    public TextMeshProUGUI countText; // 📌 "x0", "x1"을 표시할 텍스트

    [Header("Shape Settings")]
    public Vector2Int[] shapeCoords = { new Vector2Int(0, 0) };

    private Color validTint = new Color(0, 1, 0, 0.5f);
    private Color invalidTint = new Color(1, 0, 0, 0.5f);

    private Vector3 startPos;
    private Vector2Int[] originalShape;
    private Quaternion originalRotation;
    private GridManager gridManager;
    private Camera mainCam;
    private Image img;
    private bool isDragging = false;

    private GameObject previewGhost;
    private SpriteRenderer[] ghostRenderers;
    private Vector3Int lastCellPos;

    void Start()
    {
        gridManager = Object.FindAnyObjectByType<GridManager>();
        mainCam = Camera.main;
        img = GetComponent<Image>();

        originalShape = (Vector2Int[])shapeCoords.Clone();
        originalRotation = transform.rotation;

        UpdateUI(); // 시작할 때 텍스트 및 투명도 갱신
    }

    void Update()
    {
        if (isDragging && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RotateShape();
        }
    }

    void RotateShape()
    {
        for (int i = 0; i < shapeCoords.Length; i++)
        {
            int x = shapeCoords[i].x;
            int y = shapeCoords[i].y;
            shapeCoords[i] = new Vector2Int(y, -x);
        }

        transform.Rotate(0, 0, -90);
        if (previewGhost != null)
        {
            previewGhost.transform.Rotate(0, 0, -90);
            if (isDragging) UpdateGhostValidity(lastCellPos);
        }
    }

    // 📌 [추가] 뽑기에서 당첨되면 개수 증가
    public void AddBlock()
    {
        blockCount++;
        UpdateUI();
    }

    // 📌 [추가] UI 갱신 함수 (개수가 0이면 반투명하게)
    private void UpdateUI()
    {
        if (countText != null) countText.text = $"x {blockCount}";

        // 개수가 0이면 반투명, 있으면 불투명
        if (blockCount > 0) img.color = new Color(1f, 1f, 1f, 1f);
        else img.color = new Color(1f, 1f, 1f, 0.5f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (blockCount <= 0) return; // 📌 개수가 0이면 드래그 불가

        isDragging = true;
        startPos = transform.position;
        img.enabled = false;

        previewGhost = gridManager.CreateModularPreview(shapeCoords, blockPrefab, invalidTint);
        ghostRenderers = previewGhost.GetComponentsInChildren<SpriteRenderer>();

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (blockCount <= 0 || previewGhost == null) return;

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3 correctedPos = new Vector3(mouseWorldPos.x, mouseWorldPos.y, 0);
        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(correctedPos);

        Vector3 snapPos = gridManager.groundTilemap.GetCellCenterWorld(cellPos);
        previewGhost.transform.position = snapPos;

        if (cellPos != lastCellPos)
        {
            lastCellPos = cellPos;
            UpdateGhostValidity(cellPos);
        }
    }

    private void UpdateGhostValidity(Vector3Int cellPos)
    {
        if (previewGhost == null || ghostRenderers == null) return;

        bool canPlace = gridManager.CanPlaceShape(cellPos, shapeCoords);
        Color targetTint = canPlace ? validTint : invalidTint;

        foreach (var sr in ghostRenderers)
        {
            if (sr != null) sr.color = targetTint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (blockCount <= 0) return;

        isDragging = false;

        if (previewGhost != null)
        {
            Destroy(previewGhost);
            previewGhost = null;
            ghostRenderers = null;
        }

        Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;
        Vector3 correctedPos = new Vector3(worldPos.x, worldPos.y, 0);
        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(correctedPos);

        if (gridManager.CanPlaceShape(cellPos, shapeCoords))
        {
            gridManager.PlaceShape(cellPos, shapeCoords, blockValue, blockPrefab);

            // 📌 [핵심] 설치 성공 시 개수 감소
            blockCount--;
            ResetToOriginalState();
            img.enabled = true;
            UpdateUI();
        }
        else
        {
            ResetToOriginalState();
            img.enabled = true;
        }
    }

    void ResetToOriginalState()
    {
        shapeCoords = (Vector2Int[])originalShape.Clone();
        transform.rotation = originalRotation;
        transform.position = startPos;
    }
}