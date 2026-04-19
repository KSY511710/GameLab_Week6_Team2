using System.Collections.Generic;
using Special.Data;
using Special.Effects;
using Special.Runtime;
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
    /// 드래그 중엔 효과 범위를 ScopeOverlayRenderer 로 미리 보여준다.
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

        [Header("Drag Scope Preview")]
        [Tooltip("드래그 중 효과 범위 오버레이에 사용할 1셀 스프라이트. 비우면 1x1 흰색 자동 생성.")]
        [SerializeField] private Sprite dragOverlaySprite;
        [Tooltip("오버레이 SpriteRenderer 의 정렬 순서. 설치 연출 오버레이(50)보다 아래가 보기 편함.")]
        [SerializeField] private int dragOverlaySortingOrder = 49;
        [Tooltip("효과에 overlayColor 가 설정돼 있지 않거나 여러 효과 중 선택 실패 시 사용할 기본색.")]
        [SerializeField] private Color dragOverlayFallbackColor = new Color(1f, 0.85f, 0.2f, 0.28f);

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

        private ScopeOverlayRenderer dragOverlay;
        private readonly List<Vector2Int> dragScopeScratch = new List<Vector2Int>();
        private readonly HashSet<Vector2Int> dragScopeSeen = new HashSet<Vector2Int>();
        private Vector2Int lastPreviewAnchorArray = new Vector2Int(int.MinValue, int.MinValue);

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

            dragOverlay = new ScopeOverlayRenderer(transform, dragOverlaySprite, dragOverlaySortingOrder);
            lastPreviewAnchorArray = new Vector2Int(int.MinValue, int.MinValue);

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

                Vector2Int anchorArr = gridManager.WorldCellToArrayIndex(cellPos);
                if (anchorArr != lastPreviewAnchorArray)
                {
                    lastPreviewAnchorArray = anchorArr;
                    RefreshScopePreview(anchorArr);
                }
            }
        }

        private void UpdateGhostValidity(Vector3Int cellPos)
        {
            if (previewGhost == null || ghostRenderers == null) return;
            bool canPlace = gridManager.CanPlaceShape(cellPos, definition.shapeCoords, definition);
            Color tint = canPlace ? validTint : invalidTint;
            foreach (var sr in ghostRenderers) if (sr != null) sr.color = tint;
        }

        /// <summary>
        /// 앵커 셀 기준 효과 범위를 누적해 오버레이에 반영. 같은 tile 안에서 마우스 미세 이동 시
        /// OnDrag 상위에서 anchorArr 동일로 조기 탈출 — 여긴 셀 경계 교차 시만 호출된다.
        /// customEffectPrefabs 는 MonoBehaviour 로 설치 시에만 인스턴스화되므로 드래그 중엔 미리보기 불가 — 의도적.
        /// </summary>
        private void RefreshScopePreview(Vector2Int anchorArr)
        {
            if (dragOverlay == null) return;

            EffectAsset[] effects = definition.effectAssets;
            if (effects == null || effects.Length == 0)
            {
                dragOverlay.Hide();
                return;
            }

            SpecialBlockInstance preview = SpecialBlockInstance.CreateDragPreview(definition, anchorArr, gridManager.width, gridManager.height);
            if (preview == null)
            {
                dragOverlay.Hide();
                return;
            }

            dragScopeScratch.Clear();
            dragScopeSeen.Clear();
            Color overlayColor = dragOverlayFallbackColor;
            bool colorPicked = false;

            for (int i = 0; i < effects.Length; i++)
            {
                EffectAsset eff = effects[i];
                if (eff == null) continue;
                // 보드 전체에 깔리는 scope 는 고스트를 가리므로 텍스트(설치 연출)로만 표현.
                if (eff.Scope == EffectScope.Global || eff.Scope == EffectScope.Zone) continue;

                List<Vector2Int> cells = eff.BuildDragPreviewCells(preview);
                if (cells == null) continue;
                for (int c = 0; c < cells.Count; c++)
                    if (dragScopeSeen.Add(cells[c])) dragScopeScratch.Add(cells[c]);

                if (!colorPicked) { overlayColor = eff.OverlayColor; colorPicked = true; }
            }

            if (dragScopeScratch.Count == 0)
            {
                dragOverlay.Hide();
                return;
            }

            dragOverlay.Show(dragScopeScratch, overlayColor, gridManager);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;

            if (dragOverlay != null)
            {
                dragOverlay.Hide();
                dragOverlay = null;
            }

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

        private void OnDisable()
        {
            if (dragOverlay != null)
            {
                dragOverlay.Hide();
                dragOverlay = null;
            }
        }
    }
}
