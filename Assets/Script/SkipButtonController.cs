using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skip 버튼의 활성/비활성을 ResourceManager.OnSkipAvailability 에 따라 자동 토글하고,
/// 버튼 클릭을 ResourceManager.TrySkip() 에 위임한다.
/// </summary>
[RequireComponent(typeof(Button))]
public class SkipButtonController : MonoBehaviour
{
    [SerializeField] private Button skipButton;

    private void Reset()
    {
        skipButton = GetComponent<Button>();
    }

    private void Awake()
    {
        if (skipButton == null) skipButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        ResourceManager.OnSkipAvailability += SetInteractable;
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);

        // 처음 활성화 시 현재 상태로 동기화 (이벤트가 이전에 한 번만 발화됐을 수 있으므로)
        bool can = ResourceManager.Instance != null && ResourceManager.Instance.CanSkip();
        SetInteractable(can);
    }

    private void OnDisable()
    {
        ResourceManager.OnSkipAvailability -= SetInteractable;
        if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipClicked);
    }

    private void SetInteractable(bool value)
    {
        if (skipButton != null) skipButton.interactable = value;
    }

    private void OnSkipClicked()
    {
        ResourceManager.Instance?.TrySkip();
    }
}
