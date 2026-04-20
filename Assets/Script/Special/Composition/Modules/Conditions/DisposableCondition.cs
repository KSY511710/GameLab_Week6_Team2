using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// 일회성(Disposable). 설치된 당일(ResourceManager.TotalDay == owner.placementDay) 에만 passed.
    /// 다음 날부터는 Fail 을 반환하여 같은 Composite 의 모든 Effect 가 발동되지 않는다.
    /// 블럭 자체는 보드에 남아 SpecialBlockDefinition 의 속성(모양/색/그룹 기여 등)만 계속 작동.
    /// 블럭의 모든 효과를 일회성으로 만들고 싶다면 해당 블럭이 가진 모든 Composite 에 본 조건을 추가한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Disposable")]
    public class DisposableCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            if (owner == null || ResourceManager.Instance == null) return ConditionResult.Fail();
            return ResourceManager.Instance.TotalDay == owner.placementDay
                ? ConditionResult.Pass(ApplyCoefficient(1f))
                : ConditionResult.Fail();
        }
    }
}
