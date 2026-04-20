using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KSM_TitleUIManager
/// 
/// 역할:
/// 1. 타이틀 씬 진입 시 Title BGM을 재생한다.
/// 2. 타이틀 씬의 Start 버튼 클릭 처리
/// 3. 타이틀 씬의 Exit 버튼 클릭 처리
/// 4. 필요 시 시작할 게임 씬 이름을 인스펙터에서 지정
/// 
/// 사용 방법:
/// - 이 스크립트를 Title 씬의 빈 오브젝트(예: TitleUIManager)에 부착
/// - startSceneName 에 실제 게임 씬 이름 입력
/// - Start 버튼 OnClick -> OnClickStart 연결
/// - Exit 버튼 OnClick -> OnClickExit 연결
/// 
/// 전제 조건:
/// - 씬 어딘가에 KSM_SoundManager가 반드시 하나 있어야 한다.
/// - KSM_SoundManager는 DontDestroyOnLoad로 유지된다.
/// - KSM_SoundManager의 BGM Entries에 Title / Main 클립이 연결되어 있어야 한다.
/// - KSM_SoundManager의 startBgm은 None으로 두는 것을 권장한다.
/// </summary>
public class KSM_TitleUIManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Start 버튼을 눌렀을 때 이동할 게임 씬 이름.")]
    [SerializeField] private string startSceneName = "GameScene";

    [Header("BGM Settings")]
    [Tooltip("타이틀 씬 진입 시 Title BGM을 자동 재생할지 여부.")]
    [SerializeField] private bool playTitleBgmOnStart = true;

    [Tooltip("게임 시작 버튼을 눌렀을 때 Main BGM으로 전환할지 여부.")]
    [SerializeField] private bool switchToMainBgmOnStartClick = true;

    [Tooltip("Main BGM 전환 시 사용할 크로스페이드 시간.")]
    [SerializeField, Min(0f)] private float mainBgmFadeSeconds = 0.6f;

    /// <summary>
    /// 타이틀 씬 시작 시 호출된다.
    /// 여기서 Title BGM을 재생한다.
    /// </summary>
    private void Start()
    {
        if (!playTitleBgmOnStart)
        {
            return;
        }

        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.PlayBgm(KSM_BgmType.Title, 0f);
        }
        else
        {
            Debug.LogWarning("[KSM_TitleUIManager] KSM_SoundManager.Instance 가 없습니다. Title BGM을 재생할 수 없습니다.");
        }
    }

    /// <summary>
    /// Start 버튼 클릭 시 호출되는 함수.
    /// Main BGM으로 전환한 뒤 지정한 게임 씬으로 이동한다.
    /// </summary>
    public void OnClickStart()
    {
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogWarning("[KSM_TitleUIManager] startSceneName 이 비어 있습니다. 인스펙터에서 시작 씬 이름을 지정하세요.");
            return;
        }

        if (switchToMainBgmOnStartClick)
        {
            if (KSM_SoundManager.Instance != null)
            {
                KSM_SoundManager.Instance.PlayBgm(KSM_BgmType.Main, mainBgmFadeSeconds);
            }
            else
            {
                Debug.LogWarning("[KSM_TitleUIManager] KSM_SoundManager.Instance 가 없습니다. Main BGM 전환 없이 씬을 이동합니다.");
            }
        }

        SceneManager.LoadScene(startSceneName);
    }

    /// <summary>
    /// Exit 버튼 클릭 시 호출되는 함수.
    /// 에디터에서는 플레이를 종료하고,
    /// 빌드된 게임에서는 애플리케이션을 종료한다.
    /// </summary>
    public void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}