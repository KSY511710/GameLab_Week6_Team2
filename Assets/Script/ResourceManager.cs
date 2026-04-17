using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public enum CurrencyType
{
    Electricity,
    Money,
    Ticket
}

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance;

    // ==========================================================
    //   Inspector Config
    // ==========================================================

    [Header("세션 · 턴 설정")]
    [Tooltip("한 세션(일일 생산량 목표 검사 사이클)에 필요한 턴 수")]
    [SerializeField, Min(1)] private int dDay = 10;

    [Tooltip("전력 → 돈 교환 비율 (전력 N : 돈 1)")]
    [SerializeField, Min(1)] private int exchangeRatio = 10;

    [Header("세션 진행 곡선 (X = SessionIndex, Y = 값)")]
    [Tooltip("세션별 일일 생산 목표치 (GWh). 그래프 키프레임을 드래그해 난이도 곡선 조정.")]
    [SerializeField]
    private AnimationCurve dailyProductionGoalCurve = AnimationCurve.Linear(0f, 200f, 10f, 20000f);

    [Tooltip("세션별 전력 → 돈 교환 캡 ($). 세션 시작 시 고정, 세션 종료 전까지 불변.")]
    [SerializeField]
    private AnimationCurve sessionExchangeCapCurve = AnimationCurve.Linear(0f, 20f, 10f, 2000f);

    [Header("비용 설정")]
    [Tooltip("기본 블록 뽑기 비용 (돈, $)")]
    public int drawCost = 50;

    [Tooltip("특수 블록 뽑기 비용 (티켓)")]
    public int specialDrawCost = 1;

    [Tooltip("그리드 확장 비용 (전력, GWh)")]
    public int expandCost = 100;

    [Tooltip("확장할 때마다 비용이 곱해지는 배율")]
    public float expandMultiplier = 1.5f;

    [Header("스킵 보상")]
    [Tooltip("스킵 시 남은 D-Day 1일당 지급되는 티켓 수")]
    [SerializeField, Min(0)] private int ticketsPerSkippedDay = 1;

    [Header("초기 재화")]
    [SerializeField, Min(0)] private int startingElectricity = 0;
    [SerializeField, Min(0)] private int startingMoney = 0;
    [SerializeField, Min(0)] private int startingTickets = 0;

    [Header("UI References")]
    [FormerlySerializedAs("totalElectricityText")]
    public TextMeshProUGUI electricityText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI ticketText;
    public TextMeshProUGUI dayText;
    [FormerlySerializedAs("goalText")]
    public TextMeshProUGUI sessionGoalText;
    public TextMeshProUGUI exchangeCapText;
    public TextMeshProUGUI expandCostText;

    // ==========================================================
    //   Runtime State
    // ==========================================================

    private readonly Dictionary<CurrencyType, int> wallet = new();

    private int totalDay;
    private int currentDDay;
    private int sessionIndex;

    private int todayProduction;
    private int dailyProductionGoal;
    private int sessionExchangeCap;
    private int moneyExchangedThisSession;

    private bool gameOver;
    private bool lastCanSkip;

    // ==========================================================
    //   Events (UI/상위 시스템 디커플링용)
    // ==========================================================

    public static event Action<CurrencyType, int> OnCurrencyChanged;
    public static event Action OnDayChanged;
    public static event Action<int> OnSessionAdvanced;
    public static event Action<bool> OnSkipAvailability;
    public static event Action OnGameOver;

    // ==========================================================
    //   Public Properties
    // ==========================================================

    public int TotalDay                  => totalDay;
    public int CurrentD_Day              => currentDDay;
    public int SessionIndex              => sessionIndex;
    public int D_DayInterval             => dDay;
    public int DailyProductionGoal       => dailyProductionGoal;
    public int TodayProduction           => todayProduction;
    public int SessionExchangeCap        => sessionExchangeCap;
    public int MoneyExchangedThisSession => moneyExchangedThisSession;
    public int RemainingExchangeCap      => Mathf.Max(0, sessionExchangeCap - moneyExchangedThisSession);
    public int ExchangeRatio             => exchangeRatio;
    public bool IsGameOver               => gameOver;

    // ==========================================================
    //   Unity Lifecycle
    // ==========================================================

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 곡선 경계 바깥은 마지막 키프레임 값으로 수렴
        dailyProductionGoalCurve.preWrapMode  = WrapMode.ClampForever;
        dailyProductionGoalCurve.postWrapMode = WrapMode.ClampForever;
        sessionExchangeCapCurve.preWrapMode   = WrapMode.ClampForever;
        sessionExchangeCapCurve.postWrapMode  = WrapMode.ClampForever;

        InitializeWallet();
    }

    private void Start()
    {
        StartNewSession(0, isInitial: true);
        UpdateUI();
        EmitAllCurrencyEvents();
        RaiseDayEvent();
        RaiseSkipAvailabilityIfChanged();
    }

    private void InitializeWallet()
    {
        wallet[CurrencyType.Electricity] = startingElectricity;
        wallet[CurrencyType.Money]       = startingMoney;
        wallet[CurrencyType.Ticket]      = startingTickets;

        totalDay        = 1;
        todayProduction = 0;
    }

    // ==========================================================
    //   Wallet API
    // ==========================================================

    public int GetCurrency(CurrencyType type)
    {
        return wallet.TryGetValue(type, out int value) ? value : 0;
    }

    public bool HasCurrency(CurrencyType type, int amount)
    {
        return GetCurrency(type) >= amount;
    }

    public void AddCurrency(CurrencyType type, int amount)
    {
        if (amount <= 0) return;

        wallet[type] = GetCurrency(type) + amount;
        OnCurrencyChanged?.Invoke(type, wallet[type]);
        UpdateUI();
    }

    public bool SpendCurrency(CurrencyType type, int amount)
    {
        if (amount < 0) return false;

        int current = GetCurrency(type);
        if (current < amount)
        {
            Debug.Log($"<color=red>{type} 재화 부족!</color> (보유 {current} / 필요 {amount})");
            return false;
        }

        wallet[type] = current - amount;
        OnCurrencyChanged?.Invoke(type, wallet[type]);
        UpdateUI();
        return true;
    }

    // 호환 래퍼 — 기존 호출부 + 가독성
    public void AddElectric(int amount)  => AddCurrency(CurrencyType.Electricity, amount);
    public bool SpendElectric(int cost)  => SpendCurrency(CurrencyType.Electricity, cost);
    public void AddMoney(int amount)     => AddCurrency(CurrencyType.Money, amount);
    public bool SpendMoney(int cost)     => SpendCurrency(CurrencyType.Money, cost);
    public void AddTicket(int amount)    => AddCurrency(CurrencyType.Ticket, amount);
    public bool SpendTicket(int count)   => SpendCurrency(CurrencyType.Ticket, count);

    // ==========================================================
    //   Day / Session Flow
    // ==========================================================

    public void ProcessNextDay()
    {
        if (gameOver) return;

        // 1. 오늘 생산량 스냅샷
        todayProduction = PowerManager.Instance != null ? PowerManager.Instance.GetTotalPower() : 0;
        AddCurrency(CurrencyType.Electricity, todayProduction);

        // 2. 자동 교환 (세션 캡 범위 내)
        AutoExchangeElectricityForMoney();

        // 3. 시간 경과
        totalDay++;
        currentDDay--;
        RaiseDayEvent();

        Debug.Log($"<color=green>Day {totalDay - 1} 종료</color> | 생산 {todayProduction} GWh | 남은 D-Day {currentDDay}");

        // 4. D-Day 도달 시 목표 검사 → 세션 전환 or 게임 오버
        if (currentDDay <= 0)
        {
            CheckDailyGoalOrGameOver();
        }

        UpdateUI();
        RaiseSkipAvailabilityIfChanged();
    }

    public bool CanSkip()
    {
        return !gameOver && currentDDay > 0 && todayProduction >= dailyProductionGoal;
    }

    public bool TrySkip()
    {
        if (!CanSkip())
        {
            Debug.Log("<color=yellow>스킵 불가: 조건 미충족</color>");
            return false;
        }

        int skippedDays = currentDDay;
        int reward = ticketsPerSkippedDay * skippedDays;
        AddCurrency(CurrencyType.Ticket, reward);
        Debug.Log($"<color=cyan>스킵 성공!</color> 남은 {skippedDays}일 분 티켓 {reward}개 보상.");

        AdvanceToNextSession();
        UpdateUI();
        RaiseSkipAvailabilityIfChanged();
        return true;
    }

    private void CheckDailyGoalOrGameOver()
    {
        if (todayProduction < dailyProductionGoal)
        {
            gameOver = true;
            Debug.Log($"<color=red>게임 오버!</color> 일일 생산 {todayProduction} < 목표 {dailyProductionGoal}");
            OnGameOver?.Invoke();
            return;
        }

        Debug.Log($"<color=cyan>세션 {sessionIndex + 1} 일일 목표 달성!</color> ({todayProduction} ≥ {dailyProductionGoal})");
        AdvanceToNextSession();
    }

    private void AdvanceToNextSession()
    {
        StartNewSession(sessionIndex + 1, isInitial: false);
    }

    private void StartNewSession(int newSessionIndex, bool isInitial)
    {
        sessionIndex             = newSessionIndex;
        currentDDay              = dDay;
        moneyExchangedThisSession = 0;

        dailyProductionGoal = Mathf.Max(0, Mathf.RoundToInt(dailyProductionGoalCurve.Evaluate(sessionIndex)));
        sessionExchangeCap  = Mathf.Max(0, Mathf.RoundToInt(sessionExchangeCapCurve.Evaluate(sessionIndex)));

        if (!isInitial)
        {
            Debug.Log($"<color=magenta>세션 {sessionIndex + 1} 시작!</color> 목표 {dailyProductionGoal} GWh/day · 교환 캡 ${sessionExchangeCap}");
        }

        OnSessionAdvanced?.Invoke(sessionIndex);
        RaiseDayEvent();
    }

    // ==========================================================
    //   Exchange
    // ==========================================================

    private void AutoExchangeElectricityForMoney()
    {
        int remainingCap = RemainingExchangeCap;
        if (remainingCap <= 0) return;

        int electricity     = GetCurrency(CurrencyType.Electricity);
        int affordableMoney = electricity / exchangeRatio;
        int moneyGained     = Mathf.Min(remainingCap, affordableMoney);
        if (moneyGained <= 0) return;

        int electricitySpent = moneyGained * exchangeRatio;
        SpendCurrency(CurrencyType.Electricity, electricitySpent);
        AddCurrency(CurrencyType.Money, moneyGained);
        moneyExchangedThisSession += moneyGained;

        Debug.Log($"<color=yellow>자동 교환</color> {electricitySpent} GWh → ${moneyGained} (세션 누적 ${moneyExchangedThisSession}/${sessionExchangeCap})");
    }

    // ==========================================================
    //   Expand Cost
    // ==========================================================

    public void IncreaseExpandCost()
    {
        expandCost = Mathf.Max(1, (int)(expandCost * expandMultiplier));
        UpdateUI();
    }

    // ==========================================================
    //   UI / Events
    // ==========================================================

    private void UpdateUI()
    {
        if (electricityText != null)
            electricityText.text = $"Electricity: {GetCurrency(CurrencyType.Electricity)} GWh";

        if (moneyText != null)
            moneyText.text = $"Money: ${GetCurrency(CurrencyType.Money)}";

        if (ticketText != null)
            ticketText.text = $"Tickets: {GetCurrency(CurrencyType.Ticket)}";

        if (dayText != null)
            dayText.text = $"Day {totalDay} · D-{currentDDay}";

        if (sessionGoalText != null)
            sessionGoalText.text = $"Session {sessionIndex + 1}: {dailyProductionGoal} GWh/day";

        if (exchangeCapText != null)
            exchangeCapText.text = $"Exchange: ${moneyExchangedThisSession} / ${sessionExchangeCap}";

        if (expandCostText != null)
            expandCostText.text = $"Expand: {expandCost} GWh";
    }

    private void EmitAllCurrencyEvents()
    {
        OnCurrencyChanged?.Invoke(CurrencyType.Electricity, GetCurrency(CurrencyType.Electricity));
        OnCurrencyChanged?.Invoke(CurrencyType.Money,       GetCurrency(CurrencyType.Money));
        OnCurrencyChanged?.Invoke(CurrencyType.Ticket,      GetCurrency(CurrencyType.Ticket));
    }

    private void RaiseDayEvent()
    {
        OnDayChanged?.Invoke();
    }

    private void RaiseSkipAvailabilityIfChanged()
    {
        bool canSkipNow = CanSkip();
        if (canSkipNow != lastCanSkip)
        {
            lastCanSkip = canSkipNow;
            OnSkipAvailability?.Invoke(canSkipNow);
        }
    }
}
