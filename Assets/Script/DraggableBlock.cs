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

    // 🌟 [수정됨] 단일 프리팹 대신 중앙용, 자투리용 2개를 받습니다!
    [Header("Visual Prefabs")]
    public GameObject centerBlockPrefab;
    public GameObject sideBlockPrefab;

    [Header("Shape Settings")]
    public Vector2Int[] shapeCoords = { new Vector2Int(0, 0) };

    private Color validTint = new Color(1f, 1f, 1f, 0.5f);
    private Color invalidTint = new Color(0.2f, 0.2f, 0.2f, 0.7f);

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
    [Header("Block Type Settings")]
    public int placementCost = 10;

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

        img = GetComponent<Image>();
        if (c == KSM_GATCHA.CompanyColor.Red) img.color = new Color(1f, 0.2f, 0.2f);
        else if (c == KSM_GATCHA.CompanyColor.Blue) img.color = new Color(0.2f, 0.4f, 1f);
        else if (c == KSM_GATCHA.CompanyColor.Yellow) img.color = new Color(0.2f, 1f, 0.2f);

        if (size == 1)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0) };
        else if (size == 2)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        else if (size == 3)
            shapeCoords = new Vector2Int[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };

        originalShape = (Vector2Int[])shapeCoords.Clone();
        originalRotation = transform.rotation;
    }

    void Update()
    {
        if (isDragging && Keyboard.current.rKey.wasPressedThisFrame) RotateShape();
    }

    void RotateShape()
    {
        // 1. 논리적인 좌표 회전
        for (int i = 0; i < shapeCoords.Length; i++)
        {
            int x = shapeCoords[i].x;
            int y = shapeCoords[i].y;
            shapeCoords[i] = new Vector2Int(y, -x);
        }

        // 2. 부모 오브젝트 회전
        transform.Rotate(0, 0, -90); // 인벤토리 아이콘 회전

        if (previewGhost != null)
        {
            previewGhost.transform.Rotate(0, 0, -90); // 유령 전체 회전

            // 🌟 [추가] 중앙 블록을 찾아서 회전값을 세계 좌표 기준으로 리셋!
            Transform center = previewGhost.transform.Find("CenterPiece");
            if (center != null)
            {
                // 부모가 어떻게 돌아가든 중앙 블록은 항상 0도를 유지합니다.
                center.rotation = Quaternion.identity;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        img.enabled = false;

        // 🌟 [수정됨] 유령을 만들 때 프리팹 2개를 모두 전달합니다!
        previewGhost = gridManager.CreateModularPreview(shapeCoords, centerBlockPrefab, sideBlockPrefab, invalidTint);
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

        if (previewGhost != null)
        {
            Destroy(previewGhost);
            previewGhost = null;
            ghostRenderers = null;
        }

        Vector3 worldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        worldPos.z = 0;
        Vector3Int cellPos = gridManager.GetCellPositionFromMouse(worldPos);

        if (gridManager.CanPlaceShape(cellPos, shapeCoords))
        {
            // 🌟 1. 돈 검사 및 지불 (특수 블록 눈치 볼 필요 없이 무조건 실행!)
            if (!ResourceManager.Instance.TryPayForBasicDraw())
            {
                Debug.Log("돈이 부족해서 블록을 설치할 수 없습니다!");
                ResetToOriginalState();
                img.enabled = true;
                return;
            }

            // 2. 블록 설치!
            gridManager.PlaceShape(cellPos, shapeCoords, (int)companyColor, (int)symbolType, centerBlockPrefab, sideBlockPrefab);

            if (InventoryManager.Instance != null)
            {
                transform.SetParent(null); // 슬롯에서 빠져나오기

                InventoryManager.Instance.ProcessGravityAndRefill();
            }

            Destroy(gameObject);
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
    }
}