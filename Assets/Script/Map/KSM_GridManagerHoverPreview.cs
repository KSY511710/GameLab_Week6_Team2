using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// GridManager의 기존 확장 시스템은 유지한 채,
/// "최대 확장 범위 전체에 기본 바닥 타일을 먼저 깔고,
/// 그 위에 region 단위 검은 반투명 오버레이를 덮는" partial 파일.
///
/// 핵심 목적:
/// 1. locked region도 먼저 baseTile을 전부 깔아서 전체 맵 형태를 보여준다.
/// 2. locked region 위에는 검은 반투명 오버레이를 별도 GameObject로 올린다.
/// 3. hover 중인 현재 구매 가능 region만 오버레이를 더 밝게 만든다.
/// 4. unlock 되면 해당 region 오버레이를 제거해서 아래 흰 타일이 드러나게 한다.
/// 5. GridManager 본체는 수정하지 않는다.
/// </summary>
public partial class GridManager
{
    [Header("KSM Overlay Visual")]
    [Tooltip("닫힌 region 위에 올릴 기본 검은 반투명 오버레이 색상.")]
    [SerializeField] private Color ksmLockedOverlayColor = new Color(0f, 0f, 0f, 0.78f);

    [Tooltip("hover 중인 현재 구매 가능 region 오버레이 색상.")]
    [SerializeField] private Color ksmHoverOverlayColor = new Color(0f, 0f, 0f, 0.42f);

    [Tooltip("돈 부족 상태에서 hover 중인 region 오버레이 색상.")]
    [SerializeField] private Color ksmHoverOverlayUnaffordableColor = new Color(0f, 0f, 0f, 0.58f);

    [Tooltip("hover 중 region 외곽선 최소 색상.")]
    [SerializeField] private Color ksmHoverBorderPulseMinColor = new Color(1.00f, 0.78f, 0.08f, 0.95f);

    [Tooltip("hover 중 region 외곽선 최대 색상.")]
    [SerializeField] private Color ksmHoverBorderPulseMaxColor = new Color(1.00f, 0.96f, 0.28f, 1.00f);

    [Tooltip("hover 중 외곽선 발광 속도.")]
    [SerializeField, Min(0.1f)] private float ksmHoverBorderPulseSpeed = 5.5f;

    [Tooltip("오버레이를 타일보다 카메라 쪽으로 조금 더 앞으로 띄울 z 오프셋.")]
    [SerializeField] private float ksmOverlayZOffset = -0.10f;

    [Tooltip("오버레이 외곽선 두께(월드 단위).")]
    [SerializeField, Min(0.01f)] private float ksmBorderThicknessWorld = 0.08f;

    [Tooltip("Tilemap sorting order보다 얼마나 위에 오버레이를 띄울지.")]
    [SerializeField] private int ksmOverlaySortingOrderOffset = 10;

    /// <summary>
    /// region 하나에 대응하는 런타임 오버레이 묶음.
    /// </summary>
    private class KSM_RuntimeRegionOverlay
    {
        /// <summary>
        /// region 전체 오버레이 루트 오브젝트.
        /// </summary>
        public GameObject rootObject;

        /// <summary>
        /// 내부 반투명 채움용 SpriteRenderer.
        /// </summary>
        public SpriteRenderer fillRenderer;

        /// <summary>
        /// hover 강조용 외곽선 LineRenderer.
        /// </summary>
        public LineRenderer borderRenderer;
    }

    /// <summary>
    /// 현재 생성된 locked region 오버레이들.
    /// key = region 좌표.
    /// </summary>
    private readonly Dictionary<Vector2Int, KSM_RuntimeRegionOverlay> ksmLockedRegionOverlays
        = new Dictionary<Vector2Int, KSM_RuntimeRegionOverlay>();

    /// <summary>
    /// hover 외곽선 발광 코루틴 핸들.
    /// </summary>
    private Coroutine ksmHoverBorderPulseRoutine;

    /// <summary>
    /// 런타임 생성용 1x1 흰색 스프라이트 캐시.
    /// </summary>
    private static Sprite ksmRuntimeWhiteSprite;

    /// <summary>
    /// LineRenderer용 머티리얼 캐시.
    /// </summary>
    private static Material ksmRuntimeLineMaterial;

