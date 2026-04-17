using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Block Attributes")]
    public int colorID = 1; // 인스펙터에서 1(빨강), 2(파랑) 등으로 설정
    public int shapeID = 0; // 모양이 없다면 0으로 두세요.

    public GameObject blockPrefab;

    [Header("Inventory Settings")]
    public int blockCount = 0;
    public TextMeshProUGUI countText;

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

        UpdateUI();
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

    public void AddBlock()
    {
        blockCount++;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (countText != null) countText.text = $"x {blockCount}";

        if (blockCount > 0) img.color = new Color(1f, 1f, 1f, 1f);
        else img.color = new Color(1f, 1f, 1f, 0.5f);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (blockCount <= 0) return;

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
            gridManager.PlaceShape(cellPos, shapeCoords, colorID, shapeID, blockPrefab);

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