using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSM_MapExpandButtonManager
///
/// 역할:
/// 1. 현재 구조적으로 확장 가능한 모든 targetRegion마다 투명 hit area 버튼을 1개씩 만든다.
/// 2. 버튼은 targetRegion 전체를 덮고, hover 시 비용 표시와 강조가 동작한다.
/// 3. 실제 후보 땅의 시각 표현은 GridManager의 passive candidate 타일이 담당한다.
/// 4. 확장 성공 후에는 버튼 배치와 후보 타일을 새 구조 기준으로 다시 계산한다.
/// 5. GridManager 본체는 수정하지 않고, 공개된 API만 사용한다.
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

    [Tooltip("타일 가장자리 인식 빈틈을 줄이기 위한 월드 크기 추가값.")]
    [SerializeField] private Vector2 buttonExtraWorldSize = new Vector2(0.20f, 0.20f);

    [Tooltip("Screen Space Canvas일 때 클릭 여유를 위한 픽셀 패딩.")]
    [SerializeField, Min(0f)] private float screenSpacePixelPadding = 12f;

    /// <summary>
    /// 실제 생성된 버튼과 이 버튼이 대표하는 source/target/direction 정보를 함께 저장한다.
    /// </summary>
    private class SpawnedTargetButton
    {
        /// <summary>
        /// 생성된 버튼 컴포넌트.
        /// </summary>
        public KSM_MapExpandButton button;

        /// <summary>
        /// 버튼이 연결된 기준 열린 구역.
        /// </summary>
        public Vector2Int sourceRegion;

        /// <summary>
        /// 버튼이 덮는 목표 구역.
        /// </summary>
        public Vector2Int targetRegion;

        /// <summary>
        /// 확장 방향.
        /// </summary>
        public KSM_ExpandDirection direction;
    }

    /// <summary>
    /// 현재 생성된 버튼 목록.
    /// </summary>
    private readonly List<SpawnedTargetButton> activeButtons = new List<SpawnedTargetButton>();

    /// <summary>
    /// 같은 targetRegion에 버튼이 중복 생성되지 않도록 막는 집합.
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
    /// Awake 시 참조를 자동 보정한다.
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
    /// Start에서 한 프레임 기다린 뒤 버튼과 후보 타일을 생성한다.
    /// GridManager Start 이후 실행되도록 의도한 흐름이다.
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
    /// 비활성화 시 이벤트를 해제하고 생성한 버튼만 정리한다.
    ///
    /// 중요:
    /// 여기서 passive candidate 타일까지 지워버리면
    /// locked region에 미리 깔아둔 baseTile이 사라져서
    /// Ground Tilemap이 갑자기 중앙만 남은 것처럼 보일 수 있다.
    ///
    /// 따라서 OnDisable에서는:
    /// 1. 이벤트 해제
    /// 2. 버튼 오브젝트만 정리
    /// 까지만 수행하고,
    /// 맵 타일/오버레이 정리는 하지 않는다.
    /// </summary>
    private void OnDisable()
    {
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ClearAllButtons();
    }

    /// <summary>
    /// 카메라 이동 / 줌 / Canvas 변화 중에도 버튼이 targetRegion 중앙과 크기에 맞도록
    /// 매 프레임 다시 계산한다.
    /// </summary>
    private void LateUpdate()
    {
        UpdateAllButtonTransforms();
    }

    /// <summary>
    /// 확장 성공 등으로 맵 구조가 바뀌면 버튼과 후보 타일을 전부 다시 생성한다.
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
    /// 현재 열린 모든 구역을 기준으로 확장 버튼을 다시 만들고,
    /// 동시에 passive candidate 타일도 다시 그린다.
    /// </summary>
    public void RefreshAllButtons()
    {
        ClearAllButtons();
        claimedTargetRegions.Clear();

        if (gridManager == null || expandButtonPrefab == null)
        {
            return;
        }

        // 후보 타일을 다시 깔아 둔다.
        gridManager.KSM_RefreshPassiveExpandCandidates();

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
    /// targetRegion 전체를 덮는 버튼 생성을 시도한다.
    /// </summary>
    /// <param name="sourceRegion">기준 열린 구역</param>
    /// <param name="direction">확장 방향</param>
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
    /// <param name="spawned">배치 갱신할 버튼 정보</param>
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
    /// <param name="targetRegion">목표 구역</param>
    /// <param name="centerWorld">계산된 월드 중심점</param>
    /// <param name="finalWorldSize">계산된 최종 월드 크기</param>
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
        Camera uiCamera =
            cachedCanvas != null && cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay
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

    /// <summary>
    /// World Space Canvas 또는 일반 월드 오브젝트일 때 버튼 위치 / 크기를 맞춘다.
    /// </summary>
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