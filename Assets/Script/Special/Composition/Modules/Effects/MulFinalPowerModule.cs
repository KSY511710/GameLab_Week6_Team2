using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 l) PowerCalculationContext.FinalMultiplier 에 multiplier 곱셈.
    /// 조건 게이트(passed)는 CompositeEffectAsset 이 이미 통과시킨 상태에서만 호출됨.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Mul Final Power")]
    public class MulFinalPowerModule : EffectModule
    {
        [Min(0f)] public float multiplier = 1.5f;
        [Tooltip("true 면 condition.scalar 도 추가 가중. 최종 = base * multiplier^scalar 와 유사한 누승 표현.")]
        public bool useScalarAsExponent = false;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is PowerCalculationContext power)
            {
                float m = useScalarAsExponent ? Mathf.Pow(multiplier, condition.scalar) : multiplier;
                power.FinalMultiplier *= m;
            }
        }
    }
}
