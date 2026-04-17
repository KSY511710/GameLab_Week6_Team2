using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // 게임 오버 시 씬 전환용

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    [Header("바꿀거 (초기 세팅값)")]
    [SerializeField] private int TotalElectricity = 0;
    [SerializeField] private int CurrentDay = 1;

    [Header("비용 설정")]
    public int drawCost = 50;             // 뽑기 기본 비용
    public int expandCost = 100;          // 확장 기본 비용
    public float expandMultiplier = 1.5f; // 확장할 때마다 비용 증가율
    public int darwSumCost = 10;

    [Header("목표 설정 (10일마다 검사)")]
    public int baseGoal = 200;            // 10일차 첫 목표 전력
    public int goalInterval = 10;         // 검사 주기 (10일)
    public float GoalMultipier = 2;

    [Header("안바꿔도 되는거 (실제 런타임 데이터)")]
    public int _totalElectricity = 0;
    public int _currentDay = 0;

    [Header("UI References")]
    public TextMeshProUGUI totalElectricityText;
    public TextMeshProUGUI dayText;
    public TextMeshProUGUI expandCostText;
    public TextMeshProUGUI goalText;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        _currentDay = CurrentDay;
        _totalElectricity = TotalElectricity;
        UpdateUI();
    }

    // 📌 [핵심] 다음날 처리
    public void ProcessNextDay()
    {
        int dailyProduction = PowerManager.Instance.GetTotalPower();
        AddElectric(dailyProduction);

        // 하루가 끝나기 전에 목표 달성 여부 체크 (10, 20, 30일...)
        if (_currentDay % goalInterval == 0)
        {
            CheckGoalReached();
        }

        _currentDay++;
        UpdateUI();

        Debug.Log($"<color=green>{_currentDay - 1}일 종료!</color> 생산량 {dailyProduction} 추가됨. 총 전력: {_totalElectricity}");
    }

    // 📌 목표 검사 및 게임 오버 처리
    private void CheckGoalReached()
    {
        int currentGoal = GetCurrentGoal();

        if (_totalElectricity < currentGoal)
        {
            Debug.Log($"<color=red>게임 오버!</color> 목표 {currentGoal} GWh 미달 (현재: {_totalElectricity} GWh)");
            // TODO: 게임 오버 UI 띄우기
        }
        else
        {
            Debug.Log($"<color=cyan>{_currentDay}일차 목표({currentGoal} GWh) 달성!</color> 생존했습니다.");
            _totalElectricity-= currentGoal;
        }
    }

    // 📌 현재 스테이지 목표 계산 공식
    public int GetCurrentGoal()
    {
        int stage = _currentDay / goalInterval;
        return baseGoal * (int)Mathf.Pow(2, stage - 1);
    }

    // 📌 전력 증가
    public void AddElectric(int Production)
    {
        _totalElectricity += Production;
    }

    // 📌 전력 소모 (뽑기, 확장 시 사용)
    public bool SpendElectric(int cost)
    {
        if (_totalElectricity >= cost)
        {
            _totalElectricity -= cost;
            UpdateUI();
            return true; // 차감 성공
        }

        Debug.Log("<color=red>전기가 부족합니다!</color>");
        return false; // 차감 실패
    }

    // 📌 부지 확장 시 비용 증가
    public void IncreaseExpandCost()
    {
        expandCost = (int)(expandCost * expandMultiplier);
        UpdateUI();
    }

    // 📌 UI 갱신 (빠진 것 없이 모두 포함)
    private void UpdateUI()
    {
        if (totalElectricityText != null) totalElectricityText.text = $"Total: {_totalElectricity} GWh";
        if (dayText != null) dayText.text = $"Day {_currentDay}";
        if (expandCostText != null) expandCostText.text = $"Expand: {expandCost} GWh";

        // 다음 목표 텍스트 갱신
        if (goalText != null)
        {
            int nextGoalDay = ((_currentDay - 1) / goalInterval + 1) * goalInterval;
            int nextGoalAmount = baseGoal * (int)Mathf.Pow(GoalMultipier, (nextGoalDay / goalInterval) - 1);
            goalText.text = $"Goal: {nextGoalAmount} GWh (Day {nextGoalDay})";
        }
    }
 
    
}