using Special.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Special.Integration
{
    /// <summary>
    /// 특수 블럭 인벤토리 UI. instance-per-draw 모델 — 뽑힐 때마다 SpecialGachaController 가
    /// InventoryManager.TryAddBlock 경유로 이 프리팹을 가방 패널에 1개 인스턴스화한다.
    /// 드래그&드롭으로 GridManager.PlaceShape 에 specialDef 를 전달하고, 배치 성공 시
    /// InventoryManager 용량을 반납한 뒤 자신을 파괴한다.
    /// 일반 DraggableBlock 의 좁은 동작 계약(색/기호/크기 매칭)과 분리해 병렬로 돌린다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpecialDraggableBlock : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Definition")]
        public SpecialBlockDefinition definition;

        [Tooltip("Anchor(0,0) 셀에 소환될 중앙 프리팹. 일반 블럭의 centerBlockPrefab 과 동일 슬롯.")]
        public GameObject centerBlockPrefab;

        [Tooltip("Anchor 외 모든 칸에 소환될 자투리 프리팹. 모든 셀에 같은 룩이 필요하면 centerBlockPrefab 과 동일하게 지정.")]
        public GameObject sideBlockPrefab;

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

        private void Start()
        {
            img = GetComponent<Image>();
            mainCam = Camera.main;
            gridManager = Object.FindFirstObjectByType<GridManager>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (definition == null || gridManager == null) return;
            if (centerBlockPrefab == null) return;
            isDragging = true;
            startPos = transform.position;
            img.enabled = false;

            // 자투리 프리팹이 비어 있으면 중앙 프리팹으로 대체. 단일 룩 특수 블럭의 흔한 케이스.
            GameObject sideFallback = sideBlockPrefab != null ? sideBlockPrefab : centerBlockPrefab;
            previewGhost = gridManager.CreateModularPreview(definition.shapeCoords, centerBlockPrefab, sideFallback, invalidTint);
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
                GameObject sideFallback = sideBlockPrefab != null ? sideBlockPrefab : centerBlockPrefab;
                gridManager.PlaceShape(cellPos, definition.shapeCoords, colorID, definition.uniqueShapeId, centerBlockPrefab, sideFallback, definition);

                // 일반 DraggableBlock 과 동일하게 공통 가방 파이프라인으로 용량 반납 후 자신을 파괴.
                if (InventoryManager.Instance != null) InventoryManager.Instance.OnBlockUsed();
                Destroy(gameObject);
                return;
            }

            // 배치 실패: 원 위치로 복귀하고 이미지 복원.
            transform.position = startPos;
            img.enabled = true;
        }
    }
}
