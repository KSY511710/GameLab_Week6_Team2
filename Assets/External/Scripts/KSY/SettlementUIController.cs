using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 📊 1. 정산 데이터 바구니
public class SettlementData
{
    public float redPower, bluePower, greenPower;
    public float redMoney, blueMoney, greenMoney;
    public float scrapPower;
    public float scrapMoney;
    public float totalMoneyCap;
}

// 🎮 2. 정산 UI 컨트롤러 메인 클래스
public class SettlementUIController : MonoBehaviour
{
    public static SettlementUIController Instance;

    [Header("🌟 1. 애니메이션 UI (프린터 컨테이너 & 배경)")]
    public CanvasGroup dimPanel;
    public GameObject settlementPanel;
    public RectTransform printerRect;
    public RectTransform paperRect;

    [Header("🌟 2. 애니메이션 좌표 & 속도")]
    public Vector2 printerStartPos;
    public Vector2 printerCenterPos;
    public Vector2 printerExitPos;
    public Vector2 paperHiddenPos;
    public Vector2 paperPrintedPos;
    public float moveSpeed = 0.5f;

    [Header("📊 3. 전력 막대그래프 (RectTransform - Width 조절)")]
    public RectTransform redPowerBar;
    public RectTransform bluePowerBar;
    public RectTransform greenPowerBar;
    public RectTransform scrapPowerBar;
    public float maxGaugeWidth = 400f;

    [Header("📝 4. 텍스트 정보 (돈 게이지 삭제됨)")]
    public TextMeshProUGUI redText;
    public TextMeshProUGUI blueText;
    public TextMeshProUGUI greenText;
    public TextMeshProUGUI scrapText;
    public TextMeshProUGUI moneyText;

    [Header("정산 속도")]
    public float fillSpeed = 1.5f;

    [Header("정산 구간 간격")]
    [Tooltip("한 기업 정산이 끝나고 다음 기업 정산이 시작되기 전 짧은 텀.")]
    public float settlementStepGap = 0.12f;

