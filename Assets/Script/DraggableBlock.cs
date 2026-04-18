using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class DraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Block Attributes")]
    public KSM_GATCHA.CompanyColor companyColor = KSM_GATCHA.CompanyColor.Red;
    public KSM_GATCHA.BlockSymbolType symbolType = KSM_GATCHA.BlockSymbolType.Symbol01;
    [Range(2, 4)] public int blockSize = 2;

    public GameObject blockPrefab;

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

        if (shapeCoords != null)
        {
            originalShape = (Vector2Int[])shapeCoords.Clone();
            originalRotation = transform.rotation;
        }
    }
    public void InitializeBlock(KSM_GATCHA.CompanyColor c, KSM_GATCHA.BlockSymbolType s, int size)
    {
        this.companyColor = c;
        this.symbolType = s;
        this.blockSize = size;

        // 1. 색상 자동 변경 (이미지 색 바꾸기)
        img = GetComponent<Image>();
        if (c == KSM_GATCHA.CompanyColor.Red) img.color = new Color(1f, 0.2f, 0.2f);
        else if (c == KSM_GATCHA.CompanyColor.Blue) img.color = new Color(0.2f, 0.4f, 1f);
        else if (c == KSM_GATCHA.CompanyColor.Yellow) img.color = new Color(1f, 0.9f, 0.2f);
        // (기호 이미지가 있다면 여기서 스프라이트도 변경!)

        // 2. 크기(size)에 맞춰서 모양(shapeCoords) 자동 세팅!
        if (size == 1)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0) }; // 1칸
        else if (size == 2)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }; // 2칸 일자
        else if (size == 3)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) }; // 3칸 ㄱ자 (원하는 대로 세팅)

        // 3. 변경된 모양과 회전값을 백업해둡니다.
        originalShape = (Vector2Int[])shapeCoords.Clone();
        originalRotation = transform.rotation;
    }

    void Update()
    {
        if (isDragging && Keyboard.current.rKey.wasPressedThisFrame) RotateShape();
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        img.enabled = false;

        previewGhost = gridManager.CreateModularPreview(shapeCoords, blockPrefab, invalidTint);
        ghostRenderers = previewGhost.GetComponentsInChildren<SpriteRenderer>();

        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (previewGhost == null) return;

        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;

        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(mouseWorldPos);
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

        foreach (var sr in ghostRenderers) if (sr != null) sr.color = targetTint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;

        // 유령(Ghost) 파괴
        if (previewGhost != null)
        {
            Destroy(previewGhost);
            previewGhost = null;
            ghostRenderers = null;
        }

        Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;
        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(worldPos);

        // 🌟 설치 성공 여부 판단
        if (gridManager.CanPlaceShape(cellPos, shapeCoords))
        {
            // [설치 성공!] 보드판에 진짜 블록을 깔아줍니다.
            gridManager.PlaceShape(cellPos, shapeCoords, (int)companyColor, (int)symbolType, blockPrefab);

            if (InventoryManager.Instance != null) InventoryManager.Instance.OnBlockUsed();

            // 🌟 성공했을 때만 나 자신을 영원히 파괴합니다! 
            Destroy(gameObject);
        }
        else
        {
            // [설치 실패!] 원래 모양으로 복구합니다.
            ResetToOriginalState();

            // 🌟 인벤토리에 숨어있던 내 그림을 다시 켭니다. (위치는 Layout Group이 원래 자리에 잘 잡아두고 있습니다)
            img.enabled = true;
        }
    }

    void ResetToOriginalState()
    {
        shapeCoords = (Vector2Int[])originalShape.Clone();
        transform.rotation = originalRotation;
    }
}