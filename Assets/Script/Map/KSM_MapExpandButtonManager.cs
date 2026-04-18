using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KSM_MapExpandButtonManager
/// 
/// 현재 열린 모든 구역을 순회하면서
/// 각 구역의 북 / 남 / 서 / 동 방향 확장 버튼을 생성하는 매니저.
/// 
/// 이번 수정 핵심:
/// 1. 같은 targetRegion(같은 닫힌 구역)으로 향하는 버튼은 1개만 생성
/// 2. 버튼 위치를 매 프레임 다시 계산해 카메라 이동 / 줌에도 맵에 붙어 보이게 유지
/// 3. World Space Canvas를 가장 안정적인 기준으로 지원
/// 4. Screen Space Canvas도 기존 호환을 위해 계속 지원
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
    [Tooltip("버튼을 구역 경계에서 얼마나 바깥으로 띄울지. 동서남북 중심에 딱 붙이고 싶으면 0 또는 매우 작은 값 권장.")]
    [SerializeField] private float buttonWorldMargin = 0.05f;

    [Tooltip("북쪽 버튼 추가 위치 보정값.")]
    [SerializeField] private Vector3 northOffset = Vector3.zero;

    [Tooltip("남쪽 버튼 추가 위치 보정값.")]
    [SerializeField] private Vector3 southOffset = Vector3.zero;

    [Tooltip("서쪽 버튼 추가 위치 보정값.")]
    [SerializeField] private Vector3 westOffset = Vector3.zero;

    [Tooltip("동쪽 버튼 추가 위치 보정값.")]
    [SerializeField] private Vector3 eastOffset = Vector3.zero;

    /// <summary>
    /// 생성된 버튼 하나와
    /// 그 버튼이 어떤 sourceRegion / direction을 담당하는지 저장하는 내부 데이터.
    /// 
    /// 위치를 다시 계산할 때 sourceRegion + direction 정보가 필요하므로
    /// 버튼 인스턴스와 함께 묶어서 관리한다.
    /// </summary>
    private class SpawnedPortButton
    {
        /// <summary>
        /// 실제 생성된 버튼 컴포넌트.
        /// </summary>
        public KSM_MapExpandButton button;

        /// <summary>
        /// 이 버튼의 기준이 되는 열린 구역 좌표.
        /// </summary>
        public Vector2Int sourceRegion;

        /// <summary>
        /// 이 버튼이 담당하는 확장 방향.
        /// </summary>
        public KSM_ExpandDirection direction;

        /// <summary>
        /// 방향별 추가 위치 보정 오프셋.
        /// </summary>
        public Vector3 extraOffset;
    }

    /// <summary>
    /// 현재 생성되어 있는 모든 버튼 목록.
    /// 맵 구조가 바뀌면 삭제 후 다시 생성한다.
    /// </summary>
    private readonly List<SpawnedPortButton> activeButtons = new List<SpawnedPortButton>();

    /// <summary>
    /// 같은 targetRegion으로 향하는 버튼을 여러 개 만들지 않기 위해 사용하는 집합.
    /// 
    /// RefreshAllButtons 한 번 실행 중에
    /// 이미 버튼 대표를 하나 배정한 targetRegion이면 다시 생성하지 않는다.
    /// </summary>
    private readonly HashSet<Vector2Int> claimedTargetRegions = new HashSet<Vector2Int>();

    /// <summary>
    /// buttonRoot의 상위에 Canvas가 있을 수 있으므로 캐시해둔 Canvas 참조.
    /// </summary>
    private Canvas cachedCanvas;

    /// <summary>
    /// buttonRoot가 RectTransform인지 확인하기 위한 캐시.
    /// Screen Space Canvas 좌표 변환 시 사용한다.
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
    /// Start에서 한 프레임 기다린 뒤 버튼을 한 번 더 생성한다.
    /// 
    /// GridManager.Start에서 초기 열린 구역을 만드는 흐름이 먼저 끝난 뒤,
    /// 그 결과를 기준으로 버튼을 확실히 만들기 위함이다.
    /// </summary>
    private IEnumerator Start()
    {
        yield return null;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    /// <summary>
    /// 활성화 시 확장 상태 이벤트를 구독하고 즉시 버튼을 다시 계산한다.
    /// </summary>
    private void OnEnable()
    {
        GridManager.OnExpandStateChanged += HandleExpandStateChanged;
        RefreshCanvasCache();
        RefreshAllButtons();
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제하고 현재 버튼들을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ClearAllButtons();
    }

    /// <summary>
    /// 카메라 이동 / 줌 / 캔버스 재계산 중에도
    /// 버튼이 맵의 동서남북 중심에 붙어 보이도록
    /// 매 프레임 위치를 다시 계산한다.
    /// </summary>
    private void LateUpdate()
    {
        UpdateAllButtonPositions();
    }

    /// <summary>
    /// 확장 성공 등으로 맵 구조가 바뀌었을 때
    /// 버튼들을 전부 다시 생성한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        RefreshAllButtons();
    }

    /// <summary>
    /// 현재 buttonRoot 기준으로 Canvas / RectTransform 캐시를 다시 잡는다.
    /// 
    /// buttonRoot를 다른 캔버스로 옮기거나
    /// World Space Canvas를 새로 만든 경우에도 정상 동작하도록 하기 위한 함수다.
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
    /// 구조적으로 가능한 확장 버튼을 다시 전부 생성한다.
    /// 
    /// 중요한 정책:
    /// 같은 targetRegion을 향하는 버튼은 1개만 남긴다.
    /// 따라서 한 닫힌 구역을 여러 열린 구역이 동시에 바라보더라도
    /// 대표 버튼 하나만 생성된다.
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

        // 대표 버튼 선택 순서를 안정적으로 유지하기 위한 정렬.
        // 위쪽(y 큰 값)부터, 같은 줄이면 왼쪽(x 작은 값)부터 처리한다.
        openedRegions.Sort((a, b) =>
        {
            int yCompare = b.y.CompareTo(a.y);
            if (yCompare != 0) return yCompare;
            return a.x.CompareTo(b.x);
        });

        for (int i = 0; i < openedRegions.Count; i++)
        {
            Vector2Int sourceRegion = openedRegions[i];

            TryCreatePortButton(sourceRegion, KSM_ExpandDirection.North, northOffset);
            TryCreatePortButton(sourceRegion, KSM_ExpandDirection.South, southOffset);
            TryCreatePortButton(sourceRegion, KSM_ExpandDirection.West, westOffset);
            TryCreatePortButton(sourceRegion, KSM_ExpandDirection.East, eastOffset);
        }

        UpdateAllButtonPositions();
    }

    /// <summary>
    /// 특정 열린 구역의 특정 방향 포트 버튼을 생성 시도한다.
    /// 
    /// 생성 조건:
    /// 1. sourceRegion 기준으로 구조적 확장 포트가 있어야 한다.
    /// 2. 같은 targetRegion에 대한 대표 버튼이 아직 없어야 한다.
    /// 
    /// 실제로 돈이 부족하거나 애니메이션 중이어도
    /// 버튼 자체는 생성할 수 있고, 그 경우 버튼은 비활성 / 회색 처리된다.
    /// </summary>
    /// <param name="sourceRegion">기준이 되는 열린 구역 좌표</param>
    /// <param name="direction">확장 방향</param>
    /// <param name="extraOffset">방향별 추가 위치 보정값</param>
    private void TryCreatePortButton(Vector2Int sourceRegion, KSM_ExpandDirection direction, Vector3 extraOffset)
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

        // 같은 targetRegion으로 향하는 버튼이 이미 있으면 중복 생성하지 않는다.
        if (!claimedTargetRegions.Add(targetRegion))
        {
            return;
        }

        KSM_MapExpandButton newButton = Instantiate(expandButtonPrefab, buttonRoot);
        newButton.name = $"ExpandButton_{sourceRegion.x}_{sourceRegion.y}_{direction}";
        newButton.Setup(gridManager, sourceRegion, direction);

        SpawnedPortButton spawned = new SpawnedPortButton
        {
            button = newButton,
            sourceRegion = sourceRegion,
            direction = direction,
            extraOffset = extraOffset
        };

        activeButtons.Add(spawned);
        UpdateSingleButtonPosition(spawned);
    }

    /// <summary>
    /// 현재 생성된 모든 버튼의 위치를 다시 계산한다.
    /// </summary>
    private void UpdateAllButtonPositions()
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

            UpdateSingleButtonPosition(activeButtons[i]);
        }
    }

    /// <summary>
    /// 버튼 하나의 위치를 다시 계산한다.
    /// 
    /// 배치 방식:
    /// - World Space Canvas 또는 일반 월드 오브젝트면 월드 좌표를 그대로 사용
    /// - Screen Space Canvas면 월드 -> 스크린 -> 로컬 UI 좌표로 변환
    /// 
    /// 네가 원하는 "맵 타일의 동서남북 중심에 붙는 버튼"은
    /// World Space Canvas에서 가장 안정적으로 보인다.
    /// </summary>
    /// <param name="spawned">위치를 갱신할 버튼 정보</param>
    private void UpdateSingleButtonPosition(SpawnedPortButton spawned)
    {
        Vector3 worldPos =
            gridManager.GetExpandPortWorldPosition(spawned.sourceRegion, spawned.direction, buttonWorldMargin)
            + spawned.extraOffset;

        Transform buttonTransform = spawned.button.transform;
        RectTransform buttonRect = buttonTransform as RectTransform;

        bool isScreenSpaceCanvas =
            cachedCanvas != null &&
            cachedCanvas.renderMode != RenderMode.WorldSpace &&
            buttonRootRect != null &&
            buttonRect != null;

        if (isScreenSpaceCanvas)
        {
            Camera worldCamera = gridManager.mainCamera != null ? gridManager.mainCamera : Camera.main;
            Camera uiCamera = cachedCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cachedCanvas.worldCamera;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(worldCamera, worldPos);

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(buttonRootRect, screenPoint, uiCamera, out Vector2 localPoint))
            {
                buttonRect.anchoredPosition = localPoint;
                buttonRect.localRotation = Quaternion.identity;
                buttonRect.localScale = Vector3.one;
            }
        }
        else
        {
            // World Space Canvas 또는 일반 월드 기준 오브젝트일 때는
            // 월드 위치를 그대로 사용한다.
            if (buttonRect != null)
            {
                buttonRect.position = worldPos;
                buttonRect.rotation = Quaternion.identity;
                buttonRect.localScale = Vector3.one;
            }
            else
            {
                buttonTransform.position = worldPos;
                buttonTransform.rotation = Quaternion.identity;
                buttonTransform.localScale = Vector3.one;
            }
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