    /// <summary>
    /// 최대 확장 범위 안의 모든 locked region에 대해
    /// 1) baseTile을 먼저 깔고
    /// 2) 그 위에 검은 반투명 오버레이를 올린다.
    /// </summary>
    public void KSM_RefreshPassiveExpandCandidates()
    {
        if (groundTilemap == null || baseTile == null)
        {
            return;
        }

        HashSet<Vector2Int> desiredLockedRegions = new HashSet<Vector2Int>();

        int minRegionX = -maxWestExpandCount;
        int maxRegionX = maxEastExpandCount;
        int minRegionY = -maxSouthExpandCount;
        int maxRegionY = maxNorthExpandCount;

        for (int regionY = minRegionY; regionY <= maxRegionY; regionY++)
        {
            for (int regionX = minRegionX; regionX <= maxRegionX; regionX++)
            {
                Vector2Int regionCoord = new Vector2Int(regionX, regionY);

                if (!IsRegionWithinExpandBounds(regionCoord))
                {
                    continue;
                }

                if (IsRegionOpened(regionCoord))
                {
                    KSM_RemoveOverlayForRegion(regionCoord);
                    continue;
                }

                desiredLockedRegions.Add(regionCoord);

                KSM_EnsureBaseTilesForLockedRegion(regionCoord);
                KSM_EnsureOverlayForRegion(regionCoord);
                KSM_ApplyOverlayDefaultState(regionCoord);
            }
        }

        List<Vector2Int> staleRegions = new List<Vector2Int>();

        foreach (KeyValuePair<Vector2Int, KSM_RuntimeRegionOverlay> pair in ksmLockedRegionOverlays)
        {
            if (!desiredLockedRegions.Contains(pair.Key))
            {
                staleRegions.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleRegions.Count; i++)
        {
            KSM_RemoveOverlayForRegion(staleRegions[i]);
        }
    }

    /// <summary>
    /// 현재 생성된 locked region 오버레이와
    /// locked region용 baseTile을 모두 제거한다.
    /// </summary>
    public void KSM_ClearPassiveExpandCandidates()
    {
        KSM_StopHoverBorderPulse();

        List<Vector2Int> keys = new List<Vector2Int>(ksmLockedRegionOverlays.Keys);

        for (int i = 0; i < keys.Count; i++)
        {
            Vector2Int regionCoord = keys[i];

            if (!IsRegionOpened(regionCoord))
            {
                KSM_ClearBaseTilesForLockedRegion(regionCoord);
            }

            KSM_RemoveOverlayForRegion(regionCoord);
        }

        ksmLockedRegionOverlays.Clear();
    }

    /// <summary>
    /// hover 중인 현재 구매 가능 region의 오버레이를 더 밝게 만들고,
    /// 외곽선을 발광시킨다.
    /// </summary>
    public void KSM_ShowExpansionHoverPreview(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (groundTilemap == null || baseTile == null)
        {
            return;
        }

        if (!HasStructuralExpansionPort(sourceRegion, direction))
        {
            KSM_ClearExpansionHoverPreview();
            return;
        }

        Vector2Int targetRegion = GetNeighborRegionCoord(sourceRegion, direction);

        if (isPreviewActive && hasPreviewRegion && currentPreviewRegion == targetRegion)
        {
            return;
        }

        KSM_ClearExpansionHoverPreviewVisualOnly();
        KSM_RefreshPassiveExpandCandidates();

        isPreviewActive = true;
        hasPreviewRegion = true;
        currentPreviewRegion = targetRegion;

        KSM_EnsureBaseTilesForLockedRegion(targetRegion);
        KSM_EnsureOverlayForRegion(targetRegion);

        bool canExpandNow = CanExpandFromRegion(sourceRegion, direction);
        KSM_ApplyOverlayHoverState(targetRegion, canExpandNow);

        KSM_RestartHoverBorderPulse();
    }

    /// <summary>
    /// hover 강조 상태를 제거한다.
    /// </summary>
    public void KSM_ClearExpansionHoverPreview()
    {
        KSM_ClearExpansionHoverPreview(false);
    }

    /// <summary>
    /// hover 강조 상태 제거 내부 구현.
    /// </summary>
    private void KSM_ClearExpansionHoverPreview(bool restoreCamera)
    {
        KSM_ClearExpansionHoverPreviewVisualOnly();

        isPreviewActive = false;
        hasPreviewRegion = false;
        currentPreviewRegion = new Vector2Int(int.MinValue, int.MinValue);

        KSM_RefreshPassiveExpandCandidates();
    }

    /// <summary>
    /// hover 전용 강조만 제거한다.
    /// </summary>
    private void KSM_ClearExpansionHoverPreviewVisualOnly()
    {
        KSM_StopHoverBorderPulse();

        if (hasPreviewRegion)
        {
            KSM_ApplyOverlayDefaultState(currentPreviewRegion);
        }
    }

    /// <summary>
    /// locked region 아래에 흰 baseTile이 전부 깔리도록 보장한다.
    /// </summary>
    private void KSM_EnsureBaseTilesForLockedRegion(Vector2Int regionCoord)
    {
        RectInt rect = GetRegionRect(regionCoord);

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                groundTilemap.SetTile(cell, baseTile);
                groundTilemap.SetTileFlags(cell, TileFlags.None);
                groundTilemap.SetColor(cell, Color.white);
            }
        }

