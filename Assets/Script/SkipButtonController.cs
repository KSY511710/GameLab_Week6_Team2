using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skip 버튼의 활성/비활성을 ResourceManager.OnSkipAvailability 에 따라 자동 토글하고,
/// 버튼 클릭을 ResourceManager.TrySkip() 에 위임한다.
/// 클릭 시 UI Click SFX를 1회 재생한다.
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
        if (skipButton != null)
        {
            skipButton.interactable = value;
            skipButton.GetComponent<Image>().enabled = value;
        }
    }

    private void OnSkipClicked()
    {
        PlayUiClickSfx();
        ResourceManager.Instance?.TrySkip();
    }

    /// <summary>
    /// 일반 버튼 클릭음을 안전하게 재생한다.
    /// </summary>
    private void PlayUiClickSfx()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlayUiClick();
        }
    }
}