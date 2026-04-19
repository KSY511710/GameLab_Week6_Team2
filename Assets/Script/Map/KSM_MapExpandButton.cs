using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// KSM_MapExpandButton
///
/// 열린 구역 하나의 특정 방향 포트를 대표하는 확장 버튼.
/// 이번 버전에서는 작은 경계 버튼이 아니라
/// "미구매 targetRegion 중앙을 덮는 큰 오버레이 버튼"으로 사용한다.
///
/// 핵심 역할:
/// 1. Hover 시 확장 프리뷰를 보여준다.
/// 2. Hover 시 glow를 켠다.
/// 3. Click 시 실제 확장을 시도한다.
/// 4. 현재 구매 가능 상태에 따라 색상 / 텍스트 / glow 상태를 갱신한다.
/// 5. 돈이 부족해도 구조적으로 확장 가능한 땅이면 버튼은 계속 보이게 유지한다.
/// </summary>
[RequireComponent(typeof(Button))]
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

    [Tooltip("오버레이 본체 색상 변경용 Image.")]
    [SerializeField] private Image targetImage;

    [Tooltip("확장 비용을 표시할 TMP 텍스트. 필요 없으면 비워둬도 된다.")]
    [SerializeField] private TextMeshProUGUI expandCostText;

    [Tooltip("Hover 시 켜질 Glow 오브젝트. 보통 자식 Image 오브젝트를 연결한다.")]
    [SerializeField] private GameObject hoverGlowObject;

    [Tooltip("Glow 색상 변경용 Image. 필요하면 연결한다.")]
    [SerializeField] private Image hoverGlowImage;

    [Tooltip("비용 표시 포맷. {0} 자리에 숫자가 들어간다.")]
    [SerializeField] private string costFormat = "$ {0}";

    [Header("Overlay Colors")]
    [Tooltip("기본 상태. 미구매 땅을 검정색 반투명으로 덮는 색.")]
    [SerializeField] private Color normalColor = new Color(0f, 0f, 0f, 0.65f);

    [Tooltip("Hover + 현재 구매 가능 상태일 때 색상.")]
    [SerializeField] private Color hoverCanExpandColor = new Color(0f, 0f, 0f, 0.30f);

    [Tooltip("Hover + 현재 구매 불가 상태일 때 색상.")]
    [SerializeField] private Color hoverCannotExpandColor = new Color(0f, 0f, 0f, 0.55f);

    [Tooltip("돈 부족 등으로 지금 당장 구매 불가일 때 기본 색상.")]
    [SerializeField] private Color disabledColor = new Color(0f, 0f, 0f, 0.82f);

    [Tooltip("애니메이션 중 등 시스템적으로 막혀 있을 때 색상.")]
    [SerializeField] private Color blockedColor = new Color(0f, 0f, 0f, 0.90f);

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
    /// Reset 시 자기 자신의 Button / Image를 자동 연결한다.
    /// </summary>
    private void Reset()
    {
        button = GetComponent<Button>();
        targetImage = GetComponent<Image>();

        if (expandCostText == null)
        {
            expandCostText = GetComponentInChildren<TextMeshProUGUI>(true);
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

        if (targetImage != null)
        {
            targetImage.raycastTarget = true;
        }
    }

    /// <summary>
    /// 버튼 매니저가 생성 직후 호출하는 초기화 함수.
    /// 이 버튼이 어떤 열린 구역의 어떤 방향을 대표하는지 저장한다.
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
    /// OnDisable 시 이벤트를 해제한다.
    /// Hover 중이었다면 프리뷰도 제거한다.
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
        RefreshGlow(false, false, false);
    }

    /// <summary>
    /// Hover 시작 시 상태를 저장하고 프리뷰를 갱신한다.
    /// 현재 구매 가능하면 프리뷰를 보여주고,
    /// 구매 불가면 glow / 색만 바뀌고 프리뷰는 띄우지 않는다.
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
    /// Hover 종료 시 프리뷰를 제거하고 상태를 원복한다.
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
    /// 확장 성공 등으로 상태가 바뀌면 버튼 상태를 다시 갱신한다.
    /// </summary>
    private void HandleExpandStateChanged()
    {
        RefreshVisual();
    }

    /// <summary>
    /// 클릭 시 실제 확장을 시도한다.
    /// 버튼은 구조적으로 포트가 살아 있으면 계속 보이게 유지되므로,
    /// 돈이 부족해도 클릭은 들어가고 실제 성공/실패는 GridManager가 판단한다.
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
    /// 현재 상태에 맞게 버튼 색상 / interactable / 비용 텍스트 / glow를 갱신한다.
    /// </summary>
    private void RefreshVisual()
    {
        bool hasGrid = (gridManager != null);

        /// <summary>
        /// 애니메이션 중 등 시스템적으로 입력을 막아야 하는 상태인지 검사한다.
        /// </summary>
        bool blocked = (PowerManager.Instance != null && PowerManager.Instance.IsAnimating);

        /// <summary>
        /// 이 버튼이 구조적으로 아직 유효한 targetRegion을 가리키는지 검사한다.
        /// 돈과 무관하게 "버튼을 보여줄지" 결정하는 기준이다.
        /// </summary>
        bool hasStructuralPort = hasGrid && gridManager.HasStructuralExpansionPort(sourceRegionCoord, direction);

        /// <summary>
        /// 현재 실제 구매까지 가능한 상태인지 검사한다.
        /// 돈 / 설정 / 애니메이션 조건 등을 모두 포함한다.
        /// </summary>
        bool canExpandNow = hasGrid && gridManager.CanExpandFromRegion(sourceRegionCoord, direction);

        if (button != null)
        {
            // 구조적으로 포트가 살아 있고, 시스템적으로 막힌 상태가 아닐 때는
            // 버튼 자체는 항상 눌리게 둔다.
            // 실제 성공 / 실패는 GridManager.TryExpandFromRegion에서 처리한다.
            button.interactable = hasStructuralPort && !blocked;
        }

        if (targetImage != null)
        {
            if (!hasGrid || blocked)
            {
                targetImage.color = blockedColor;
            }
            else if (!canExpandNow)
            {
                targetImage.color = isHovering ? hoverCannotExpandColor : disabledColor;
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

        RefreshGlow(isHovering, canExpandNow, blocked);
        RefreshCostText(canExpandNow, blocked);
    }

    /// <summary>
    /// Hover Glow 상태를 갱신한다.
    /// </summary>
    private void RefreshGlow(bool hovering, bool canExpandNow, bool blocked)
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

        bool shouldShowGlow = hovering && !blocked;
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
    private void RefreshCostText(bool canExpandNow, bool blocked)
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