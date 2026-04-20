using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KSM_MapExpandButton
///
/// 역할:
/// 1. targetRegion 전체를 덮는 투명 hit area 역할을 한다.
/// 2. hover 중일 때만 비용 텍스트를 보여준다.
/// 3. hover 시 GridManager의 KSM 전용 후보 강조 함수를 직접 호출한다.
/// 4. 클릭 시 실제 확장을 시도한다.
/// 5. GridManager 본체의 기본 hover preview 흐름은 사용하지 않는다.
/// 6. 확장 성공 시 Expansion SFX를 재생한다.
/// 
/// 사운드 규칙:
/// - hover 시에는 소리를 재생하지 않는다.
/// - 클릭했더라도 확장 실패면 소리를 재생하지 않는다.
/// - 실제 확장 성공 시점에만 Expansion SFX를 1회 재생한다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class KSM_MapExpandButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("References")]
    [Tooltip("확장 로직을 가진 GridManager.")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("이 버튼이 연결된 기준 열린 구역 좌표.")]
    [SerializeField] private Vector2Int sourceRegionCoord = Vector2Int.zero;

    [Tooltip("이 버튼이 담당하는 확장 방향.")]
    [SerializeField] private KSM_ExpandDirection direction = KSM_ExpandDirection.North;

    [Tooltip("실제 클릭 처리용 Button.")]
    [SerializeField] private Button button;

    [Tooltip("루트 hit area용 Image.")]
    [SerializeField] private Image hitAreaImage;

    [Header("Cost Text")]
    [Tooltip("hover 중에만 표시할 비용 텍스트.")]
    [SerializeField] private TextMeshProUGUI costText;

    [Tooltip("비용 표시 문자열 포맷.")]
    [SerializeField] private string costFormat = "{0}";

    [Tooltip("구매 가능 상태 비용 텍스트 색상.")]
    [SerializeField] private Color costAffordableColor = new Color(1f, 0.95f, 0.65f, 1f);

    [Tooltip("구매 불가 상태 비용 텍스트 색상.")]
    [SerializeField] private Color costUnaffordableColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Tooltip("비표시 상태 텍스트 색상.")]
    [SerializeField] private Color costHiddenColor = new Color(1f, 1f, 1f, 0f);

    [Header("Cost Text Layout")]
    [Tooltip("비용 텍스트 고정 크기.")]
    [SerializeField] private Vector2 fixedCostTextSize = new Vector2(110f, 36f);

    [Tooltip("비용 텍스트 고정 폰트 크기.")]
    [SerializeField, Min(1f)] private float fixedCostFontSize = 18f;

    [Tooltip("부모 스케일이 커져도 텍스트가 커지지 않게 보정할지 여부.")]
    [SerializeField] private bool compensateParentScale = true;

    [Tooltip("비용 텍스트 위치 오프셋.")]
    [SerializeField] private Vector2 costTextAnchoredOffset = Vector2.zero;

    [Header("Hit Area Debug")]
    [Tooltip("평상시 hit area 색상. 보통 완전 투명.")]
    [SerializeField] private Color hitAreaNormalColor = new Color(1f, 1f, 1f, 0f);

    [Tooltip("hover 중 hit area 색상. 보통 완전 투명 또는 아주 약한 값.")]
    [SerializeField] private Color hitAreaHoverColor = new Color(1f, 1f, 1f, 0f);

    /// <summary>
    /// 현재 마우스 hover 상태인지 저장한다.
    /// </summary>
    private bool isHovering;

    /// <summary>
    /// 비용 텍스트의 RectTransform 캐시.
    /// </summary>
    private RectTransform costTextRect;

    /// <summary>
    /// Reset 시 자동 참조를 연결한다.
    /// </summary>
    private void Reset()
    {
        button = GetComponent<Button>();
        hitAreaImage = GetComponent<Image>();

        if (costText == null)
        {
            costText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    /// <summary>
    /// Awake 시 기본 참조를 보정한다.
    /// </summary>
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (hitAreaImage == null)
        {
            hitAreaImage = GetComponent<Image>();
        }

        if (gridManager == null)
        {
            gridManager = Object.FindAnyObjectByType<GridManager>();
        }

        if (costText == null)
        {
            costText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (button != null)
        {
            button.transition = Selectable.Transition.None;

            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
        }

        if (hitAreaImage != null)
        {
            hitAreaImage.enabled = true;
            hitAreaImage.raycastTarget = true;
            hitAreaImage.color = hitAreaNormalColor;
        }

        CacheCostTextRect();
        ApplyFixedCostTextSettings();
    }

    /// <summary>
    /// 시작 시 텍스트 레이아웃과 초기 표시 상태를 맞춘다.
    /// </summary>
    private void Start()
    {
        ApplyFixedCostTextSettings();
        RefreshCostText();
    }

    /// <summary>
    /// 매 프레임 비용 텍스트의 크기 / 위치 / 스케일을 안정화한다.
    /// </summary>
    private void LateUpdate()
    {
        StabilizeCostTextTransform();
    }

    /// <summary>
    /// 버튼 매니저가 생성 직후 호출하는 초기화 함수.
    /// </summary>
    /// <param name="managerGrid">GridManager 참조</param>
    /// <param name="sourceRegion">기준 열린 구역</param>
    /// <param name="expandDirection">확장 방향</param>
    public void Setup(GridManager managerGrid, Vector2Int sourceRegion, KSM_ExpandDirection expandDirection)
    {
        gridManager = managerGrid;
        sourceRegionCoord = sourceRegion;
        direction = expandDirection;
        isHovering = false;

        RefreshInteractableState();
        RefreshHitAreaColor();
        ApplyFixedCostTextSettings();
        RefreshCostText();
    }

    /// <summary>
    /// 활성화 시 이벤트를 구독하고 상태를 갱신한다.
    /// </summary>
    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickExpand);
            button.onClick.AddListener(OnClickExpand);
        }

        GridManager.OnExpandStateChanged += HandleExpandStateChanged;
        ResourceManager.OnCurrencyChanged += HandleCurrencyChanged;
        PowerManager.OnTotalPowerChanged += HandleBoardOrAnimationChanged;

        ApplyFixedCostTextSettings();
        RefreshInteractableState();
        RefreshHitAreaColor();
        RefreshCostText();
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제하고 hover 강조를 정리한다.
    ///
    /// 중요:
    /// hover 중이던 버튼이 비활성화될 때 Tilemap이 이미 파괴된 상태일 수 있다.
    /// 이 경우 GridManager의 hover cleanup 내부에서 SetTile 계열 접근이 일어나면
    /// MissingReferenceException이 날 수 있으므로, groundTilemap 생존 여부를 먼저 확인한다.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickExpand);
        }

        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ResourceManager.OnCurrencyChanged -= HandleCurrencyChanged;
        PowerManager.OnTotalPowerChanged -= HandleBoardOrAnimationChanged;

        if (isHovering && gridManager != null && gridManager.groundTilemap != null)
        {
            gridManager.KSM_ClearExpansionHoverPreview();
        }

        isHovering = false;
    }

    /// <summary>
    /// hover 시작 시 hover 상태를 켜고,
    /// targetRegion 강조와 비용 텍스트 표시를 갱신한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (gridManager != null && gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction))
        {
            gridManager.KSM_ShowExpansionHoverPreview(sourceRegionCoord, direction);
        }

        RefreshHitAreaColor();
        RefreshCostText();
    }

    /// <summary>
    /// hover 중 이동 시에도 강조 상태와 비용 표시가 유지되도록 한다.
    /// </summary>
    public void OnPointerMove(PointerEventData eventData)
    {
        if (!isHovering)
        {
            isHovering = true;
        }

        if (gridManager != null && gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction))
        {
            gridManager.KSM_ShowExpansionHoverPreview(sourceRegionCoord, direction);
        }

        RefreshCostText();
    }

    /// <summary>
    /// hover 종료 시 hover 상태를 끄고 강조를 제거한다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (gridManager != null)
        {
            gridManager.KSM_ClearExpansionHoverPreview();
        }

        RefreshHitAreaColor();
        RefreshCostText();
    }

    /// <summary>
    /// 클릭 시 실제 확장을 시도한다.
    /// 성공 시 hover 강조를 지우고 Expansion SFX를 재생한다.
    /// </summary>
    private void OnClickExpand()
    {
        if (gridManager == null)
        {
            return;
        }

        KSM_ExpandResult result = gridManager.TryExpandFromRegion(sourceRegionCoord, direction);

        if (result == KSM_ExpandResult.Success)
        {
            gridManager.KSM_ClearExpansionHoverPreview();
            PlayExpansionSfx();
        }

        RefreshInteractableState();
        RefreshCostText();
    }

    /// <summary>
    /// 확장 성공 SFX를 안전하게 재생한다.
    /// 
    /// 역할:
    /// - KSM_SoundManager가 존재할 때만 Expansion SFX를 재생한다.
    /// - 사운드 매니저가 없더라도 오류 없이 넘어가게 한다.
    /// </summary>
    private void PlayExpansionSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlayExpansion();
        }
    }

    /// <summary>
    /// 확장 상태 변경 이벤트 수신 시 버튼 상태를 갱신한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        RefreshInteractableState();
        RefreshCostText();
    }

    /// <summary>
    /// 돈 변화 시 비용 텍스트 색상을 갱신한다.
    /// </summary>
    /// <param name="type">변화한 재화 타입</param>
    /// <param name="value">변화 후 값</param>
    private void HandleCurrencyChanged(CurrencyType type, int value)
    {
        if (type != CurrencyType.Money)
        {
            return;
        }

        RefreshCostText();
    }

    /// <summary>
    /// 보드 애니메이션 상태 변화 시 비용 텍스트 색상을 갱신한다.
    /// </summary>
    private void HandleBoardOrAnimationChanged()
    {
        RefreshCostText();
    }

    /// <summary>
    /// 현재 구조적으로 유효한 포트인지 기준으로
    /// 버튼 상호작용과 hit area raycast 상태를 갱신한다.
    /// </summary>
    private void RefreshInteractableState()
    {
        bool hasStructuralPort =
            gridManager != null &&
            gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction);

        if (button != null)
        {
            button.interactable = hasStructuralPort;
        }

        if (hitAreaImage != null)
        {
            hitAreaImage.raycastTarget = hasStructuralPort;

            if (!hasStructuralPort)
            {
                hitAreaImage.color = hitAreaNormalColor;
            }
        }
    }

    /// <summary>
    /// 현재 hover 상태에 맞게 hit area 색상을 갱신한다.
    /// </summary>
    private void RefreshHitAreaColor()
    {
        if (hitAreaImage == null)
        {
            return;
        }

        hitAreaImage.color = isHovering ? hitAreaHoverColor : hitAreaNormalColor;
    }

    /// <summary>
    /// 비용 텍스트를 갱신한다.
    /// hover 중일 때만 금액을 보여준다.
    /// </summary>
    private void RefreshCostText()
    {
        if (costText == null)
        {
            return;
        }

        bool hasStructuralPort =
            gridManager != null &&
            gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction);

        if (!hasStructuralPort || !isHovering)
        {
            costText.text = string.Empty;
            costText.color = costHiddenColor;
            return;
        }

        if (ResourceManager.Instance == null)
        {
            costText.text = "-";
            costText.color = costUnaffordableColor;
            return;
        }

        int cost = ResourceManager.Instance.GetExpandCost();
        costText.text = string.Format(costFormat, cost);

        bool canExpandNow =
            gridManager != null &&
            gridManager.CanExpandFromRegion(sourceRegionCoord, direction);

        costText.color = canExpandNow ? costAffordableColor : costUnaffordableColor;
    }

    /// <summary>
    /// 비용 텍스트 RectTransform을 캐시한다.
    /// </summary>
    private void CacheCostTextRect()
    {
        if (costText != null)
        {
            costTextRect = costText.rectTransform;
        }
    }

    /// <summary>
    /// 비용 텍스트 레이아웃을 고정한다.
    /// world space / screen space 모두에서 크기 튐을 줄인다.
    /// </summary>
    private void ApplyFixedCostTextSettings()
    {
        if (costText == null)
        {
            return;
        }

        if (costTextRect == null)
        {
            costTextRect = costText.rectTransform;
        }

        costText.enableAutoSizing = false;
        costText.fontSize = fixedCostFontSize;
        costText.alignment = TextAlignmentOptions.Center;
        costText.raycastTarget = false;
        costText.overflowMode = TextOverflowModes.Overflow;

        if (costTextRect != null)
        {
            costTextRect.anchorMin = new Vector2(0.5f, 0.5f);
            costTextRect.anchorMax = new Vector2(0.5f, 0.5f);
            costTextRect.pivot = new Vector2(0.5f, 0.5f);
            costTextRect.anchoredPosition = costTextAnchoredOffset;
            costTextRect.sizeDelta = fixedCostTextSize;
        }
    }

    /// <summary>
    /// 부모 스케일 영향을 받아 텍스트가 비정상적으로 커지지 않도록
    /// 매 프레임 비용 텍스트 Transform을 안정화한다.
    /// </summary>
    private void StabilizeCostTextTransform()
    {
        if (costTextRect == null)
        {
            return;
        }

        costTextRect.anchorMin = new Vector2(0.5f, 0.5f);
        costTextRect.anchorMax = new Vector2(0.5f, 0.5f);
        costTextRect.pivot = new Vector2(0.5f, 0.5f);
        costTextRect.anchoredPosition = costTextAnchoredOffset;
        costTextRect.sizeDelta = fixedCostTextSize;

        if (!compensateParentScale)
        {
            costTextRect.localScale = Vector3.one;
            return;
        }

        Transform parentTransform = costTextRect.parent;

        if (parentTransform == null)
        {
            costTextRect.localScale = Vector3.one;
            return;
        }

        Vector3 parentLossyScale = parentTransform.lossyScale;
        float safeX = Mathf.Max(0.0001f, Mathf.Abs(parentLossyScale.x));
        float safeY = Mathf.Max(0.0001f, Mathf.Abs(parentLossyScale.y));

        costTextRect.localScale = new Vector3(1f / safeX, 1f / safeY, 1f);
    }
}