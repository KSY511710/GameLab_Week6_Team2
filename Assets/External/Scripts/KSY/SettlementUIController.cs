using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// 📊 정산 화면에 전달할 데이터 바구니
public class SettlementData
{
    public float redPower, bluePower, greenPower;
    public float redMoney, blueMoney, greenMoney;

    // 🌟 자투리(그룹 미소속) 데이터 추가
    public float scrapPower;
    public float scrapMoney;

    public float totalMoneyCap;
}

public class SettlementUIController : MonoBehaviour
{
    public static SettlementUIController Instance;

    [Header("UI 패널")]
    public GameObject settlementPanel;

    [Header("전력 막대그래프 (Image - Fill Amount)")]
    public Image redPowerBar;
    public Image bluePowerBar;
    public Image greenPowerBar;
    public Image scrapPowerBar; // 🌟 자투리용 막대그래프 추가 (회색 추천)

    [Header("돈 막대그래프 (Image - Fill Amount)")]
    public Image moneyBar;

    [Header("텍스트 정보")]
    public TextMeshProUGUI redText;
    public TextMeshProUGUI blueText;
    public TextMeshProUGUI greenText;
    public TextMeshProUGUI scrapText; // 🌟 자투리 텍스트 추가
    public TextMeshProUGUI moneyText;

    [Header("연출 속도")]
    public float fillSpeed = 1.5f;

    private float sharedMoneyTracker = 0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void PlaySettlementAnimation(SettlementData data, Action onAnimationComplete)
    {
        settlementPanel.SetActive(true);
        StartCoroutine(AnimateSettlementRoutine(data, onAnimationComplete));
    }

    private IEnumerator AnimateSettlementRoutine(SettlementData data, Action onAnimationComplete)
    {
        // 1. 초기 상태 세팅 (자투리 전력도 최댓값 계산에 포함)
        float maxPower = Mathf.Max(data.redPower, data.bluePower, data.greenPower, data.scrapPower, 1f);

        sharedMoneyTracker = 0f;

        redPowerBar.fillAmount = data.redPower / maxPower;
        bluePowerBar.fillAmount = data.bluePower / maxPower;
        greenPowerBar.fillAmount = data.greenPower / maxPower;
        scrapPowerBar.fillAmount = data.scrapPower / maxPower; // 🌟 자투리 세팅
        moneyBar.fillAmount = 0f;

        redText.text = $"{data.redPower:F0} GWh";
        blueText.text = $"{data.bluePower:F0} GWh";
        greenText.text = $"{data.greenPower:F0} GWh";
        scrapText.text = $"{data.scrapPower:F0} GWh"; // 🌟 자투리 세팅
        moneyText.text = "$0";

        yield return new WaitForSeconds(0.5f);

        // 2. 각 색상별 전력 정산
        yield return StartCoroutine(DrainPowerAndFillMoney(redPowerBar, redText, data.redPower, data.redMoney, data.totalMoneyCap));
        yield return StartCoroutine(DrainPowerAndFillMoney(bluePowerBar, blueText, data.bluePower, data.blueMoney, data.totalMoneyCap));
        yield return StartCoroutine(DrainPowerAndFillMoney(greenPowerBar, greenText, data.greenPower, data.greenMoney, data.totalMoneyCap));

        // 🌟 3. 마지막으로 자투리 전력 정산!
        yield return StartCoroutine(DrainPowerAndFillMoney(scrapPowerBar, scrapText, data.scrapPower, data.scrapMoney, data.totalMoneyCap));

        // 연출 끝!
        yield return new WaitForSeconds(1.5f);
        settlementPanel.SetActive(false);

        onAnimationComplete?.Invoke();
    }

    private IEnumerator DrainPowerAndFillMoney(Image powerBar, TextMeshProUGUI powerText, float startPower, float earnedMoney, float maxMoneyCap)
    {
        if (startPower <= 0) yield break; // 이 색상의 전력이 없으면 그냥 스킵!

        float t = 0;
        float startMoney = sharedMoneyTracker;
        float targetMoney = sharedMoneyTracker + earnedMoney;

        // 🌟 [핵심 변경점] 애니메이션을 시작하기 전, 현재 막대그래프의 높이(비율)를 기억해둡니다!
        float startFillAmount = powerBar.fillAmount;

        while (t < 1f)
        {
            t += Time.deltaTime * fillSpeed;

            // 1. 전력 텍스트는 숫자 그대로 부드럽게 줄어들고
            float currentPower = Mathf.Lerp(startPower, 0f, t);
            powerText.text = $"{currentPower:F0} GWh";

            // 🌟 2. 전력 막대그래프는 '기억해둔 원래 높이'에서 0으로 부드럽게 줄어듭니다! (100% 튀는 현상 해결)
            powerBar.fillAmount = Mathf.Lerp(startFillAmount, 0f, t);

            // 3. 돈 바는 차오름 (공유 변수에 현재 금액을 기록)
            sharedMoneyTracker = Mathf.Lerp(startMoney, targetMoney, t);
            moneyBar.fillAmount = sharedMoneyTracker / maxMoneyCap;
            moneyText.text = $"${sharedMoneyTracker:F0}";

            yield return null;
        }

        // 혹시 모를 소수점 오차 보정
        sharedMoneyTracker = targetMoney;
        powerBar.fillAmount = 0f;
        powerText.text = "0 GWh";
        moneyBar.fillAmount = sharedMoneyTracker / maxMoneyCap;
        moneyText.text = $"${sharedMoneyTracker:F0}";

        yield return new WaitForSeconds(0.3f); // 다음 색상으로 넘어가기 전 짧은 대기
    }
}