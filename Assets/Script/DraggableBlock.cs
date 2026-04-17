using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Block Attributes")]
    public KSM_GATCHA.CompanyColor companyColor = KSM_GATCHA.CompanyColor.Red;
    public KSM_GATCHA.BlockSymbolType symbolType = KSM_GATCHA.BlockSymbolType.Symbol01;
    [Range(1, 3)] public int blockSize = 1;

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
    private void OnEnable()
    {
        GachaConnector.OnBlockDrawn += CheckAndAddBlock;
        Debug.Log("구독성공");
    }

    private void OnDisable()
    {
        GachaConnector.OnBlockDrawn -= CheckAndAddBlock;
    }
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

        Color currentColor = img.color;

        if (blockCount > 0)
        {
            currentColor.a = 1f;
        }
        else
        {
            currentColor.a = 0.5f;
        }

        // 3. 투명도만 바뀐 색상을 다시 이미지에 쏙 넣습니다.
        img.color = currentColor;
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
            gridManager.PlaceShape(cellPos, shapeCoords, (int)companyColor, (int)symbolType, blockPrefab);

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
    private void CheckAndAddBlock(KSM_GATCHA.CompanyColor drawnCompany, KSM_GATCHA.BlockSymbolType drawnSymbol, int drawnSize)
    {
        // SO 대신 내 스크립트에 있는 변수들과 직접 비교합니다!
        if (this.companyColor == drawnCompany &&
            this.symbolType == drawnSymbol &&
            this.blockSize == drawnSize)
        {
            // 색상, 기호, 크기가 모두 일치하면 개수 증가!
            AddBlock();
        }
    }
}