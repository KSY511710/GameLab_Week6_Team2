using Special.Runtime;
using UnityEngine;

namespace Special.Composition
{
    /// <summary>
    /// 조합형 특수블럭의 "효과(값 수정자)" 한 단위.
    /// Phase 가 가리키는 훅에서 ConditionResult 와 IEffectContext 를 받아 부작용을 적용한다.
    ///
    /// 신규 효과 추가 시: 본 클래스를 상속, Phase/Apply 만 구현, [CreateAssetMenu] 부여.
    /// 기존 코드는 한 줄도 수정할 필요가 없다.
    /// </summary>
    public abstract class EffectModule : ScriptableObject
    {
        public abstract EffectTriggerPhase Phase { get; }

        /// <summary>
        /// condition.passed == false 인 경우 CompositeEffectAsset 이 호출 자체를 막으므로
        /// 여기서 다시 판정할 필요는 없다. condition.scalar / condition.targets 를 사용.
        /// </summary>
        public abstract void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx);

        /// <summary>
        /// PowerPlant role 이 "이 모듈이 현재 얼마나 생산 중인지" 를 묻는 라이브 질의.
        /// CompositeEffectAsset.EstimateLivePower 에서 모든 모듈에 누적 호출한다.
        /// 일반(Grouping) 효과는 0 반환이면 충분.
        /// </summary>
        public virtual float EstimateLivePower(SpecialBlockInstance owner, ConditionResult condition) => 0f;

        /// <summary>
        /// 시퀀서/정보 패널이 효과 한 줄을 표시할 때 호출. 기본은 에셋 이름만 돌려주지만,
        /// 각 효과 모듈이 오버라이드해 "실제 수치" (예: +N, ×M.MM) 를 넣어주면 된다.
        /// condition.passed == false 일 때도 호출될 수 있으므로 구현체는 그 경우 0 처리한다.
        /// </summary>
        public virtual string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            return string.IsNullOrEmpty(name) ? GetType().Name : name;
        }

        /// <summary>Trace 기록용 소스 이름. owner/definition 가 null 안전.</summary>
        protected static string SourceName(SpecialBlockInstance owner)
        {
            if (owner == null) return null;
            var def = owner.definition;
            if (def == null) return null;
            return def.displayName;
        }
    }
}
