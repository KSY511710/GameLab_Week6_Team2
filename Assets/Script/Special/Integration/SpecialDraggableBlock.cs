using Special.Data;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Special.Integration
{
    /// <summary>
    /// 특수 블럭 전용 인벤토리 슬롯. SpecialGachaController.OnSpecialBlockDrawn 을 구독해
    /// 본인의 definition.id 와 일치하면 개수 증가. 드래그&드롭으로 GridManager.PlaceShape 에
    /// specialDef 를 전달한다.
    /// 일반 DraggableBlock 의 좁은 동작 계약(색/기호/크기 매칭)과 분리해 병렬로 돌린다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpecialDraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Definition")]
        public SpecialBlockDefinition definition;

        [Tooltip("보드에 실제로 소환될 1셀 Sprite 프리팹. 일반 블럭과 동일한 프리팹 재사용 가능.")]
        public GameObject blockPrefab;

        [Header("Inventory")]
        public int blockCount = 0;
        public TextMeshProUGUI countText;

        private Color validTint = new Color(0f, 1f, 0f, 0.5f);
        private Color invalidTint = new Color(1f, 0f, 0f, 0.5f);

        private Vector3 startPos;
        private Image img;
        private Camera mainCam;
        private GridManager gridManager;

        private GameObject previewGhost;
        private SpriteRenderer[] ghostRenderers;
        private Vector3Int lastCellPos;
        private bool isDragging;

        private void OnEnable()
        {
            SpecialGachaController.OnSpecialBlockDrawn += HandleSpecialDrawn;
        }

        private void OnDisable()
        {
            SpecialGachaController.OnSpecialBlockDrawn -= HandleSpecialDrawn;
        }

        private void Start()
        {
            img = GetComponent<Image>();
            mainCam = Camera.main;
            gridManager = Object.FindFirstObjectByType<GridManager>();
            UpdateUI();
        }

        private void HandleSpecialDrawn(SpecialBlockDefinition def)
        {
            if (definition == null || def == null) return;
            if (definition.id == def.id)
            {
                blockCount++;
                UpdateUI();
            }
        }

        private void UpdateUI()
        {
            if (countText != null) countText.text = $"x {blockCount}";
            if (img == null) return;
            Color c = img.color;
            c.a = blockCount > 0 ? 1f : 0.5f;
            img.color = c;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (blockCount <= 0 || definition == null || gridManager == null) return;
            isDragging = true;
            startPos = transform.position;
            img.enabled = false;

            previewGhost = gridManager.CreateModularPreview(definition.shapeCoords, blockPrefab, invalidTint);
            ghostRenderers = previewGhost.GetComponentsInChildren<SpriteRenderer>();
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || previewGhost == null) return;

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector3Int cellPos = gridManager.GetCellPositionFromMouse(mouseWorld);
            previewGhost.transform.position = gridManager.groundTilemap.GetCellCenterWorld(cellPos);

            if (cellPos != lastCellPos)
            {
                lastCellPos = cellPos;
                UpdateGhostValidity(cellPos);
            }
        }

        private void UpdateGhostValidity(Vector3Int cellPos)
        {
            if (previewGhost == null || ghostRenderers == null) return;
            bool canPlace = gridManager.CanPlaceShape(cellPos, definition.shapeCoords, definition);
            Color tint = canPlace ? validTint : invalidTint;
            foreach (var sr in ghostRenderers) if (sr != null) sr.color = tint;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;

            if (previewGhost != null)
            {
                Destroy(previewGhost);
                previewGhost = null;
                ghostRenderers = null;
            }

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0f;
            Vector3Int cellPos = gridManager.GetCellPositionFromMouse(mouseWorld);

            if (gridManager.CanPlaceShape(cellPos, definition.shapeCoords, definition))
            {
                int colorID = definition.colorBinding == SpecialColorBinding.Single ? definition.ResolveSingleColorID() : 0;
                // MultiPrimary 는 SpecialBlockResolver 가 그룹화 시점에 확정하므로 설치 시점은 0.
                gridManager.PlaceShape(cellPos, definition.shapeCoords, colorID, definition.uniqueShapeId, blockPrefab, definition);
                blockCount--;
                UpdateUI();
            }

            transform.position = startPos;
            img.enabled = true;
        }
    }
}
