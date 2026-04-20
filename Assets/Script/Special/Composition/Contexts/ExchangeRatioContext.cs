using UnityEngine;

namespace Special.Composition.Contexts
{
    /// <summary>
    /// PowerManager.RecalculateAllGroupPowers 가 그룹별 환전 비율(GWh 당 $ 환산 계수)을 확정할 때
    /// 훅으로 발화. 여러 효과가 동시에 Multiplier/Offset 누적에 참여해도 결과가 순서 불변이도록
    /// 합성식은 BaseRatio * Multiplier + Offset 으로 고정한다.
    ///
    /// - Multiplier 는 곱셈 누적 (기본 1f). 여러 효과가 0.5, 0.8 을 기여하면 0.4 로 합성.
    /// - Offset 은 덧셈 누적 (기본 0f). 여러 효과의 +/-값이 자연스럽게 더해진다.
    /// - 최종 결과는 Compute() 에서 하한(0.01) 클램핑만 적용 — 0 또는 음수 환율은 AutoExchange 에서 NaN/무한 유발.
    ///
    /// GroupInfo.finalColor 로 색상 매칭이 가능하며, 솔로 그룹도 동일 규칙(색 없으면 0=자투리).
    /// </summary>
    public class ExchangeRatioContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnExchangeRatio;

        /// <summary>대상 그룹. EffectModule 이 Group.finalColor / clusterPositions 등을 조회해 조건부 적용.</summary>
        public GroupInfo Group;

        /// <summary>ResourceManager.ExchangeRatio 기준. 훅 적용 전 원본 값.</summary>
        public float BaseRatio = 1f;

        /// <summary>곱셈 누적. 1 = 변화 없음.</summary>
        public float Multiplier = 1f;

        /// <summary>덧셈 누적. 0 = 변화 없음. 단위는 GWh per $ (BaseRatio 와 동일).</summary>
        public float Offset = 0f;

        /// <summary>최종 적용될 환율. 하한 0.01 클램프.</summary>
        public float Compute() => Mathf.Max(0.01f, BaseRatio * Multiplier + Offset);
    }
}
