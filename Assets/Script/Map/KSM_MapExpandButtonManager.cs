using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KSM_MapExpandButtonManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private KSM_MapExpandButton expandButtonPrefab;
    [SerializeField] private Transform buttonRoot;

    [Header("Placement")]
    [SerializeField] private Vector3 buttonWorldOffset = Vector3.zero;
    [SerializeField, Min(0.1f)] private float buttonSizeScale = 1f;

    // 타일 가장자리 인식 빈틈 줄이기
    [SerializeField] private Vector2 buttonExtraWorldSize = new Vector2(0.20f, 0.20f);

    // Screen Space Canvas일 때 픽셀 패딩 추가
    [SerializeField, Min(0f)] private float screenSpacePixelPadding = 12f;

    private class SpawnedTargetButton
    {
        public KSM_MapExpandButton button;
        public Vector2Int sourceRegion;
        public Vector2Int targetRegion;
        public KSM_ExpandDirection direction;
    }

    private readonly List<SpawnedTargetButton> activeButtons = new List<SpawnedTargetButton>();
    private readonly HashSet<Vector2Int> claimedTargetRegions = new HashSet<Vector2Int>();

    private Canvas cachedCanvas;
    private RectTransform buttonRootRect;

    private void Awake()
    {
        if (gridManager == null)
        {
            gridManager = Object.FindAnyObjectByType<GridManager>();
        }

        if (buttonRoot == null)
        {
            buttonRoot = transform;
        }

        RefreshCanvasCache();
    }

    private IEnumerator Start()
    {
        yield return null;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    private void OnEnable()
    {
        GridManager.OnExpandStateChanged += HandleExpandStateChanged;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    private void OnDisable()
    {
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ClearAllButtons();

        if (gridManager != null)
        {
            gridManager.KSM_ClearPassiveExpandCandidates();
            gridManager.KSM_ClearExpansionHoverPreview();
        }
    }

    private void LateUpdate()
    {
        UpdateAllButtonTransforms();
    }

    private void HandleExpandStateChanged()
    {
        RefreshAllButtons();
    }

    private void RefreshCanvasCache()
    {
        if (buttonRoot == null)
        {
            return;
        }

        cachedCanvas = buttonRoot.GetComponentInParent<Canvas>();
        buttonRootRect = buttonRoot as RectTransform;
    }

    public void RefreshAllButtons()
    {
        ClearAllButtons();
        claimedTargetRegions.Clear();

        if (gridManager == null || expandButtonPrefab == null)
        {
            return;
        }

        List<Vector2Int> openedRegions = gridManager.GetOpenedRegions();

        openedRegions.Sort((a, b) =>
        {
            int yCompare = b.y.CompareTo(a.y);
            if (yCompare != 0)
            {
                return yCompare;
            }

            return a.x.CompareTo(b.x);
        });

        for (int i = 0; i < openedRegions.Count; i++)
        {
            Vector2Int sourceRegion = openedRegions[i];

            TryCreateTargetRegionButton(sourceRegion, KSM_ExpandDirection.North);
            TryCreateTargetRegionButton(sourceRegion, KSM_ExpandDirection.South);
            TryCreateTargetRegionButton(sourceRegion, KSM_ExpandDirection.West);
            TryCreateTargetRegionButton(sourceRegion, KSM_ExpandDirection.East);
        }

        UpdateAllButtonTransforms();
        gridManager.KSM_RefreshPassiveExpandCandidates();
    }

    private void TryCreateTargetRegionButton(Vector2Int sourceRegion, KSM_ExpandDirection direction)
    {
        if (gridManager == null)
        {
            return;
        }

        if (!gridManager.HasStructuralExpansionPort(sourceRegion, direction))
        {
            return;
        }

        Vector2Int targetRegion = gridManager.GetNeighborRegionCoord(sourceRegion, direction);

        if (!claimedTargetRegions.Add(targetRegion))
        {
            return;
        }

        KSM_MapExpandButton newButton = Instantiate(expandButtonPrefab, buttonRoot);
        newButton.name = $"ExpandRegionButton_Target_{targetRegion.x}_{targetRegion.y}";
        newButton.Setup(gridManager, sourceRegion, direction);
        newButton.transform.SetAsLastSibling();

        SpawnedTargetButton spawned = new SpawnedTargetButton
        {
            button = newButton,
            sourceRegion = sourceRegion,
            targetRegion = targetRegion,
            direction = direction
        };

        activeButtons.Add(spawned);
        UpdateSingleButtonTransform(spawned);
    }

    private void UpdateAllButtonTransforms()
    {
        if (gridManager == null)
        {
            return;
        }

        for (int i = 0; i < activeButtons.Count; i++)
        {
            if (activeButtons[i] == null || activeButtons[i].button == null)
            {
                continue;
            }

            UpdateSingleButtonTransform(activeButtons[i]);
        }
    }

    private void UpdateSingleButtonTransform(SpawnedTargetButton spawned)
    {
        if (spawned == null || spawned.button == null || gridManager == null || gridManager.groundTilemap == null)
        {
            return;
        }

        RectTransform buttonRect = spawned.button.transform as RectTransform;
        Transform buttonTransform = spawned.button.transform;

        GetTargetRegionWorldCenterAndSize(spawned.targetRegion, out Vector3 targetCenterWorld, out Vector2 targetWorldSize);

        bool isScreenSpaceCanvas =
            cachedCanvas != null &&
            cachedCanvas.renderMode != RenderMode.WorldSpace &&
            buttonRootRect != null &&
            buttonRect != null;

        if (isScreenSpaceCanvas)
        {
            UpdateScreenSpaceButtonRect(buttonRect, targetCenterWorld, targetWorldSize);
        }
        else
        {
            UpdateWorldSpaceButtonRect(buttonTransform, buttonRect, targetCenterWorld, targetWorldSize);
        }
    }

    private void GetTargetRegionWorldCenterAndSize(Vector2Int targetRegion, out Vector3 centerWorld, out Vector2 finalWorldSize)
    {
        RectInt targetRect = gridManager.GetRegionRect(targetRegion);

        Vector3 worldMin = gridManager.groundTilemap.CellToWorld(new Vector3Int(targetRect.xMin, targetRect.yMin, 0));
        Vector3 worldMax = gridManager.groundTilemap.CellToWorld(new Vector3Int(targetRect.xMax, targetRect.yMax, 0));

        float baseWidth = Mathf.Abs(worldMax.x - worldMin.x);
        float baseHeight = Mathf.Abs(worldMax.y - worldMin.y);

        float scaledWidth = baseWidth * buttonSizeScale;
        float scaledHeight = baseHeight * buttonSizeScale;

        finalWorldSize = new Vector2(
            scaledWidth + buttonExtraWorldSize.x,
            scaledHeight + buttonExtraWorldSize.y
        );

        centerWorld = new Vector3(
            (worldMin.x + worldMax.x) * 0.5f,
            (worldMin.y + worldMax.y) * 0.5f,
            0f
        ) + buttonWorldOffset;
    }

    private void UpdateScreenSpaceButtonRect(RectTransform buttonRect, Vector3 centerWorld, Vector2 worldSize)
    {
        Camera worldCamera = gridManager.mainCamera != null ? gridManager.mainCamera : Camera.main;
        Camera uiCamera = cachedCanvas != null && cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : cachedCanvas != null ? cachedCanvas.worldCamera : null;

        Vector3 worldBottomLeft = new Vector3(
            centerWorld.x - (worldSize.x * 0.5f),
            centerWorld.y - (worldSize.y * 0.5f),
            centerWorld.z
        );

        Vector3 worldTopRight = new Vector3(
            centerWorld.x + (worldSize.x * 0.5f),
            centerWorld.y + (worldSize.y * 0.5f),
            centerWorld.z
        );

        Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(worldCamera, centerWorld);
        Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(worldCamera, worldBottomLeft);
        Vector2 screenTopRight = RectTransformUtility.WorldToScreenPoint(worldCamera, worldTopRight);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonRootRect, screenCenter, uiCamera, out Vector2 localCenter) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonRootRect, screenBottomLeft, uiCamera, out Vector2 localBottomLeft) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonRootRect, screenTopRight, uiCamera, out Vector2 localTopRight))
        {
            float width = Mathf.Abs(localTopRight.x - localBottomLeft.x) + screenSpacePixelPadding;
            float height = Mathf.Abs(localTopRight.y - localBottomLeft.y) + screenSpacePixelPadding;

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            buttonRect.anchoredPosition = localCenter;
            buttonRect.sizeDelta = new Vector2(width, height);
            buttonRect.localRotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;
        }
    }

    private void UpdateWorldSpaceButtonRect(Transform buttonTransform, RectTransform buttonRect, Vector3 centerWorld, Vector2 worldSize)
    {
        if (buttonRect != null)
        {
            float targetZ = buttonRect.position.z;
            Vector3 finalWorldPos = new Vector3(centerWorld.x, centerWorld.y, targetZ);

            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);

            buttonRect.position = finalWorldPos;
            buttonRect.rotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;

            Vector3 parentLossyScale = buttonRect.parent != null ? buttonRect.parent.lossyScale : Vector3.one;

            float safeScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentLossyScale.x));
            float safeScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentLossyScale.y));

            float localWidth = worldSize.x / safeScaleX;
            float localHeight = worldSize.y / safeScaleY;

            buttonRect.sizeDelta = new Vector2(localWidth, localHeight);
        }
        else
        {
            buttonTransform.position = centerWorld;
            buttonTransform.rotation = Quaternion.identity;
        }
    }

    private void ClearAllButtons()
    {
        for (int i = 0; i < activeButtons.Count; i++)
        {
            if (activeButtons[i] == null || activeButtons[i].button == null)
            {
                continue;
            }

            Destroy(activeButtons[i].button.gameObject);
        }

        activeButtons.Clear();
        claimedTargetRegions.Clear();
    }
}