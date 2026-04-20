using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// KSM_ESCMenuManager
///
/// 역할:
/// 1. ESC 키 입력으로 ESC 패널을 열고 닫는다.
/// 2. Back 버튼 클릭 시 ESC 패널을 닫는다.
/// 3. Exit 버튼 클릭 시 타이틀 씬으로 이동하거나 게임을 종료한다.
/// 4. 게임 정지(Time.timeScale 변경)는 하지 않는다.
/// 5. MainPanel 같은 다른 UI는 건드리지 않고, ESC 패널만 켜고 끈다.
///
/// 사용 방법:
/// 1. 이 스크립트를 빈 오브젝트(예: KSM_ESCMenuManager)에 부착한다.
/// 2. escPanelRoot 에 ESC 패널 오브젝트를 연결한다.
/// 3. ESC 패널은 시작 시 꺼져 있어도 되고, 켜져 있어도 Start에서 자동으로 꺼진다.
/// 4. Back 버튼 OnClick에 OnClickBack 연결
/// 5. Exit 버튼 OnClick에 OnClickExit 연결
/// </summary>
public class KSM_ESCMenuManager : MonoBehaviour
{
    [Header("ESC Panel")]
    [Tooltip("ESC 키를 눌렀을 때 열고 닫을 ESC 패널 루트 오브젝트.")]
    [SerializeField] private GameObject escPanelRoot;

    [Header("Input")]
    [Tooltip("ESC 키로 패널을 열고 닫을지 여부.")]
    [SerializeField] private bool allowEscapeToggle = true;

    [Header("Exit Settings")]
    [Tooltip("true면 Exit 버튼 클릭 시 타이틀 씬으로 이동한다. false면 게임 종료.")]
    [SerializeField] private bool exitToTitleScene = true;

    [Tooltip("Exit 버튼 클릭 시 이동할 타이틀 씬 이름.")]
    [SerializeField] private string titleSceneName = "TitleScene";

    /// <summary>
    /// 현재 ESC 패널이 열려 있는지 여부를 저장한다.
    /// </summary>
    private bool isEscPanelOpen = false;

    /// <summary>
    /// 시작 시 ESC 패널을 닫힌 상태로 초기화한다.
    /// </summary>
    private void Start()
    {
        SetEscPanelOpen(false);
    }

    /// <summary>
    /// 매 프레임 ESC 키 입력을 검사하여 ESC 패널을 열고 닫는다.
    /// </summary>
    private void Update()
    {
        if (!allowEscapeToggle)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleEscPanel();
        }
    }

    /// <summary>
    /// ESC 패널 상태를 반전시킨다.
    /// 열려 있으면 닫고, 닫혀 있으면 연다.
    /// </summary>
    public void ToggleEscPanel()
    {
        SetEscPanelOpen(!isEscPanelOpen);
    }

    /// <summary>
    /// ESC 패널을 연다.
    /// 외부에서 직접 호출하고 싶을 때 사용할 수 있다.
    /// </summary>
    public void OpenEscPanel()
    {
        SetEscPanelOpen(true);
    }

    /// <summary>
    /// ESC 패널을 닫는다.
    /// 외부에서 직접 호출하고 싶을 때 사용할 수 있다.
    /// </summary>
    public void CloseEscPanel()
    {
        SetEscPanelOpen(false);
    }

    /// <summary>
    /// Back 버튼 클릭 시 호출된다.
    /// ESC 패널만 닫는다.
    /// </summary>
    public void OnClickBack()
    {
        SetEscPanelOpen(false);
    }

    /// <summary>
    /// Exit 버튼 클릭 시 호출된다.
    /// 설정에 따라 타이틀 씬으로 이동하거나 게임을 종료한다.
    /// </summary>
    public void OnClickExit()
    {
        if (exitToTitleScene)
        {
            if (string.IsNullOrWhiteSpace(titleSceneName))
            {
                Debug.LogWarning("[KSM_ESCMenuManager] titleSceneName 이 비어 있습니다. 인스펙터에서 확인하세요.");
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
    /// ESC 패널의 실제 활성화 상태를 적용한다.
    /// </summary>
    /// <param name="open">true면 열기, false면 닫기</param>
    private void SetEscPanelOpen(bool open)
    {
        isEscPanelOpen = open;

        if (escPanelRoot == null)
        {
            Debug.LogWarning("[KSM_ESCMenuManager] escPanelRoot 가 연결되지 않았습니다.");
            return;
        }

        escPanelRoot.SetActive(open);
    }
}