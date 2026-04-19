using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KSM_MapExpandButton
///
/// 역할
/// 1. targetRegion 전체를 덮는 "투명 hit area" 역할을 한다.
/// 2. 실제로 보이는 것은 중앙의 작은 버튼 비주얼이다.
/// 3. Hover 시 GridManager 쪽 타일 프리뷰를 호출한다.
/// 4. Click 시 실제 확장을 시도한다.
/// 5. 돈이 부족해도 구조적으로 확장 가능한 땅이면 hover 프리뷰는 보여준다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class KSM_MapExpandButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("확장 로직을 담당하는 GridManager.")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("이 버튼이 연결된 기준 열린 구역 좌표.")]
    [SerializeField] private Vector2Int sourceRegionCoord = Vector2Int.zero;

    [Tooltip("이 버튼이 담당하는 확장 방향.")]
    [SerializeField] private KSM_ExpandDirection direction = KSM_ExpandDirection.North;

    [Tooltip("실제 클릭 처리용 Button.")]
    [SerializeField] private Button button;

    [Tooltip("루트 hit area 용 Image. 보통 루트 오브젝트의 Image를 넣는다.")]
    [SerializeField] private Image hitAreaImage;

    [Tooltip("실제로 보이는 중앙 버튼 비주얼 루트.")]
    [SerializeField] private RectTransform centerVisualRoot;

    [Tooltip("중앙 버튼 비주얼 색상 변경용 Image.")]
    [SerializeField] private Image centerVisualImage;

    [Tooltip("비용 텍스트. 필요 없으면 비워도 된다.")]
    [SerializeField] private TextMeshProUGUI expandCostText;

    [Tooltip("Hover 시 켜질 Glow 오브젝트.")]
    [SerializeField] private GameObject hoverGlowObject;

    [Tooltip("Glow 색상 변경용 Image.")]
    [SerializeField] private Image hoverGlowImage;

    [Tooltip("비용 표시 포맷. {0} 자리에 숫자가 들어간다.")]
    [SerializeField] private string costFormat = "$ {0}";

    [Header("Hit Area Colors")]
    [Tooltip("평상시 hit area 색상. 완전 투명 권장.")]
    [SerializeField] private Color hitAreaNormalColor = new Color(1f, 1f, 1f, 0f);

    [Tooltip("Hover 시 hit area 색상. 완전 투명 또는 아주 약한 값 권장.")]
    [SerializeField] private Color hitAreaHoverColor = new Color(1f, 1f, 1f, 0f);

    [Header("Center Visual Colors")]
    [Tooltip("구매 가능 상태의 기본 중앙 버튼 색상.")]
    [SerializeField] private Color centerNormalColor = new Color(0f, 0f, 0f, 0.80f);

    [Tooltip("Hover + 구매 가능 상태의 중앙 버튼 색상.")]
    [SerializeField] private Color centerHoverColor = new Color(0.15f, 0.15f, 0.15f, 0.92f);

    [Tooltip("돈 부족 등으로 지금 당장 구매 불가 상태의 중앙 버튼 색상.")]
    [SerializeField] private Color centerDisabledColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("애니메이션 중 등 시스템적으로 막혀 있을 때 중앙 버튼 색상.")]
    [SerializeField] private Color centerBlockedColor = new Color(0f, 0f, 0f, 0.35f);

    [Header("Glow Colors")]
    [Tooltip("Hover + 구매 가능 상태일 때 Glow 색상.")]
    [SerializeField] private Color hoverGlowCanExpandColor = new Color(1f, 0.95f, 0.55f, 0.85f);

    [Tooltip("Hover + 구매 불가 상태일 때 Glow 색상.")]
    [SerializeField] private Color hoverGlowCannotExpandColor = new Color(0.55f, 0.55f, 0.55f, 0.60f);

    [Header("Cost Text Colors")]
    [Tooltip("비용 텍스트 기본 색상.")]
    [SerializeField] private Color costNormalColor = Color.white;

    [Tooltip("비용 텍스트 비활성 색상.")]
    [SerializeField] private Color costDisabledColor = new Color(0.70f, 0.70f, 0.70f, 0.90f);

    /// <summary>
    /// 현재 Hover 중인지 저장한다.
    /// </summary>
    private bool isHovering;

    /// <summary>
    /// Reset 시 자동 참조를 연결한다.
    /// </summary>
    private void Reset()
    {
        button = GetComponent<Button>();
        hitAreaImage = GetComponent<Image>();

        if (expandCostText == null)
        {
            expandCostText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (centerVisualRoot == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform childRect = transform.GetChild(i) as RectTransform;
                if (childRect != null)
                {
                    centerVisualRoot = childRect;
                    break;
                }
            }
        }

        if (centerVisualImage == null && centerVisualRoot != null)
        {
            centerVisualImage = centerVisualRoot.GetComponent<Image>();
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

        if (centerVisualImage == null && centerVisualRoot != null)
        {
            centerVisualImage = centerVisualRoot.GetComponent<Image>();
        }

        if (hitAreaImage != null)
        {
            hitAreaImage.raycastTarget = true;
        }

        if (centerVisualImage != null)
        {
            centerVisualImage.raycastTarget = false;
        }

        if (hoverGlowImage != null)
        {
            hoverGlowImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// 버튼 매니저가 생성 직후 호출하는 초기화 함수.
    /// </summary>
    public void Setup(GridManager managerGrid, Vector2Int sourceRegion, KSM_ExpandDirection expandDirection)
    {
        gridManager = managerGrid;
        sourceRegionCoord = sourceRegion;
        direction = expandDirection;
        isHovering = false;

        RefreshVisual();
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

        ResourceManager.OnCurrencyChanged += HandleCurrencyChanged;
        PowerManager.OnTotalPowerChanged += HandleBoardOrAnimationChanged;
        GridManager.OnExpandStateChanged += HandleExpandStateChanged;

        RefreshVisual();
    }

    /// <summary>
    /// 비활성화 시 이벤트를 해제하고 프리뷰를 제거한다.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickExpand);
        }

        ResourceManager.OnCurrencyChanged -= HandleCurrencyChanged;
        PowerManager.OnTotalPowerChanged -= HandleBoardOrAnimationChanged;
        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;

        if (isHovering && gridManager != null)
        {
            gridManager.ClearExpansionPreview();
        }

        isHovering = false;
        RefreshGlow(false, false, false, false);
    }

    /// <summary>
    /// Hover 시작 시 프리뷰를 보여준다.
    /// 여기서는 "구매 가능"이 아니라 "구조적으로 확장 가능한 땅인지"를 기준으로 프리뷰를 띄운다.
    /// 그래야 돈이 부족해도 어떤 땅을 살 수 있는지 사용자에게 보인다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (gridManager != null && gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction))
        {
            gridManager.PreviewExpansionAreaFromRegion(sourceRegionCoord, direction);
        }

        RefreshVisual();
    }

    /// <summary>
    /// Hover 종료 시 프리뷰를 제거한다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;

        if (gridManager != null)
        {
            gridManager.ClearExpansionPreview();
        }

        RefreshVisual();
    }

    /// <summary>
    /// 클릭 시 실제 확장을 시도한다.
    /// </summary>
    private void OnClickExpand()
    {
        if (gridManager == null)
        {
            return;
        }

        gridManager.TryExpandFromRegion(sourceRegionCoord, direction);
        RefreshVisual();
    }

    /// <summary>
    /// 돈 변화 시 버튼 상태를 갱신한다.
    /// </summary>
    private void HandleCurrencyChanged(CurrencyType type, int value)
    {
        if (type != CurrencyType.Money)
        {
            return;
        }

        RefreshVisual();
    }

    /// <summary>
    /// 발전량 변화 / 애니메이션 변화 시 버튼 상태를 갱신한다.
    /// </summary>
    private void HandleBoardOrAnimationChanged()
    {
        RefreshVisual();
    }

    /// <summary>
    /// 확장 성공 등으로 구조가 바뀌면 버튼 상태를 갱신한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        RefreshVisual();
    }

    /// <summary>
    /// 현재 상태에 맞게 hit area / 중앙 버튼 / glow / 비용 텍스트를 갱신한다.
    /// </summary>
    private void RefreshVisual()
    {
        bool hasGrid = (gridManager != null);

        /// <summary>
        /// 애니메이션 중 등 입력이 막혀야 하는 상태인지 검사한다.
        /// </summary>
        bool blocked = (PowerManager.Instance != null && PowerManager.Instance.IsAnimating);

        /// <summary>
        /// 구조적으로 아직 유효한 targetRegion인지 검사한다.
        /// </summary>
        bool hasStructuralPort = hasGrid && gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction);

        /// <summary>
        /// 현재 실제 구매까지 가능한 상태인지 검사한다.
        /// </summary>
        bool canExpandNow = hasGrid && gridManager.CanExpandFromRegion(sourceRegionCoord, direction);

        if (button != null)
        {
            button.interactable = hasStructuralPort && !blocked;
        }

        if (hitAreaImage != null)
        {
            hitAreaImage.enabled = hasStructuralPort;
            hitAreaImage.raycastTarget = hasStructuralPort;
            hitAreaImage.color = isHovering ? hitAreaHoverColor : hitAreaNormalColor;
        }

        if (centerVisualRoot != null)
        {
            centerVisualRoot.gameObject.SetActive(hasStructuralPort);
        }

        if (centerVisualImage != null)
        {
            if (!hasGrid || blocked)
            {
                centerVisualImage.color = centerBlockedColor;
            }
            else if (!canExpandNow)
            {
                centerVisualImage.color = centerDisabledColor;
            }
            else if (isHovering)
            {
                centerVisualImage.color = centerHoverColor;
            }
            else
            {
                centerVisualImage.color = centerNormalColor;
            }
        }

        RefreshGlow(isHovering, hasStructuralPort, canExpandNow, blocked);
        RefreshCostText(hasStructuralPort, canExpandNow, blocked);
    }

    /// <summary>
    /// Hover Glow 상태를 갱신한다.
    /// </summary>
    private void RefreshGlow(bool hovering, bool hasStructuralPort, bool canExpandNow, bool blocked)
    {
        GameObject glowTarget = hoverGlowObject;

        if (glowTarget == null && hoverGlowImage != null)
        {
            glowTarget = hoverGlowImage.gameObject;
        }

        if (glowTarget == null)
        {
            return;
        }

        bool shouldShowGlow = hovering && hasStructuralPort && !blocked;
        glowTarget.SetActive(shouldShowGlow);

        if (!shouldShowGlow)
        {
            return;
        }

        if (hoverGlowImage != null)
        {
            hoverGlowImage.color = canExpandNow ? hoverGlowCanExpandColor : hoverGlowCannotExpandColor;
            hoverGlowImage.raycastTarget = false;
        }
    }

    /// <summary>
    /// 비용 텍스트를 갱신한다.
    /// </summary>
    private void RefreshCostText(bool hasStructuralPort, bool canExpandNow, bool blocked)
    {
        if (expandCostText == null)
        {
            return;
        }

        expandCostText.gameObject.SetActive(hasStructuralPort);

        if (!hasStructuralPort)
        {
            return;
        }

        if (ResourceManager.Instance == null)
        {
            expandCostText.text = "-";
            expandCostText.color = costDisabledColor;
            return;
        }

        int cost = ResourceManager.Instance.GetExpandCost();
        expandCostText.text = string.Format(costFormat, cost);

        if (blocked || !canExpandNow)
        {
            expandCostText.color = costDisabledColor;
        }
        else
        {
            expandCostText.color = costNormalColor;
        }
    }
}