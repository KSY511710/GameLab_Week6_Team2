using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition
{
    /// <summary>
    /// 조합형 특수블럭의 "작동 조건/계수" 한 단위.
    /// CompositeEffectAsset 이 이 모듈 리스트를 AND 결합하여 효과 적용 여부와 강도를 결정한다.
    ///
    /// 신규 조건 추가 시: 본 클래스를 상속, Evaluate 만 구현, [CreateAssetMenu] 부여.
    /// 기존 코드는 한 줄도 수정할 필요가 없다.
    /// </summary>
    public abstract class ConditionModule : ScriptableObject
    {
        [Header("Coefficient")]
        [Tooltip("Evaluate 가 산출한 raw scalar 에 곱해지는 계수.")]
        [SerializeField] protected float multiplier = 1f;
        [Tooltip("Evaluate 가 산출한 raw scalar 를 나누는 계수. 0 이면 항상 0 출력.")]
        [SerializeField] protected float divisor = 1f;

        public abstract ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range);

        /// <summary>
        /// 시퀀서/정보 패널이 조건 한 줄을 표시할 때 호출. 기본 포맷은 "<이름> (✓/✗, scalar=…)".
        /// </summary>
        public virtual string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult result)
        {
            string label = string.IsNullOrEmpty(name) ? GetType().Name : name;
            if (!result.passed) return $"{label} <color=#FF6666>✗</color>";
            // scalar 가 1 이면 단순 통과, 아니면 계수로 덧붙여 표시.
            if (Mathf.Approximately(result.scalar, 1f)) return $"{label} <color=#66DD99>✓</color>";
            return $"{label} <color=#66DD99>✓</color> <color=#AADDFF>(×{FormatScalar(result.scalar)})</color>";
        }

        /// <summary>multiplier / divisor 적용. divisor==0 은 0 으로 안전 처리.</summary>
        protected float ApplyCoefficient(float raw)
            => divisor == 0f ? 0f : raw * multiplier / divisor;

        private static string FormatScalar(float v)
        {
            if (v == (int)v) return ((int)v).ToString();
            return v.ToString("0.##");
        }
    }
}
