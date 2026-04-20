using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// OnProductionSettle 훅(일일 정산 직전) 에서 PowerManager.SubmitSpecialContribution 호출.
    /// 비-PowerPlant 블럭이 "내가 오늘 이만큼 생산했다" 를 SettlementUI 의 색상 막대에 반영시키기 위한 효과.
    /// 기여 전력 = condition.scalar * powerPerScalar.
    /// 라이브 파워 표시는 PowerManager.CalculateTotalPower 가 비-PowerPlant 인스턴스의
    /// CompositeEffectAsset.EstimateLiveContributionPower(OnProductionSettle 모듈 한정) 를 합산해 반영.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Submit Daily Contribution")]
    public class SubmitDailyContributionModule : EffectModule
    {
        [Tooltip("condition.scalar 1 단위당 제출할 전력량.")]
        [Min(0f)] public float powerPerScalar = 1f;
        [Tooltip("Settlement UI 에 노출되는 태그(디버그/툴팁용). 비워두면 에셋 이름 사용.")]
        public string sourceTag = "";

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnProductionSettle;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (owner == null || PowerManager.Instance == null) return;
            float power = condition.scalar * powerPerScalar;
            if (power <= 0f) return;
            string tag = string.IsNullOrEmpty(sourceTag) ? name : sourceTag;
            PowerManager.Instance.SubmitSpecialContribution(owner, power, tag);
        }

        /// <summary>
        /// PowerPlant 솔로 그룹 경로(CompositeEffectAsset.EstimateLivePower) 와
        /// 비-PowerPlant 라이브 합산 경로(CompositeEffectAsset.EstimateLiveContributionPower) 양쪽에 동일 값을 돌려준다.
        /// </summary>
        public override float EstimateLivePower(SpecialBlockInstance owner, ConditionResult condition)
            => condition.scalar * powerPerScalar;

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "일일 기여 <color=#888888>효과 미발동</color>";
            float power = condition.scalar * powerPerScalar;
            return $"일일 기여 <color=#FFE066>+{power:0.##} GWh</color>";
        }
    }
}
