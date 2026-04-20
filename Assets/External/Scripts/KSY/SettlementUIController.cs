using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 📊 1. 정산 데이터 바구니 (반드시 파일 맨 위에 있어야 에러가 안 납니다!)
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
    public CanvasGroup dimPanel;           // 검은 반투명 배경
    public GameObject settlementPanel;     // 정산 UI 전체 부모
    public RectTransform printerRect;      // 프린터 컨테이너 (부품들을 묶은 투명 상자)
    public RectTransform paperRect;        // 영수증 종이

    [Header("🌟 2. 애니메이션 좌표 & 속도")]
    public Vector2 printerStartPos;        // 대기 (오른쪽 밖)
    public Vector2 printerCenterPos;       // 출력 (중앙)
    public Vector2 printerExitPos;         // 퇴장 (왼쪽 밖)
    public Vector2 paperHiddenPos;         // 종이 숨김 (프린터 안)
    public Vector2 paperPrintedPos;        // 종이 나옴 (프린터 밖)
    public float moveSpeed = 0.5f;

    [Header("📊 3. 전력 막대그래프 (RectTransform - Width 조절)")]
    public RectTransform redPowerBar;
    public RectTransform bluePowerBar;
    public RectTransform greenPowerBar;
    public RectTransform scrapPowerBar;
    public float maxGaugeWidth = 400f;     // 🌟 게이지가 100%일 때의 최대 가로 길이

    [Header("📝 4. 텍스트 정보 (돈 게이지 삭제됨)")]
    public TextMeshProUGUI redText;
    public TextMeshProUGUI blueText;
    public TextMeshProUGUI greenText;
    public TextMeshProUGUI scrapText;
    public TextMeshProUGUI moneyText;      // 총 정산 금액 텍스트

    [Header("정산 속도")]
    public float fillSpeed = 1.5f;

    private float sharedMoneyTracker = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        if (dimPanel != null) dimPanel.alpha = 0f;
        if (settlementPanel != null) settlementPanel.SetActive(false);
    }

    public void PlaySettlementAnimation(SettlementData data, Action onAnimationComplete)
    {
        if (settlementPanel == null) return;
        settlementPanel.SetActive(true);
        StartCoroutine(AnimateSettlementSequence(data, onAnimationComplete));
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

        redText.text = $"{data.redPower:F0} GWh";
        blueText.text = $"{data.bluePower:F0} GWh";
        greenText.text = $"{data.greenPower:F0} GWh";
        scrapText.text = $"{data.scrapPower:F0} GWh";
        moneyText.text = "총정산: $0";

        printerRect.anchoredPosition = printerStartPos;
        paperRect.anchoredPosition = paperHiddenPos;

        // ==========================================
        // 2단계: 등장 애니메이션 (배경 -> 프린터 -> 영수증)
        // ==========================================
        yield return StartCoroutine(LerpAlpha(dimPanel, 0f, 1f, 0.3f));
        yield return StartCoroutine(LerpPosition(printerRect, printerStartPos, printerCenterPos, moveSpeed));
        yield return StartCoroutine(LerpPosition(paperRect, paperHiddenPos, paperPrintedPos, moveSpeed));

        yield return new WaitForSeconds(0.3f);

        // ==========================================
        // 3단계: 정산 애니메이션 (Width 줄어들고 돈 올라감)
        // ==========================================
        yield return StartCoroutine(DrainPowerAndFillMoney(redPowerBar, redText, data.redPower, data.redMoney));
        yield return StartCoroutine(DrainPowerAndFillMoney(bluePowerBar, blueText, data.bluePower, data.blueMoney));
        yield return StartCoroutine(DrainPowerAndFillMoney(greenPowerBar, greenText, data.greenPower, data.greenMoney));
        yield return StartCoroutine(DrainPowerAndFillMoney(scrapPowerBar, scrapText, data.scrapPower, data.scrapMoney));

        yield return new WaitForSeconds(2.0f);

        // ==========================================
        // 4단계: 퇴장 애니메이션 (영수증 -> 프린터 -> 배경)
        // ==========================================
        yield return StartCoroutine(LerpPosition(paperRect, paperPrintedPos, paperHiddenPos, moveSpeed));
        yield return StartCoroutine(LerpPosition(printerRect, printerCenterPos, printerExitPos, moveSpeed));
        yield return StartCoroutine(LerpAlpha(dimPanel, 1f, 0f, 0.3f));

        settlementPanel.SetActive(false);
        onAnimationComplete?.Invoke();
    }

    // 🌟 헬퍼 함수: 막대그래프 너비 설정
    private void SetBarWidth(RectTransform bar, float ratio)
    {
        if (bar != null)
        {
            bar.sizeDelta = new Vector2(maxGaugeWidth * ratio, bar.sizeDelta.y);
        }
    }

    // 🌟 핵심 로직: 전력 바 줄어들고 돈 텍스트 올라가는 애니메이션
    private IEnumerator DrainPowerAndFillMoney(RectTransform powerBar, TextMeshProUGUI powerText, float startPower, float earnedMoney)
    {
        if (startPower <= 0 || powerBar == null) yield break;

        float t = 0;
        float startMoney = sharedMoneyTracker;
        float targetMoney = sharedMoneyTracker + earnedMoney;
        float startWidth = powerBar.sizeDelta.x; // 현재 너비 기억

        while (t < 1f)
        {
            t += Time.deltaTime * fillSpeed;

            // 1. 전력 숫자 줄어듦
            powerText.text = $"{Mathf.Lerp(startPower, 0f, t):F0} GWh";

            // 2. 게이지 바 길이(Width) 줄어듦
            powerBar.sizeDelta = new Vector2(Mathf.Lerp(startWidth, 0f, t), powerBar.sizeDelta.y);

            // 3. 돈 숫자 올라감 (N0로 천 단위 콤마 찍기)
            sharedMoneyTracker = Mathf.Lerp(startMoney, targetMoney, t);
            moneyText.text = $"총정산: ${sharedMoneyTracker:N0}";

            yield return null;
        }

        // 최종 수치 고정
        powerBar.sizeDelta = new Vector2(0f, powerBar.sizeDelta.y);
        powerText.text = "0 GWh";
        sharedMoneyTracker = targetMoney;
        moneyText.text = $"총정산: ${sharedMoneyTracker:N0}";

        yield return new WaitForSeconds(0.3f);
    }

    // 🌟 헬퍼 함수: 부드러운 위치 이동
    private IEnumerator LerpPosition(RectTransform rect, Vector2 start, Vector2 end, float duration)
    {
        if (rect == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Sin(elapsed / duration * Mathf.PI * 0.5f); // Ease-Out 효과
            rect.anchoredPosition = Vector2.Lerp(start, end, t);
            yield return null;
        }
        rect.anchoredPosition = end;
    }

    // 🌟 헬퍼 함수: 부드러운 투명도 변경
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