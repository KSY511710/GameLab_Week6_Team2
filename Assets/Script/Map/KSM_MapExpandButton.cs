using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KSM_MapExpandButton
/// 
/// 열린 구역 하나의 특정 방향 포트를 담당하는 확장 버튼.
/// 
/// 이 버튼은 단순히 방향만 기억하는 것이 아니라
/// "어떤 구역(sourceRegion)의 어떤 방향(direction)인지"를 함께 기억한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class KSM_MapExpandButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [Tooltip("확장 로직을 담당하는 GridManager.")]
    [SerializeField] private GridManager gridManager;

    [Tooltip("이 버튼이 붙은 기준 구역 좌표.")]
    [SerializeField] private Vector2Int sourceRegionCoord = Vector2Int.zero;

    [Tooltip("이 버튼이 담당하는 확장 방향.")]
    [SerializeField] private KSM_ExpandDirection direction = KSM_ExpandDirection.North;

    [Tooltip("실제 클릭 처리용 Button.")]
    [SerializeField] private Button button;

    [Tooltip("색상 변경용 Image.")]
    [SerializeField] private Image targetImage;

    [Tooltip("확장 비용을 표시할 TMP 텍스트.")]
    [SerializeField] private TextMeshProUGUI expandCostText;

    [Tooltip("비용 표시 포맷. {0} 자리에 숫자가 들어간다.")]
    [SerializeField] private string costFormat = "$ {0}";

    [Header("Colors")]
    [Tooltip("기본 버튼 색상.")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.95f);

    [Tooltip("Hover + 확장 가능 상태 색상.")]
    [SerializeField] private Color hoverCanExpandColor = new Color(0.90f, 0.90f, 0.90f, 1f);

    [Tooltip("확장 불가능할 때의 버튼 색상.")]
    [SerializeField] private Color disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.70f);

    [Tooltip("시퀀스 중 등으로 시스템적으로 막혀 있을 때의 색상.")]
    [SerializeField] private Color blockedColor = new Color(0.35f, 0.35f, 0.35f, 0.60f);

    [Tooltip("비용 텍스트 기본 색상.")]
    [SerializeField] private Color costNormalColor = Color.white;

    [Tooltip("비용 텍스트 비활성 색상.")]
    [SerializeField] private Color costDisabledColor = new Color(0.70f, 0.70f, 0.70f, 0.85f);

    /// <summary>
    /// 현재 Hover 중인지 저장한다.
    /// </summary>
    private bool isHovering;

    /// <summary>
    /// Reset 시 자기 자신의 Button / Image를 자동 연결한다.
    /// </summary>
    private void Reset()
    {
        button = GetComponent<Button>();
        targetImage = GetComponent<Image>();

        if (expandCostText == null)
        {
            expandCostText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    /// <summary>
    /// Awake 시 기본 참조 자동 보정.
    /// </summary>
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        if (gridManager == null)
        {
            gridManager = Object.FindAnyObjectByType<GridManager>();
        }
    }

    /// <summary>
    /// 버튼 매니저가 생성 직후 호출하는 초기화 함수.
    /// 이 버튼이 어떤 구역의 어떤 방향 포트인지 지정한다.
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
    /// OnEnable 시 이벤트 구독 및 상태 동기화.
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
    /// OnDisable 시 이벤트 해제.
    /// 
    /// 중요:
    /// 버튼 재생성/비활성화 시 무조건 프리뷰를 지우면
    /// hover 도중 깜빡이는 현상이 더 심해질 수 있다.
    /// 
    /// 그래서 실제로 hover 중인 버튼일 때만 프리뷰를 지운다.
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
    }

    /// <summary>
    /// Hover 시작 시 이 포트가 확장 가능하면 프리뷰를 보여준다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;

        if (gridManager != null && gridManager.CanExpandFromRegion(sourceRegionCoord, direction))
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
    /// 돈 변화 시 버튼 상태를 다시 계산한다.
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
    /// 총 발전량 / 애니메이션 상태 변화 시 버튼 상태를 다시 계산한다.
    /// </summary>
    private void HandleBoardOrAnimationChanged()
    {
        RefreshVisual();
    }

    /// <summary>
    /// 확장 성공 등으로 상태가 바뀌면 비용 / 버튼 상태를 다시 갱신한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
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

        if (!gridManager.CanExpandFromRegion(sourceRegionCoord, direction))
        {
            RefreshVisual();
            return;
        }

        KSM_ExpandResult result = gridManager.TryExpandFromRegion(sourceRegionCoord, direction);

        switch (result)
        {
            case KSM_ExpandResult.Success:
            case KSM_ExpandResult.NotEnoughMoney:
            case KSM_ExpandResult.Busy:
            case KSM_ExpandResult.InvalidSetup:
            default:
                RefreshVisual();
                break;
        }
    }

    /// <summary>
    /// 현재 상태에 맞게 버튼 interactable / 색상 / 비용 텍스트를 갱신한다.
    /// </summary>
    private void RefreshVisual()
    {
        bool hasGrid = (gridManager != null);
        bool blocked = (PowerManager.Instance != null && PowerManager.Instance.IsAnimating);
        bool canExpand = hasGrid && gridManager.CanExpandFromRegion(sourceRegionCoord, direction);

        if (button != null)
        {
            button.interactable = canExpand;
        }

        if (targetImage != null)
        {
            if (!hasGrid || blocked)
            {
                targetImage.color = blockedColor;
            }
            else if (!canExpand)
            {
                targetImage.color = disabledColor;
            }
            else if (isHovering)
            {
                targetImage.color = hoverCanExpandColor;
            }
            else
            {
                targetImage.color = normalColor;
            }
        }

        RefreshCostText(canExpand, blocked);
    }

    /// <summary>
    /// 비용 텍스트를 갱신한다.
    /// </summary>
    private void RefreshCostText(bool canExpand, bool blocked)
    {
        if (expandCostText == null)
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

        if (blocked || !canExpand)
        {
            expandCostText.color = costDisabledColor;
        }
        else
        {
            expandCostText.color = costNormalColor;
        }
    }
}