using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class KSM_MapExpandButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Vector2Int sourceRegionCoord = Vector2Int.zero;
    [SerializeField] private KSM_ExpandDirection direction = KSM_ExpandDirection.North;
    [SerializeField] private Button button;
    [SerializeField] private Image hitAreaImage;

    [Header("Cost Text")]
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private string costFormat = "{0}";
    [SerializeField] private Color costAffordableColor = new Color(1f, 0.95f, 0.65f, 1f);
    [SerializeField] private Color costUnaffordableColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color costHiddenColor = new Color(1f, 1f, 1f, 0f);

    [Header("Cost Text Layout")]
    [SerializeField] private Vector2 fixedCostTextSize = new Vector2(110f, 36f);
    [SerializeField, Min(1f)] private float fixedCostFontSize = 18f;
    [SerializeField] private bool compensateParentScale = true;
    [SerializeField] private Vector2 costTextAnchoredOffset = Vector2.zero;

    [Header("Hit Area Debug")]
    [SerializeField] private Color hitAreaNormalColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private Color hitAreaHoverColor = new Color(1f, 1f, 1f, 0f);

    private bool isHovering;
    private RectTransform costTextRect;

    private void Reset()
    {
        button = GetComponent<Button>();
        hitAreaImage = GetComponent<Image>();

        if (costText == null)
        {
            costText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

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
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
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

    private void Start()
    {
        ApplyFixedCostTextSettings();
        RefreshCostText();
    }

    private void LateUpdate()
    {
        StabilizeCostTextTransform();
    }

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

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClickExpand);
        }

        GridManager.OnExpandStateChanged -= HandleExpandStateChanged;
        ResourceManager.OnCurrencyChanged -= HandleCurrencyChanged;
        PowerManager.OnTotalPowerChanged -= HandleBoardOrAnimationChanged;

        if (isHovering && gridManager != null)
        {
            gridManager.KSM_ClearExpansionHoverPreview();
        }

        isHovering = false;
    }

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
        }

        RefreshInteractableState();
        RefreshCostText();
    }

    private void HandleExpandStateChanged()
    {
        RefreshInteractableState();
        RefreshCostText();
    }

    private void HandleCurrencyChanged(CurrencyType type, int value)
    {
        if (type != CurrencyType.Money)
        {
            return;
        }

        RefreshCostText();
    }

    private void HandleBoardOrAnimationChanged()
    {
        RefreshCostText();
    }

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

    private void RefreshHitAreaColor()
    {
        if (hitAreaImage == null)
        {
            return;
        }

        hitAreaImage.color = isHovering ? hitAreaHoverColor : hitAreaNormalColor;
    }

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

    private void CacheCostTextRect()
    {
        if (costText != null)
        {
            costTextRect = costText.rectTransform;
        }
    }

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