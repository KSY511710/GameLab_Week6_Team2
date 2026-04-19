using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KSM_ESCMenuManager
/// 
/// 역할:
/// 1. ESC 키 입력으로 ESC 메뉴를 열고 닫는다.
/// 2. Back 버튼 클릭 시 ESC 메뉴를 닫는다.
/// 3. Exit 버튼 클릭 시 타이틀 씬으로 이동하거나 게임을 종료한다.
/// 4. 멀티/실시간 진행을 고려하여 Time.timeScale 을 사용하지 않는다.
/// 
/// 권장 UI 구조:
/// - ESCMenuCanvas
///   - ESCPanel
///     - BackButton
///     - ExitButton
/// 
/// 사용 방법:
/// 1. 게임 씬의 Canvas 아래에 ESCPanel을 만든다.
/// 2. 시작 시 ESCPanel은 비활성화 상태로 둔다.
/// 3. 이 스크립트를 빈 오브젝트(예: ESCMenuManager)에 부착한다.
/// 4. escMenuRoot 에 ESCPanel 연결
/// 5. Back 버튼 OnClick -> OnClickBack 연결
/// 6. Exit 버튼 OnClick -> OnClickExit 연결
/// 
/// Exit 동작:
/// - exitToTitleScene 이 true 이면 titleSceneName 으로 이동
/// - exitToTitleScene 이 false 이면 게임 자체 종료
/// </summary>
public class KSM_ESCMenuManager : MonoBehaviour
{
    [Header("ESC Menu UI")]
    [Tooltip("ESC 키를 눌렀을 때 켜고 끌 메뉴 루트 오브젝트(보통 Panel).")]
    [SerializeField] private GameObject escMenuRoot;

    [Header("Exit Settings")]
    [Tooltip("true면 Exit 버튼 클릭 시 타이틀 씬으로 이동한다. false면 게임을 종료한다.")]
    [SerializeField] private bool exitToTitleScene = true;

    [Tooltip("Exit 버튼이 타이틀로 이동할 때 불러올 씬 이름.")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Header("Input Settings")]
    [Tooltip("ESC 키로 메뉴를 토글할지 여부.")]
    [SerializeField] private bool allowEscapeToggle = true;

    /// <summary>
    /// 현재 ESC 메뉴가 열려 있는지 여부를 저장한다.
    /// </summary>
    private bool isMenuOpen = false;

    /// <summary>
    /// 시작 시 ESC 메뉴를 닫힌 상태로 초기화한다.
    /// </summary>
    private void Start()
    {
        SetMenuOpen(false);
    }

    /// <summary>
    /// 매 프레임 ESC 입력을 확인한다.
    /// ESC 키가 눌리면 메뉴를 열거나 닫는다.
    /// </summary>
    private void Update()
    {
        if (!allowEscapeToggle)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    /// <summary>
    /// 현재 ESC 메뉴 상태를 반전시킨다.
    /// 열려 있으면 닫고, 닫혀 있으면 연다.
    /// </summary>
    public void ToggleMenu()
    {
        SetMenuOpen(!isMenuOpen);
    }

    /// <summary>
    /// Back 버튼 클릭 시 호출되는 함수.
    /// ESC 메뉴를 닫고 게임으로 돌아간다.
    /// </summary>
    public void OnClickBack()
    {
        SetMenuOpen(false);
    }

    /// <summary>
    /// Exit 버튼 클릭 시 호출되는 함수.
    /// 설정에 따라 타이틀 씬으로 이동하거나 게임을 종료한다.
    /// </summary>
    public void OnClickExit()
    {
        if (exitToTitleScene)
        {
            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogWarning("[KSM_ESCMenuManager] titleSceneName 이 비어 있습니다. 인스펙터에서 타이틀 씬 이름을 지정하세요.");
                return;
            }

            SceneManager.LoadScene(titleSceneName);
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>
    /// ESC 메뉴를 강제로 여는 함수.
    /// 다른 시스템에서 ESC 메뉴를 열고 싶을 때 사용할 수 있다.
    /// </summary>
    public void OpenMenu()
    {
        SetMenuOpen(true);
    }

    /// <summary>
    /// ESC 메뉴를 강제로 닫는 함수.
    /// 다른 시스템에서 ESC 메뉴를 닫고 싶을 때 사용할 수 있다.
    /// </summary>
    public void CloseMenu()
    {
        SetMenuOpen(false);
    }

    /// <summary>
    /// 메뉴의 실제 열림/닫힘 상태를 적용한다.
    /// UI 활성화와 내부 상태값을 함께 관리한다.
    /// </summary>
    /// <param name="open">true면 메뉴를 열고, false면 메뉴를 닫는다.</param>
    private void SetMenuOpen(bool open)
    {
        isMenuOpen = open;

        if (escMenuRoot != null)
        {
            escMenuRoot.SetActive(open);
        }
        else
        {
            Debug.LogWarning("[KSM_ESCMenuManager] escMenuRoot 가 연결되지 않았습니다.");
        }

        // 멀티/실시간 진행을 고려하여 Time.timeScale 은 건드리지 않는다.
        // 필요하면 여기서 마우스 커서 표시만 제어하면 된다.
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }
}