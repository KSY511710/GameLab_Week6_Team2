using Special.Composition.Contexts;
using Special.Data;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// ExchangeRatioContext 에 색상 필터로 Multiplier / Offset 을 누적하는 효과.
    /// "빨강 그룹의 환전 비율 x0.5", "파랑 그룹에 +3 가산" 같은 기획이 이 한 모듈로 커버된다.
    ///
    /// 합성 규칙: 최종 ratio = BaseRatio * Multiplier(누적곱) + Offset(누적합). 여러 효과가 동시에 적용되어도
    /// 곱/합이 각각 교환법칙을 만족하므로 등록 순서에 무관하게 같은 값이 나온다.
    ///
    /// ColorSet 플래그로 복수 색상을 한 번에 타겟팅할 수 있고, includeScrap 으로 색상 없는 그룹
    /// (OffPalette PowerPlant 솔로 등, finalColor=0) 도 별도로 포함 가능.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Modify Exchange Ratio By Color")]
    public class ModifyExchangeRatioByColorModule : EffectModule
    {
        [Header("대상")]
        [Tooltip("환율을 수정할 대상 색상. 복수 선택 가능. None 이면 어떤 유색 그룹에도 적용되지 않음.")]
        public ColorSet targetColors = ColorSet.All;

        [Tooltip("색상 없는 그룹(자투리/OffPalette PowerPlant, finalColor=0) 에도 적용할지 여부.")]
        public bool includeScrap = false;

        [Header("효과량")]
        [Tooltip("환율 배수. 1 = 변화 없음, 0.5 = 절반(유리), 2 = 두 배(불리). 0 이하 입력 금지.")]
        [Min(0f)] public float multiplier = 1f;

        [Tooltip("환율 가산치. +값은 환율 상승(불리), -값은 환율 하락(유리). 단위는 GWh per $ (BaseRatio 와 동일).")]
        public float offset = 0f;

        [Tooltip("true 면 multiplier 를 condition.scalar 만큼 누승(Mathf.Pow). false 면 단순 곱셈. offset 은 scalar 선형 가중.")]
        public bool useScalarAsExponent = false;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnExchangeRatio;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (ctx is not ExchangeRatioContext ex) return;
            if (ex.Group == null) return;
            if (!MatchesTargetColor(ex.Group.finalColor)) return;

            float scalar = Mathf.Max(0f, condition.scalar);
            float m = useScalarAsExponent ? Mathf.Pow(multiplier, scalar) : multiplier;
            ex.Multiplier *= m;
            ex.Offset += offset * scalar;
        }

        private bool MatchesTargetColor(int colorID)
        {
            switch (colorID)
            {
                case 1: return (targetColors & ColorSet.Red) != 0;
                case 2: return (targetColors & ColorSet.Blue) != 0;
                case 3: return (targetColors & ColorSet.Yellow) != 0;
                default: return includeScrap;
            }
        }
    }
}