    private float sharedMoneyTracker = 0f;
    private Coroutine activeSettlementCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (dimPanel != null) dimPanel.alpha = 0f;
        if (settlementPanel != null) settlementPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.StopMoneyExchangeSfx(0f);
        }
    }

    public void PlaySettlementAnimation(SettlementData data, Action onAnimationComplete)
    {
        if (settlementPanel == null) return;

        if (activeSettlementCoroutine != null)
        {
            StopCoroutine(activeSettlementCoroutine);
            activeSettlementCoroutine = null;
        }

        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.StopMoneyExchangeSfx(0f);
        }

        settlementPanel.SetActive(true);
        activeSettlementCoroutine = StartCoroutine(AnimateSettlementSequence(data, onAnimationComplete));
    }

    private IEnumerator AnimateSettlementSequence(SettlementData data, Action onAnimationComplete)
    {
        // ==========================================
        // 1단계: 초기 데이터 및 UI 크기 셋팅
        // ==========================================
        float maxPower = Mathf.Max(data.redPower, data.bluePower, data.greenPower, data.scrapPower, 1f);
        sharedMoneyTracker = 0f;

        SetBarWidth(redPowerBar, data.redPower / maxPower);
        SetBarWidth(bluePowerBar, data.bluePower / maxPower);
        SetBarWidth(greenPowerBar, data.greenPower / maxPower);
        SetBarWidth(scrapPowerBar, data.scrapPower / maxPower);

        if (redText != null) redText.text = $"{data.redPower:F0} GWh";
        if (blueText != null) blueText.text = $"{data.bluePower:F0} GWh";
        if (greenText != null) greenText.text = $"{data.greenPower:F0} GWh";
        if (scrapText != null) scrapText.text = $"{data.scrapPower:F0} GWh";
        if (moneyText != null) moneyText.text = "총정산: $0";

        if (printerRect != null) printerRect.anchoredPosition = printerStartPos;
        if (paperRect != null) paperRect.anchoredPosition = paperHiddenPos;

        // ==========================================
        // 2단계: 등장 애니메이션 (배경 -> 프린터 -> 영수증)
        // ==========================================
        yield return StartCoroutine(LerpAlpha(dimPanel, 0f, 1f, 0.3f));
        yield return StartCoroutine(LerpPosition(printerRect, printerStartPos, printerCenterPos, moveSpeed));
        yield return StartCoroutine(LerpPosition(paperRect, paperHiddenPos, paperPrintedPos, moveSpeed));

        yield return new WaitForSeconds(0.3f);

        // ==========================================
        // 3단계: 기업별 정산 애니메이션
        // 각 기업 시작 시 MoneyExchange 시작
        // 각 기업이 0이 되면 MoneyExchange 즉시 정지
        // ==========================================
        yield return StartCoroutine(DrainPowerAndFillMoneyWithSfx(redPowerBar, redText, data.redPower, data.redMoney));
        yield return StartCoroutine(DrainPowerAndFillMoneyWithSfx(bluePowerBar, blueText, data.bluePower, data.blueMoney));
        yield return StartCoroutine(DrainPowerAndFillMoneyWithSfx(greenPowerBar, greenText, data.greenPower, data.greenMoney));
        yield return StartCoroutine(DrainPowerAndFillMoneyWithSfx(scrapPowerBar, scrapText, data.scrapPower, data.scrapMoney));

        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.StopMoneyExchangeSfx(0f);
        }
        PlayMoneyAllSfx(data);

        yield return new WaitForSeconds(2.0f);

        // ==========================================
        // 4단계: 퇴장 애니메이션 (영수증 -> 프린터 -> 배경)
        // ==========================================
        yield return StartCoroutine(LerpPosition(paperRect, paperPrintedPos, paperHiddenPos, moveSpeed));
        yield return StartCoroutine(LerpPosition(printerRect, printerCenterPos, printerExitPos, moveSpeed));
        yield return StartCoroutine(LerpAlpha(dimPanel, 1f, 0f, 0.3f));

        if (settlementPanel != null)
        {
            settlementPanel.SetActive(false);
        }

        activeSettlementCoroutine = null;
        onAnimationComplete?.Invoke();
    }

    private void PlayMoneyAllSfx(SettlementData data)
    {
        if (KSM_SoundManager.Instance == null)
        {
            return;
        }

        float totalMoney =
            data.redMoney +
            data.blueMoney +
            data.greenMoney +
            data.scrapMoney;

        if (totalMoney <= 0f)
        {
            return;
        }

        KSM_SoundManager.Instance.PlayMoneyAll();
    }
    private void SetBarWidth(RectTransform bar, float ratio)
    {
        if (bar != null)
        {
            bar.sizeDelta = new Vector2(maxGaugeWidth * ratio, bar.sizeDelta.y);
        }
    }

    /// <summary>
    /// 기업 하나의 정산 구간을 처리한다.
    /// 시작 시 MoneyExchange를 켜고,
    /// 0이 되는 즉시 끈다.
    /// </summary>
    private IEnumerator DrainPowerAndFillMoneyWithSfx(
        RectTransform powerBar,
        TextMeshProUGUI powerText,
        float startPower,
        float earnedMoney)
    {
        if (startPower <= 0f || powerBar == null)
        {
            yield break;
        }

        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.StartMoneyExchangeSfx();
        }

        yield return StartCoroutine(DrainPowerAndFillMoney(powerBar, powerText, startPower, earnedMoney));

        if (KSM_SoundManager.Instance != null)
        {
            KSM_SoundManager.Instance.StopMoneyExchangeSfx(0.05f);
        }

        if (settlementStepGap > 0f)
        {
            yield return new WaitForSeconds(settlementStepGap);
        }
    }

    /// <summary>
    /// 전력 바를 0까지 줄이고, 총 정산 금액을 올린다.
    /// 
    /// 주의:
    /// - 여기서는 끝난 뒤 추가 대기하지 않는다.
    /// - 소리가 0 되는 순간 바로 꺼져야 하므로, 대기는 바깥 코루틴에서 처리한다.
    /// </summary>
    private IEnumerator DrainPowerAndFillMoney(
        RectTransform powerBar,
        TextMeshProUGUI powerText,
        float startPower,
        float earnedMoney)
    {
        if (startPower <= 0f || powerBar == null)
        {
            yield break;
        }

        float t = 0f;
        float startMoney = sharedMoneyTracker;
        float targetMoney = sharedMoneyTracker + earnedMoney;
        float startWidth = powerBar.sizeDelta.x;

        while (t < 1f)
        {
            t += Time.deltaTime * fillSpeed;

            if (powerText != null)
            {
                powerText.text = $"{Mathf.Lerp(startPower, 0f, t):F0} GWh";
            }

            powerBar.sizeDelta = new Vector2(Mathf.Lerp(startWidth, 0f, t), powerBar.sizeDelta.y);

            sharedMoneyTracker = Mathf.Lerp(startMoney, targetMoney, t);

            if (moneyText != null)
            {
                moneyText.text = $"총정산: ${sharedMoneyTracker:N0}";
            }

            yield return null;
        }

        powerBar.sizeDelta = new Vector2(0f, powerBar.sizeDelta.y);

        if (powerText != null)
        {
            powerText.text = "0 GWh";
        }

        sharedMoneyTracker = targetMoney;

        if (moneyText != null)
        {
            moneyText.text = $"총정산: ${sharedMoneyTracker:N0}";
        }
    }

    private IEnumerator LerpPosition(RectTransform rect, Vector2 start, Vector2 end, float duration)
    {
        if (rect == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Sin(elapsed / duration * Mathf.PI * 0.5f);
            rect.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }

        rect.anchoredPosition = end;
    }

    private IEnumerator LerpAlpha(CanvasGroup cg, float start, float end, float duration)
    {
        if (cg == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        cg.alpha = end;
    }
}