        RefreshTilesAroundRect(rect);
    }

    /// <summary>
    /// locked region용 baseTile을 제거한다.
    /// </summary>
    private void KSM_ClearBaseTilesForLockedRegion(Vector2Int regionCoord)
    {
        RectInt rect = GetRegionRect(regionCoord);

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);

                if (IsWorldCellUnlocked(cell))
                {
                    continue;
                }

                groundTilemap.SetTile(cell, null);
            }
        }

        RefreshTilesAroundRect(rect);
    }

    /// <summary>
    /// 특정 region의 오버레이가 존재하도록 보장한다.
    /// 없으면 새로 생성한다.
    /// </summary>
    private void KSM_EnsureOverlayForRegion(Vector2Int regionCoord)
    {
        if (ksmLockedRegionOverlays.TryGetValue(regionCoord, out KSM_RuntimeRegionOverlay existingOverlay))
        {
            KSM_UpdateOverlayTransform(regionCoord, existingOverlay);
            return;
        }

        RectInt rect = GetRegionRect(regionCoord);
        KSM_GetRegionWorldBounds(rect, out Vector3 centerWorld, out Vector2 sizeWorld);

        GameObject root = new GameObject($"KSM_LockedRegionOverlay_{regionCoord.x}_{regionCoord.y}");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(centerWorld.x, centerWorld.y, ksmOverlayZOffset);

        GameObject fillObject = new GameObject("Fill");
        fillObject.transform.SetParent(root.transform, false);
        fillObject.transform.localPosition = Vector3.zero;
        fillObject.transform.localRotation = Quaternion.identity;
        fillObject.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);

        SpriteRenderer fillRenderer = fillObject.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = KSM_GetRuntimeWhiteSprite();
        fillRenderer.drawMode = SpriteDrawMode.Simple;

        KSM_ApplySorting(fillRenderer, ksmOverlaySortingOrderOffset);

        GameObject borderObject = new GameObject("Border");
        borderObject.transform.SetParent(root.transform, false);
        borderObject.transform.localPosition = Vector3.zero;
        borderObject.transform.localRotation = Quaternion.identity;
        borderObject.transform.localScale = Vector3.one;

        LineRenderer borderRenderer = borderObject.AddComponent<LineRenderer>();
        borderRenderer.useWorldSpace = false;
        borderRenderer.loop = false;
        borderRenderer.positionCount = 5;
        borderRenderer.widthMultiplier = ksmBorderThicknessWorld;
        borderRenderer.numCapVertices = 2;
        borderRenderer.numCornerVertices = 2;
        borderRenderer.material = KSM_GetRuntimeLineMaterial();
        borderRenderer.textureMode = LineTextureMode.Stretch;
        borderRenderer.enabled = false;

        borderRenderer.sortingLayerID = fillRenderer.sortingLayerID;
        borderRenderer.sortingOrder = fillRenderer.sortingOrder + 1;

        float halfW = sizeWorld.x * 0.5f;
        float halfH = sizeWorld.y * 0.5f;

        borderRenderer.SetPosition(0, new Vector3(-halfW, -halfH, 0f));
        borderRenderer.SetPosition(1, new Vector3(-halfW, halfH, 0f));
        borderRenderer.SetPosition(2, new Vector3(halfW, halfH, 0f));
        borderRenderer.SetPosition(3, new Vector3(halfW, -halfH, 0f));
        borderRenderer.SetPosition(4, new Vector3(-halfW, -halfH, 0f));

        KSM_RuntimeRegionOverlay overlay = new KSM_RuntimeRegionOverlay
        {
            rootObject = root,
            fillRenderer = fillRenderer,
            borderRenderer = borderRenderer
        };

        ksmLockedRegionOverlays.Add(regionCoord, overlay);
        KSM_ApplyOverlayDefaultState(regionCoord);
    }

    /// <summary>
    /// 특정 region 오버레이를 제거한다.
    /// </summary>
    private void KSM_RemoveOverlayForRegion(Vector2Int regionCoord)
    {
        if (!ksmLockedRegionOverlays.TryGetValue(regionCoord, out KSM_RuntimeRegionOverlay overlay))
        {
            return;
        }

        if (overlay != null && overlay.rootObject != null)
        {
            Destroy(overlay.rootObject);
        }

        ksmLockedRegionOverlays.Remove(regionCoord);
    }

    /// <summary>
    /// 특정 region 오버레이를 기본 상태로 되돌린다.
    /// </summary>
    private void KSM_ApplyOverlayDefaultState(Vector2Int regionCoord)
    {
        if (!ksmLockedRegionOverlays.TryGetValue(regionCoord, out KSM_RuntimeRegionOverlay overlay))
        {
            return;
        }

        if (overlay.fillRenderer != null)
        {
            overlay.fillRenderer.color = ksmLockedOverlayColor;
        }

        if (overlay.borderRenderer != null)
        {
            overlay.borderRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 특정 region 오버레이를 hover 상태로 만든다.
    /// </summary>
    private void KSM_ApplyOverlayHoverState(Vector2Int regionCoord, bool canExpandNow)
    {
        if (!ksmLockedRegionOverlays.TryGetValue(regionCoord, out KSM_RuntimeRegionOverlay overlay))
        {
            return;
        }

        if (overlay.fillRenderer != null)
        {
            overlay.fillRenderer.color = canExpandNow
                ? ksmHoverOverlayColor
                : ksmHoverOverlayUnaffordableColor;
        }

        if (overlay.borderRenderer != null)
        {
            overlay.borderRenderer.enabled = true;
            overlay.borderRenderer.startColor = ksmHoverBorderPulseMinColor;
            overlay.borderRenderer.endColor = ksmHoverBorderPulseMinColor;
        }
    }

    /// <summary>
    /// region rect의 월드 중심과 월드 크기를 계산한다.
    /// </summary>
    private void KSM_GetRegionWorldBounds(RectInt rect, out Vector3 centerWorld, out Vector2 sizeWorld)
    {
        Vector3 worldMin = groundTilemap.CellToWorld(new Vector3Int(rect.xMin, rect.yMin, 0));
        Vector3 worldMax = groundTilemap.CellToWorld(new Vector3Int(rect.xMax, rect.yMax, 0));

        float widthWorld = Mathf.Abs(worldMax.x - worldMin.x);
        float heightWorld = Mathf.Abs(worldMax.y - worldMin.y);

        centerWorld = new Vector3(
            (worldMin.x + worldMax.x) * 0.5f,
            (worldMin.y + worldMax.y) * 0.5f,
            0f
        );

        sizeWorld = new Vector2(widthWorld, heightWorld);
    }

    /// <summary>
    /// 오버레이 transform / 크기를 현재 타일맵 상태에 맞게 갱신한다.
    /// </summary>
    private void KSM_UpdateOverlayTransform(Vector2Int regionCoord, KSM_RuntimeRegionOverlay overlay)
    {
        if (overlay == null || overlay.rootObject == null || overlay.fillRenderer == null)
        {
            return;
        }

        RectInt rect = GetRegionRect(regionCoord);
        KSM_GetRegionWorldBounds(rect, out Vector3 centerWorld, out Vector2 sizeWorld);

        overlay.rootObject.transform.position = new Vector3(centerWorld.x, centerWorld.y, ksmOverlayZOffset);

        overlay.fillRenderer.transform.localPosition = Vector3.zero;
        overlay.fillRenderer.transform.localRotation = Quaternion.identity;
        overlay.fillRenderer.transform.localScale = new Vector3(sizeWorld.x, sizeWorld.y, 1f);

        if (overlay.borderRenderer != null)
        {
            float halfW = sizeWorld.x * 0.5f;
            float halfH = sizeWorld.y * 0.5f;

            overlay.borderRenderer.SetPosition(0, new Vector3(-halfW, -halfH, 0f));
            overlay.borderRenderer.SetPosition(1, new Vector3(-halfW, halfH, 0f));
            overlay.borderRenderer.SetPosition(2, new Vector3(halfW, halfH, 0f));
            overlay.borderRenderer.SetPosition(3, new Vector3(halfW, -halfH, 0f));
            overlay.borderRenderer.SetPosition(4, new Vector3(-halfW, -halfH, 0f));
        }
    }

    /// <summary>
    /// hover 외곽선 발광 코루틴을 다시 시작한다.
    /// </summary>
    private void KSM_RestartHoverBorderPulse()
    {
        KSM_StopHoverBorderPulse();

        if (!isActiveAndEnabled || !hasPreviewRegion)
        {
            return;
        }

        if (!ksmLockedRegionOverlays.ContainsKey(currentPreviewRegion))
        {
            return;
        }

        ksmHoverBorderPulseRoutine = StartCoroutine(KSM_HoverBorderPulseRoutine());
    }

    /// <summary>
    /// hover 외곽선 발광 코루틴을 정지한다.
    /// </summary>
    private void KSM_StopHoverBorderPulse()
    {
        if (ksmHoverBorderPulseRoutine == null)
        {
            return;
        }

        StopCoroutine(ksmHoverBorderPulseRoutine);
        ksmHoverBorderPulseRoutine = null;
    }

    /// <summary>
    /// 현재 hover 중인 region 외곽선을 노란색으로 발광시킨다.
    /// </summary>
    private IEnumerator KSM_HoverBorderPulseRoutine()
    {
        while (isPreviewActive && hasPreviewRegion)
        {
            if (!ksmLockedRegionOverlays.TryGetValue(currentPreviewRegion, out KSM_RuntimeRegionOverlay overlay))
            {
                break;
            }

            if (overlay == null || overlay.borderRenderer == null || !overlay.borderRenderer.enabled)
            {
                break;
            }

            float pulse = (Mathf.Sin(Time.unscaledTime * ksmHoverBorderPulseSpeed) + 1f) * 0.5f;
            Color borderColor = Color.Lerp(ksmHoverBorderPulseMinColor, ksmHoverBorderPulseMaxColor, pulse);

            overlay.borderRenderer.startColor = borderColor;
            overlay.borderRenderer.endColor = borderColor;

            yield return null;
        }

        ksmHoverBorderPulseRoutine = null;
    }

    /// <summary>
    /// 오버레이 SpriteRenderer sorting 설정을 타일맵보다 위로 맞춘다.
    /// </summary>
    private void KSM_ApplySorting(Renderer renderer, int orderOffset)
    {
        if (renderer == null || groundTilemap == null)
        {
            return;
        }

        TilemapRenderer tilemapRenderer = groundTilemap.GetComponent<TilemapRenderer>();

        if (tilemapRenderer != null)
        {
            renderer.sortingLayerID = tilemapRenderer.sortingLayerID;
            renderer.sortingOrder = tilemapRenderer.sortingOrder + orderOffset;
        }
    }

    /// <summary>
    /// 런타임 생성용 1x1 흰색 스프라이트를 반환한다.
    /// Point 필터를 써서 뿌옇게 번지는 느낌을 줄인다.
    /// </summary>
    private static Sprite KSM_GetRuntimeWhiteSprite()
    {
        if (ksmRuntimeWhiteSprite != null)
        {
            return ksmRuntimeWhiteSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "KSM_RuntimeWhiteTexture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        ksmRuntimeWhiteSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f
        );

        return ksmRuntimeWhiteSprite;
    }

    /// <summary>
    /// 런타임 LineRenderer용 머티리얼을 반환한다.
    /// </summary>
    private static Material KSM_GetRuntimeLineMaterial()
    {
        if (ksmRuntimeLineMaterial != null)
        {
            return ksmRuntimeLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        ksmRuntimeLineMaterial = new Material(shader);
        ksmRuntimeLineMaterial.name = "KSM_RuntimeLineMaterial";

        return ksmRuntimeLineMaterial;
    }
}