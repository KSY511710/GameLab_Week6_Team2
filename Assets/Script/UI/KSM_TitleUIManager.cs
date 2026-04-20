using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KSM_TitleUIManager
/// 
/// 역할:
/// 1. 타이틀 씬의 Start 버튼 클릭 처리
/// 2. 타이틀 씬의 Exit 버튼 클릭 처리
/// 3. 필요 시 시작할 게임 씬 이름을 인스펙터에서 지정
/// 
/// 사용 방법:
/// - 이 스크립트를 Title 씬의 빈 오브젝트(예: TitleUIManager)에 부착
/// - startSceneName 에 실제 게임 씬 이름 입력
/// - Start 버튼 OnClick -> OnClickStart 연결
/// - Exit 버튼 OnClick -> OnClickExit 연결
/// </summary>
public class KSM_TitleUIManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Start 버튼을 눌렀을 때 이동할 게임 씬 이름.")]
    [SerializeField] private string startSceneName = "GameScene";

    /// <summary>
    /// Start 버튼 클릭 시 호출되는 함수.
    /// 지정한 게임 씬으로 이동한다.
    /// </summary>
    public void OnClickStart()
    {
        if (string.IsNullOrWhiteSpace(startSceneName))
        {
            Debug.LogWarning("[KSM_TitleUIManager] startSceneName 이 비어 있습니다. 인스펙터에서 시작 씬 이름을 지정하세요.");
            return;
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