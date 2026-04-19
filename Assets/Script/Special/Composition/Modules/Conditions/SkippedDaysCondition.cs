using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// 누적 스킵 일수(ResourceManager.SkippedDaysTotal)를 scalar 로 출력. 항상 passed.
    /// "스킵한 만큼 보상 ×" 류 효과의 입력.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Skipped Days")]
    public class SkippedDaysCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int skipped = ResourceManager.Instance != null ? ResourceManager.Instance.SkippedDaysTotal : 0;
            return ConditionResult.Pass(ApplyCoefficient(skipped));
        }
    }
}
