using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 2D 타일맵용 카메라 입력 전담 스크립트.
///
/// 담당 기능
/// 1. 마우스 휠로 Orthographic 줌 인/아웃
/// 2. 좌클릭 드래그로 카메라 팬
/// 3. 줌 임계값에 따라 발전소 내부 구성 자동 표시 이벤트 발행
/// 4. 카메라가 맵 탐색 가능 범위를 너무 벗어나지 않도록 clamp
/// 5. 휠 줌은 관성(momentum) 기반으로 누적되어 더 부드럽게 감속한다.
///
/// 주의
/// - 블럭 드래그/배치/회전 로직은 전혀 건드리지 않는다.
/// - R키 입력은 사용하지 않는다.
/// - 현재 요청대로 좌클릭 팬을 사용하므로,
///   블럭 드래그 입력과 충돌할 가능성이 있다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class KSM_CameraController : MonoBehaviour
{
    /// <summary>
    /// 카메라 줌 상태에 따라 발전소 내부 구성 자동 표시가 켜졌는지 알리는 이벤트.
    /// PlacedBlockVisual이 구독해서 hover 없이도 원본 블럭 구성을 표시한다.
    /// </summary>
    public static event Action<bool> OnAutoRevealStateChanged;

    /// <summary>
    /// 현재 전역 자동 표시 상태.
    /// true면 충분히 줌인된 상태로 간주한다.
    /// </summary>
    public static bool IsAutoRevealActive { get; private set; }

    [Header("References")]
    [Tooltip("제어할 카메라. 비워두면 현재 오브젝트의 Camera를 자동 사용한다.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("보드 범위 정보를 얻어올 GridManager. 비워두면 씬에서 자동 탐색한다.")]
    [SerializeField] private GridManager gridManager;

    [Header("Zoom")]
    [Tooltip("휠 1단 입력 시 줌 관성에 더해지는 기본 힘. 클수록 휠 반응이 더 시원해진다.")]
    [SerializeField, Min(0.01f)] private float zoomImpulsePerWheelStep = 9.0f;

    [Tooltip("줌 관성이 초당 얼마나 빨리 0으로 감속되는지. 클수록 빨리 멈춘다.")]
    [SerializeField, Min(0.01f)] private float zoomFrictionPerSecond = 28.0f;

    [Tooltip("줌 관성 최대 절대값. 과도한 휠 입력으로 너무 급격히 확대/축소되는 것을 막는다.")]
    [SerializeField, Min(0.01f)] private float maxZoomMomentum = 18.0f;

    [Tooltip("최소 줌 값. 값이 작을수록 더 가까이 본다.")]
    [SerializeField, Min(0.01f)] private float minOrthographicSize = 3.2f;

    [Tooltip("최대 줌 값. 값이 클수록 더 멀리 본다.")]
    [SerializeField, Min(0.01f)] private float maxOrthographicSize = 28f;

    [Tooltip("줌할 때 마우스 커서가 가리키는 지점을 최대한 유지할지 여부.")]
    [SerializeField] private bool zoomTowardMouse = true;

    [Tooltip("휠 입력 후 저장된 줌 앵커를 잠시 유지하는 시간. 값이 너무 짧으면 포인트 유지가 약해지고, 너무 길면 과보정 느낌이 날 수 있다.")]
    [SerializeField, Min(0.01f)] private float zoomAnchorHoldDuration = 0.18f;

    [Tooltip("UI 위에 마우스가 올라가 있을 때 휠 줌을 무시할지 여부.")]
    [SerializeField] private bool ignoreZoomWhenPointerOverUI = true;

    [Header("Pan")]
    [Tooltip("좌클릭 드래그 팬 속도 배율.")]
    [SerializeField, Min(0.01f)] private float panSensitivity = 1f;

    [Tooltip("UI 위에서 좌클릭을 시작했을 때 팬을 막을지 여부.")]
    [SerializeField] private bool ignorePanWhenPointerOverUI = true;

    [Header("Smoothing")]
    [Tooltip("카메라 위치가 목표 위치를 따라가는 부드러움 시간. 작을수록 즉각 반응.")]
    [SerializeField, Min(0.001f)] private float positionSmoothTime = 0.08f;

    [Tooltip("카메라 줌이 목표 줌값을 따라가는 부드러움 시간. 작을수록 즉각 반응.")]
    [SerializeField, Min(0.001f)] private float zoomSmoothTime = 0.12f;

    [Header("Clamp")]
    [Tooltip("카메라를 맵 탐색 가능 범위 안으로 제한할지 여부.")]
    [SerializeField] private bool clampToMapBounds = true;

    [Tooltip("맵 가장자리 밖으로 허용할 여유 월드 거리.")]
    [SerializeField] private float clampPaddingWorld = 2f;

    [Tooltip("현재 열린 보드가 아니라 최대 확장 범위 전체를 기준으로 clamp할지 여부.")]
    [SerializeField] private bool useFullNavigationBounds = true;

    [Tooltip("카메라 화면이 허용 범위보다 더 큰 축에서는 강제로 중앙 정렬할지 여부. 끄면 줌 시 원위치 스냅을 줄일 수 있다.")]
    [SerializeField] private bool recenterAxisWhenViewportExceedsBounds = false;

    [Header("Reveal By Zoom")]
    [Tooltip("orthographic size가 이 값 이하가 되면 발전소 내부 구성 자동 표시를 켠다.")]
    [SerializeField, Min(0.01f)] private float autoRevealEnterOrthoSize = 4.8f;

    [Tooltip("orthographic size가 이 값 이상이 되면 발전소 내부 구성 자동 표시를 끈다. 깜빡임 방지용 히스테리시스 값이다.")]
    [SerializeField, Min(0.01f)] private float autoRevealExitOrthoSize = 5.3f;

    /// <summary>
    /// 현재 좌클릭 팬 진행 중인지 여부.
    /// </summary>
    private bool isPanning;

    /// <summary>
    /// 직전 프레임의 마우스 스크린 좌표.
    /// 팬 이동량을 월드 델타로 변환할 때 사용한다.
    /// </summary>
    private Vector3 lastMouseScreenPosition;

    /// <summary>
    /// 카메라가 최종적으로 따라가야 하는 목표 월드 위치.
    /// 실제 transform.position은 이 값을 향해 부드럽게 보간된다.
    /// </summary>
    private Vector3 targetPosition;

    /// <summary>
    /// 카메라가 최종적으로 따라가야 하는 목표 orthographic size.
    /// 실제 orthographicSize는 이 값을 향해 부드럽게 보간된다.
    /// </summary>
    private float targetOrthographicSize;

    /// <summary>
    /// SmoothDamp용 현재 위치 속도.
    /// </summary>
    private Vector3 positionVelocity;

    /// <summary>
    /// SmoothDamp용 현재 줌 속도.
    /// </summary>
    private float zoomVelocity;

    /// <summary>
    /// 휠 입력이 누적되어 형성된 줌 관성값.
    /// 양수면 더 줌인(orthographicSize 감소), 음수면 더 줌아웃 방향으로 작동한다.
    /// </summary>
    private float zoomMomentum;

    /// <summary>
    /// 마우스 기준 줌 포인트 유지용 스크린 좌표.
    /// 휠을 굴린 순간의 마우스 위치를 저장한다.
    /// </summary>
    private Vector3 zoomAnchorScreenPosition;

    /// <summary>
    /// 마우스 기준 줌 포인트 유지용 월드 좌표.
    /// 줌 전 기준으로 "여기를 유지하고 싶다"는 목표점을 담는다.
    /// </summary>
    private Vector3 zoomAnchorWorldPosition;

    /// <summary>
    /// 저장된 줌 앵커를 유지할 남은 시간.
    /// </summary>
    private float zoomAnchorTimer;

    /// <summary>
    /// 현재 줌 앵커가 유효한지 여부.
    /// </summary>
    private bool hasZoomAnchor;

    /// <summary>
    /// Awake에서 참조를 보정한다.
    /// </summary>
    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (gridManager == null)
        {
            gridManager = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        }

        if (targetCamera != null)
        {
            targetCamera.orthographic = true;
            targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);

            targetPosition = targetCamera.transform.position;
            targetOrthographicSize = targetCamera.orthographicSize;
        }

        if (autoRevealExitOrthoSize < autoRevealEnterOrthoSize)
        {
            autoRevealExitOrthoSize = autoRevealEnterOrthoSize;
        }
    }

    /// <summary>
    /// 활성화 시 보드 상태 변화 이벤트를 구독한다.
    /// </summary>
    private void OnEnable()
    {
        GridManager.OnExpandStateChanged += HandleExpandStateChanged;
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제한다.
    /// </summary>
    private void OnDisable()
    {
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
    }

    /// <summary>
    /// 시작 시 현재 카메라 size 기준으로 자동 표시 상태를 즉시 계산한다.
    /// </summary>
    private void Start()
    {
        EvaluateAutoRevealState(forceNotify: true);
    }

    /// <summary>
    /// 입력 처리.
    /// 줌/팬은 target 값만 바꾸고,
    /// 실제 카메라 적용은 LateUpdate에서 부드럽게 진행한다.
    /// </summary>
    private void Update()
    {
        HandleZoomInput();
        HandlePanInput();
        UpdateZoomMomentum();
    }

    /// <summary>
    /// 목표 위치/줌값으로 실제 카메라를 부드럽게 이동시킨다.
    /// </summary>
    private void LateUpdate()
    {
        ApplyCameraSmoothing();
        EvaluateAutoRevealState(forceNotify: false);
    }

    /// <summary>
    /// 맵 구조가 바뀌면 목표 카메라 상태를 현재 범위 기준으로 한 번 clamp한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        ClampTargetCameraToBounds();
    }

    /// <summary>
    /// 마우스 휠 입력을 받아 줌 관성에 누적한다.
    /// 즉시 줌하지 않고, 저장된 관성이 UpdateZoomMomentum에서 점진적으로 적용된다.
    /// </summary>
    private void HandleZoomInput()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (ignoreZoomWhenPointerOverUI && IsPointerOverUI())
        {
            return;
        }

        float wheel = Input.mouseScrollDelta.y;
        if (Mathf.Abs(wheel) < 0.01f)
        {
            return;
        }

        if (zoomTowardMouse)
        {
            zoomAnchorScreenPosition = Input.mousePosition;
            zoomAnchorWorldPosition = ScreenToWorldOnCameraPlane(zoomAnchorScreenPosition, targetPosition, targetOrthographicSize);
            zoomAnchorTimer = zoomAnchorHoldDuration;
            hasZoomAnchor = true;
        }

        zoomMomentum += wheel * zoomImpulsePerWheelStep;
        zoomMomentum = Mathf.Clamp(zoomMomentum, -maxZoomMomentum, maxZoomMomentum);
    }

    /// <summary>
    /// 저장된 줌 관성을 매 프레임 목표 줌값에 반영하고,
    /// friction으로 천천히 감속시킨다.
    /// 마우스 기준 줌 앵커가 있으면 targetPosition도 함께 보정한다.
    /// </summary>
    private void UpdateZoomMomentum()
    {
        if (targetCamera == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
        {
            return;
        }

        if (Mathf.Abs(zoomMomentum) > 0.0001f)
        {
            float previousTargetSize = targetOrthographicSize;

            // zoomMomentum > 0 이면 zoom in 방향이므로 orthographicSize는 감소해야 한다.
            targetOrthographicSize -= zoomMomentum * dt;
            targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, minOrthographicSize, maxOrthographicSize);

            // clamp에 막혀 더 이상 진행할 수 없으면 관성을 바로 정리해 과한 떨림을 방지한다.
            if (Mathf.Approximately(previousTargetSize, targetOrthographicSize) &&
                ((zoomMomentum > 0f && targetOrthographicSize <= minOrthographicSize) ||
                 (zoomMomentum < 0f && targetOrthographicSize >= maxOrthographicSize)))
            {
                zoomMomentum = 0f;
            }
            else
            {
                zoomMomentum = Mathf.MoveTowards(zoomMomentum, 0f, zoomFrictionPerSecond * dt);
            }

            if (zoomTowardMouse && hasZoomAnchor)
            {
                Vector3 worldAfterZoom = ScreenToWorldOnCameraPlane(zoomAnchorScreenPosition, targetPosition, targetOrthographicSize);
                Vector3 correction = zoomAnchorWorldPosition - worldAfterZoom;
                correction.z = 0f;
                targetPosition += correction;
            }

            ClampTargetCameraToBounds();
        }

        if (hasZoomAnchor)
        {
            zoomAnchorTimer -= dt;

            if (zoomAnchorTimer <= 0f && Mathf.Abs(zoomMomentum) <= 0.0001f)
            {
                hasZoomAnchor = false;
                zoomAnchorTimer = 0f;
            }
        }
    }

    /// <summary>
    /// 좌클릭 드래그 팬 입력을 처리한다.
    /// 즉시 카메라를 움직이지 않고 목표 위치만 이동시킨다.
    /// </summary>
    private void HandlePanInput()
    {
        if (targetCamera == null)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (ignorePanWhenPointerOverUI && IsPointerOverUI())
            {
                isPanning = false;
                return;
            }

            isPanning = true;
            lastMouseScreenPosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isPanning = false;
            return;
        }

        if (!isPanning || !Input.GetMouseButton(0))
        {
            return;
        }

        Vector3 currentMouseScreenPosition = Input.mousePosition;
        if (currentMouseScreenPosition == lastMouseScreenPosition)
        {
            return;
        }

        Vector3 previousWorld = ScreenToWorldOnCameraPlane(lastMouseScreenPosition, targetPosition, targetOrthographicSize);
        Vector3 currentWorld = ScreenToWorldOnCameraPlane(currentMouseScreenPosition, targetPosition, targetOrthographicSize);
        Vector3 deltaWorld = (previousWorld - currentWorld) * panSensitivity;
        deltaWorld.z = 0f;

        targetPosition += deltaWorld;
        lastMouseScreenPosition = currentMouseScreenPosition;

        ClampTargetCameraToBounds();
    }

    /// <summary>
    /// 현재 실제 카메라 상태를 목표 위치/줌값으로 부드럽게 이동시킨다.
    /// SmoothDamp는 unscaledDeltaTime을 사용해 프레임 변동에 좀 더 안정적으로 대응한다.
    /// </summary>
    private void ApplyCameraSmoothing()
    {
        if (targetCamera == null)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Vector3 currentPosition = targetCamera.transform.position;

        float safePositionSmooth = Mathf.Max(0.001f, positionSmoothTime);
        float safeZoomSmooth = Mathf.Max(0.001f, zoomSmoothTime);

        Vector3 smoothedPosition = Vector3.SmoothDamp(
            currentPosition,
            targetPosition,
            ref positionVelocity,
            safePositionSmooth,
            Mathf.Infinity,
            dt);

        float smoothedSize = Mathf.SmoothDamp(
            targetCamera.orthographicSize,
            targetOrthographicSize,
            ref zoomVelocity,
            safeZoomSmooth,
            Mathf.Infinity,
            dt);

        targetCamera.transform.position = new Vector3(smoothedPosition.x, smoothedPosition.y, currentPosition.z);
        targetCamera.orthographicSize = smoothedSize;
    }

    /// <summary>
    /// 특정 카메라 위치/줌값을 기준으로 스크린 좌표를 월드 좌표로 변환한다.
    /// zoomTowardMouse 보정 계산에 사용한다.
    /// </summary>
    private Vector3 ScreenToWorldOnCameraPlane(Vector3 screenPosition, Vector3 cameraWorldPosition, float orthographicSize)
    {
        if (targetCamera == null)
        {
            return Vector3.zero;
        }

        float halfHeight = orthographicSize;
        float halfWidth = orthographicSize * targetCamera.aspect;

        float normalizedX = screenPosition.x / Screen.width;
        float normalizedY = screenPosition.y / Screen.height;

        float worldX = cameraWorldPosition.x + Mathf.Lerp(-halfWidth, halfWidth, normalizedX);
        float worldY = cameraWorldPosition.y + Mathf.Lerp(-halfHeight, halfHeight, normalizedY);

        return new Vector3(worldX, worldY, cameraWorldPosition.z);
    }

    /// <summary>
    /// 목표 카메라 상태가 허용 범위를 넘지 않도록 보정한다.
    /// clamp는 실제 카메라가 아니라 target 값에 적용한다.
    /// </summary>
    private void ClampTargetCameraToBounds()
    {
        if (!clampToMapBounds || targetCamera == null || gridManager == null)
        {
            return;
        }

        Rect clampRect;
        bool hasRect = useFullNavigationBounds
            ? gridManager.KSM_TryGetCameraNavigationWorldRect(out clampRect)
            : gridManager.KSM_TryGetOpenedBoardWorldRect(out clampRect);

        if (!hasRect)
        {
            return;
        }

        float halfHeight = targetOrthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        float xMin = clampRect.xMin - clampPaddingWorld + halfWidth;
        float xMax = clampRect.xMax + clampPaddingWorld - halfWidth;
        float yMin = clampRect.yMin - clampPaddingWorld + halfHeight;
        float yMax = clampRect.yMax + clampPaddingWorld - halfHeight;

        float clampedX = targetPosition.x;
        float clampedY = targetPosition.y;

        if (xMin <= xMax)
        {
            clampedX = Mathf.Clamp(targetPosition.x, xMin, xMax);
        }
        else if (recenterAxisWhenViewportExceedsBounds)
        {
            clampedX = clampRect.center.x;
        }

        if (yMin <= yMax)
        {
            clampedY = Mathf.Clamp(targetPosition.y, yMin, yMax);
        }
        else if (recenterAxisWhenViewportExceedsBounds)
        {
            clampedY = clampRect.center.y;
        }

        targetPosition = new Vector3(clampedX, clampedY, targetPosition.z);
    }

    /// <summary>
    /// 현재 실제 카메라 orthographic size를 기준으로 발전소 내부 구성 자동 표시 상태를 계산한다.
    /// </summary>
    private void EvaluateAutoRevealState(bool forceNotify)
    {
        if (targetCamera == null)
        {
            return;
        }

        bool nextState = IsAutoRevealActive;
        float currentSize = targetCamera.orthographicSize;

        if (!IsAutoRevealActive && currentSize <= autoRevealEnterOrthoSize)
        {
            nextState = true;
        }
        else if (IsAutoRevealActive && currentSize >= autoRevealExitOrthoSize)
        {
            nextState = false;
        }

        if (!forceNotify && nextState == IsAutoRevealActive)
        {
            return;
        }

        IsAutoRevealActive = nextState;
        OnAutoRevealStateChanged?.Invoke(IsAutoRevealActive);
    }

    /// <summary>
    /// 현재 포인터가 UI 위에 있는지 검사한다.
    /// </summary>
    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}