using TMPro;
using UnityEngine;

/// <summary>
/// 상점 뽑기 버튼 옆에 붙이는 가격 라벨.
/// ResourceManager.OnDrawCostChanged 와 OnSessionAdvanced 를 구독하여 실시간으로 갱신한다.
/// 폴링(Update) 사용 금지.
/// </summary>
public class DrawPriceLabel : MonoBehaviour
{
    [SerializeField] private DrawKind kind = DrawKind.Basic;
    [SerializeField] private TextMeshProUGUI label;
    [Tooltip("표시 포맷. {0}이 비용 숫자로 치환된다.")]
    [SerializeField] private string format = "$ {0}";

    private int lastDisplayedCost = int.MinValue;

    private void Reset()
    {
        label = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        ResourceManager.OnDrawCostChanged += Refresh;
        ResourceManager.OnSessionAdvanced += HandleSessionAdvanced;
        Refresh();
    }

    private void OnDisable()
    {
        ResourceManager.OnDrawCostChanged -= Refresh;
        ResourceManager.OnSessionAdvanced -= HandleSessionAdvanced;
    }

    private void HandleSessionAdvanced(int _) => Refresh();

    private void Refresh()
    {
        if (label == null) return;
        if (ResourceManager.Instance == null) return;

        int cost = ResourceManager.Instance.GetDrawCost(kind);
        if (cost == lastDisplayedCost) return;

        lastDisplayedCost = cost;
        label.text = string.Format(format, cost);
    }
}
