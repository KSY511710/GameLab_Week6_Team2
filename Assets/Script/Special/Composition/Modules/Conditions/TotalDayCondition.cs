using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// 경과 총 일수(ResourceManager.TotalDay)를 scalar 로 출력. 항상 passed.
    /// "전체 일차만큼 최종 전력 ×" 같은 누적 시간형 효과의 입력.
    /// multiplier=0.01, divisor=1 이면 "1% 씩 일당 가중" 표현.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Total Day")]
    public class TotalDayCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int totalDay = ResourceManager.Instance != null ? ResourceManager.Instance.TotalDay : 0;
            return ConditionResult.Pass(ApplyCoefficient(totalDay));
        }
    }
}
