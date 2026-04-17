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

    [Tooltip("세션별 '일일' 전력 → 돈 교환 캡 ($). 매 턴 시작마다 사용량이 0으로 리셋된다.")]
    [FormerlySerializedAs("sessionExchangeCapCurve")]
    [SerializeField]
    private AnimationCurve dailyExchangeCapCurve = AnimationCurve.Linear(0f, 20f, 10f, 2000f);

    [Header("확장 비용 곡선 (X = 누적 확장 횟수, Y = $ 비용)")]
    [Tooltip("그리드 확장 누적 횟수에 따른 비용 ($). 확장은 영구적이므로 세션을 넘어도 카운터가 유지된다.")]
    [SerializeField]
    private AnimationCurve expandCostCurve = AnimationCurve.Linear(0f, 50f, 10f, 1000f);

    [Header("기본 뽑기 비용 곡선")]
    [Tooltip("X = SessionIndex → 세션별 기본 뽑기 베이스 비용 ($).")]
    [SerializeField]
    private AnimationCurve basicDrawBaseCostCurve = AnimationCurve.Linear(0f, 10f, 10f, 100f);

    [Tooltip("X = 세션 내 기본 뽑기 사용 횟수 → 베이스 비용에 곱해지는 배율. 세션 시작 시 0으로 리셋.")]
    [SerializeField]
    private AnimationCurve basicDrawUsageMultiplierCurve = AnimationCurve.Linear(0f, 1f, 20f, 2f);

    [Header("테마 뽑기 비용 곡선")]
    [Tooltip("X = SessionIndex → 세션별 테마(색상) 뽑기 베이스 비용 ($).")]
    [SerializeField]
    private AnimationCurve themeDrawBaseCostCurve = AnimationCurve.Linear(0f, 30f, 10f, 300f);

    [Tooltip("X = 세션 내 테마 뽑기 사용 횟수 → 베이스 비용에 곱해지는 배율. 세션 시작 시 0으로 리셋.")]
    [SerializeField]
    private AnimationCurve themeDrawUsageMultiplierCurve = AnimationCurve.Linear(0f, 1f, 10f, 3f);

    [Header("특수 뽑기 (티켓)")]
    [Tooltip("특수 블록 뽑기 비용 (티켓)")]
    public int specialDrawCost = 1;

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

    // Day / Session
    private int totalDay;
    private int currentDDay;
    private int sessionIndex;
    private int todayProduction;
    private int dailyProductionGoal;
    private bool gameOver;
    private bool lastCanSkip;

    // Exchange
    private int dailyExchangeCap;         // 세션 시작 시 고정, 매일 사용
    private int moneyExchangedToday;      // 매 턴 시작마다 0 리셋

    // Usage counters
    private int expandCount;                   // 영구 누적
    private int basicDrawCountThisSession;     // 세션 내 리셋
    private int themeDrawCountThisSession;     // 세션 내 리셋

    // ==========================================================
    //   Events
    // ==========================================================

    public static event Action<CurrencyType, int> OnCurrencyChanged;
    public static event Action OnDayChanged;
    public static event Action<int> OnSessionAdvanced;
    public static event Action<bool> OnSkipAvailability;
    public static event Action OnGameOver;

    // ==========================================================
    //   Public Properties
    // ==========================================================

    public int TotalDay                => totalDay;
    public int CurrentD_Day            => currentDDay;
    public int SessionIndex            => sessionIndex;
    public int D_DayInterval           => dDay;
    public int DailyProductionGoal     => dailyProductionGoal;
    public int TodayProduction         => todayProduction;
    public int DailyExchangeCap        => dailyExchangeCap;
    public int MoneyExchangedToday     => moneyExchangedToday;
    public int RemainingExchangeCap    => Mathf.Max(0, dailyExchangeCap - moneyExchangedToday);
    public int ExchangeRatio           => exchangeRatio;
    public int ExpandCount             => expandCount;
    public int BasicDrawCountThisSession => basicDrawCountThisSession;
    public int ThemeDrawCountThisSession => themeDrawCountThisSession;
    public bool IsGameOver             => gameOver;

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

        ClampCurveWrapModes();
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

    private void ClampCurveWrapModes()
    {
        AnimationCurve[] allCurves =
        {
            dailyProductionGoalCurve,
            dailyExchangeCapCurve,
            expandCostCurve,
            basicDrawBaseCostCurve,
            basicDrawUsageMultiplierCurve,
            themeDrawBaseCostCurve,
            themeDrawUsageMultiplierCurve
        };

        foreach (var curve in allCurves)
        {
            if (curve == null) continue;
            curve.preWrapMode  = WrapMode.ClampForever;
            curve.postWrapMode = WrapMode.ClampForever;
        }
    }

    private void InitializeWallet()
    {
        wallet[CurrencyType.Electricity] = startingElectricity;
        wallet[CurrencyType.Money]       = startingMoney;
        wallet[CurrencyType.Ticket]      = startingTickets;

        totalDay        = 1;
        todayProduction = 0;
        expandCount     = 0;
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

    public void AddElectric(int amount) => AddCurrency(CurrencyType.Electricity, amount);
    public bool SpendElectric(int cost) => SpendCurrency(CurrencyType.Electricity, cost);
    public void AddMoney(int amount)    => AddCurrency(CurrencyType.Money, amount);
    public bool SpendMoney(int cost)    => SpendCurrency(CurrencyType.Money, cost);
    public void AddTicket(int amount)   => AddCurrency(CurrencyType.Ticket, amount);
    public bool SpendTicket(int count)  => SpendCurrency(CurrencyType.Ticket, count);

    // ==========================================================
    //   Day / Session Flow
    // ==========================================================

    public void ProcessNextDay()
    {
        if (gameOver) return;

        // 매 턴 시작마다 일일 교환 사용량을 0으로 리셋
        moneyExchangedToday = 0;

        // 1. 오늘 생산량 스냅샷
        todayProduction = PowerManager.Instance != null ? PowerManager.Instance.GetTotalPower() : 0;
        AddCurrency(CurrencyType.Electricity, todayProduction);

        // 2. 자동 교환 (오늘의 캡 범위 내)
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
        sessionIndex               = newSessionIndex;
        currentDDay                = dDay;
        moneyExchangedToday        = 0;
        basicDrawCountThisSession  = 0;
        themeDrawCountThisSession  = 0;

        dailyProductionGoal = Mathf.Max(0, EvaluateCurve(dailyProductionGoalCurve, sessionIndex));
        dailyExchangeCap    = Mathf.Max(0, EvaluateCurve(dailyExchangeCapCurve, sessionIndex));

        if (!isInitial)
        {
            Debug.Log($"<color=magenta>세션 {sessionIndex + 1} 시작!</color> 목표 {dailyProductionGoal} GWh/day · 일일 교환 캡 ${dailyExchangeCap}");
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
        moneyExchangedToday += moneyGained;

        Debug.Log($"<color=yellow>자동 교환</color> {electricitySpent} GWh → ${moneyGained} (오늘 ${moneyExchangedToday}/${dailyExchangeCap})");
    }

    // ==========================================================
    //   Cost Queries & Pay Helpers
    // ==========================================================

    public int GetExpandCost()
    {
        return Mathf.Max(0, EvaluateCurve(expandCostCurve, expandCount));
    }

    public int GetBasicDrawCost()
    {
        float baseCost   = Mathf.Max(0f, basicDrawBaseCostCurve.Evaluate(sessionIndex));
        float multiplier = Mathf.Max(0f, basicDrawUsageMultiplierCurve.Evaluate(basicDrawCountThisSession));
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
    }

    public int GetThemeDrawCost()
    {
        float baseCost   = Mathf.Max(0f, themeDrawBaseCostCurve.Evaluate(sessionIndex));
        float multiplier = Mathf.Max(0f, themeDrawUsageMultiplierCurve.Evaluate(themeDrawCountThisSession));
        return Mathf.Max(0, Mathf.RoundToInt(baseCost * multiplier));
    }

    public bool TryPayForExpand()
    {
        int cost = GetExpandCost();
        if (!SpendMoney(cost)) return false;

        expandCount++;
        UpdateUI();
        return true;
    }

    public bool TryPayForBasicDraw()
    {
        int cost = GetBasicDrawCost();
        if (!SpendMoney(cost)) return false;

        basicDrawCountThisSession++;
        UpdateUI();
        return true;
    }

    public bool TryPayForThemeDraw()
    {
        int cost = GetThemeDrawCost();
        if (!SpendMoney(cost)) return false;

        themeDrawCountThisSession++;
        UpdateUI();
        return true;
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
            exchangeCapText.text = $"Today Exchange: ${moneyExchangedToday} / ${dailyExchangeCap}";

        if (expandCostText != null)
            expandCostText.text = $"Expand: ${GetExpandCost()}";
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

    private static int EvaluateCurve(AnimationCurve curve, int x)
    {
        if (curve == null) return 0;
        return Mathf.RoundToInt(curve.Evaluate(x));
    }
}
