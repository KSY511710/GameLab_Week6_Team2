using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSM_MapExpandButtonManager
///
/// 현재 열린 모든 구역을 순회하면서
/// "확장 가능한 닫힌 targetRegion"마다 버튼을 1개씩 생성하는 매니저.
///
/// 이번 버전 핵심:
/// 1. 버튼을 열린 구역의 경계가 아니라 "구매할 targetRegion 중앙"에 배치한다.
/// 2. 버튼 크기를 targetRegion 크기에 맞춰 크게 만든다.
/// 3. 같은 targetRegion으로 향하는 버튼은 1개만 유지한다.
/// 4. 카메라 이동 / 줌에도 버튼 위치와 크기가 계속 targetRegion에 맞도록 갱신한다.
/// </summary>
public class KSM_MapExpandButtonManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("확장 로직을 가진 GridManager.")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("생성할 확장 버튼 프리팹. KSM_MapExpandButton이 붙어 있어야 한다.")]
    [SerializeField] private KSM_MapExpandButton expandButtonPrefab;

    [Tooltip("생성된 버튼들을 담을 부모 Transform. 비워두면 현재 오브젝트를 사용한다.")]
    [SerializeField] private Transform buttonRoot;

    [Header("Placement")]
    [Tooltip("버튼 중심의 월드 좌표 추가 오프셋. 필요하면 아주 조금만 조정.")]
    [SerializeField] private Vector3 buttonWorldOffset = Vector3.zero;

    [Tooltip("targetRegion 기본 크기 대비 버튼 크기 배율. 1이면 땅 크기와 동일.")]
    [SerializeField, Min(0.1f)] private float buttonSizeScale = 1f;

    [Tooltip("버튼을 targetRegion보다 조금 더 크게 만들고 싶을 때 추가할 월드 크기(X,Y).")]
    [SerializeField] private Vector2 buttonExtraWorldSize = Vector2.zero;

    /// <summary>
    /// 실제 생성된 버튼과,
    /// 이 버튼이 어떤 sourceRegion / targetRegion / direction을 대표하는지 저장한다.
    /// </summary>
    private class SpawnedTargetButton
    {
        public KSM_MapExpandButton button;
        public Vector2Int sourceRegion;
        public Vector2Int targetRegion;
        public KSM_ExpandDirection direction;
    }

    /// <summary>
    /// 현재 생성된 버튼 목록.
    /// </summary>
    private readonly List<SpawnedTargetButton> activeButtons = new List<SpawnedTargetButton>();

    /// <summary>
    /// 같은 targetRegion에 버튼이 여러 개 생기지 않도록 막기 위한 집합.
    /// </summary>
    private readonly HashSet<Vector2Int> claimedTargetRegions = new HashSet<Vector2Int>();

    /// <summary>
    /// buttonRoot 상위 Canvas 캐시.
    /// </summary>
    private Canvas cachedCanvas;

    /// <summary>
    /// buttonRoot가 RectTransform인지 캐시.
    /// </summary>
    private RectTransform buttonRootRect;

    /// <summary>
    /// Awake 시 GridManager / buttonRoot / Canvas 참조를 자동 보정한다.
    /// </summary>
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

    /// <summary>
    /// Start에서 한 프레임 기다린 뒤 버튼을 다시 생성한다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    /// <summary>
    /// 활성화 시 상태 변경 이벤트를 구독하고 버튼을 즉시 갱신한다.
    /// </summary>
    private void OnEnable()
    {
        GridManager.OnExpandStateChanged += HandleExpandStateChanged;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제하고 버튼을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ClearAllButtons();
    }

    /// <summary>
    /// 카메라 이동 / 줌 / Canvas 변화 중에도
    /// 버튼이 targetRegion 중앙과 크기에 맞도록 매 프레임 다시 계산한다.
    /// </summary>
    private void LateUpdate()
    {
        UpdateAllButtonTransforms();
    }

    /// <summary>
    /// 확장 성공 등으로 맵 구조가 바뀌면 버튼을 전부 다시 생성한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        RefreshAllButtons();
    }

    /// <summary>
    /// 현재 buttonRoot 기준으로 Canvas / RectTransform 캐시를 다시 잡는다.
    /// </summary>
    private void RefreshCanvasCache()
    {
        if (buttonRoot == null)
        {
            return;
        }

        cachedCanvas = buttonRoot.GetComponentInParent<Canvas>();
        buttonRootRect = buttonRoot as RectTransform;
    }

    /// <summary>
    /// 현재 열린 모든 구역을 순회하면서
    /// 구조적으로 가능한 확장 후보 targetRegion마다 버튼을 다시 생성한다.
    /// </summary>
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
    }

    /// <summary>
    /// 특정 열린 구역의 특정 방향에 대해
    /// targetRegion 중앙 오버레이 버튼 생성을 시도한다.
    /// </summary>
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

    /// <summary>
    /// 현재 생성된 모든 버튼의 위치 / 크기를 다시 계산한다.
    /// </summary>
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

    /// <summary>
    /// 버튼 하나의 targetRegion 기준 위치 / 크기를 계산한다.
    /// </summary>
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

    /// <summary>
    /// 특정 targetRegion의 월드 중심점과 최종 월드 크기를 계산한다.
    /// </summary>
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

    /// <summary>
    /// Screen Space Canvas일 때 버튼 위치 / 크기를 맞춘다.
    /// </summary>
    private void UpdateScreenSpaceButtonRect(RectTransform buttonRect, Vector3 centerWorld, Vector2 worldSize)
    {
        Camera worldCamera = gridManager.mainCamera != null ? gridManager.mainCamera : Camera.main;
        Camera uiCamera = cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cachedCanvas.worldCamera;

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
            float width = Mathf.Abs(localTopRight.x - localBottomLeft.x);
            float height = Mathf.Abs(localTopRight.y - localBottomLeft.y);

            buttonRect.anchoredPosition = localCenter;
            buttonRect.sizeDelta = new Vector2(width, height);
            buttonRect.localRotation = Quaternion.identity;
            buttonRect.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// World Space Canvas 또는 일반 월드 오브젝트일 때 버튼 위치 / 크기를 맞춘다.
    /// </summary>
    private void UpdateWorldSpaceButtonRect(Transform buttonTransform, RectTransform buttonRect, Vector3 centerWorld, Vector2 worldSize)
    {
        if (buttonRect != null)
        {
            float targetZ = buttonRect.position.z;
            Vector3 finalWorldPos = new Vector3(centerWorld.x, centerWorld.y, targetZ);

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

    /// <summary>
    /// 현재 생성된 버튼들을 전부 삭제하고 내부 목록을 비운다.
    /// </summary>
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