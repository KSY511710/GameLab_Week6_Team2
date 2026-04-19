using System.Collections.Generic;
using UnityEngine;

namespace Special.Composition
{
    /// <summary>
    /// ConditionModule.Evaluate 의 반환값.
    /// scalar : 효과 강도 계수 (예: 발전소 개수, 일차 수치)
    /// targets: 효과가 한정해서 적용될 셀 집합. null 이면 scope 내 전체 대상.
    /// passed : 게이팅 조건. false 이면 조합된 효과가 적용되지 않는다.
    /// </summary>
    public struct ConditionResult
    {
        public float scalar;
        public IReadOnlyList<Vector2Int> targets;
        public bool passed;

        public static ConditionResult Pass(float s) => new ConditionResult { scalar = s, passed = true };
        public static ConditionResult Fail() => new ConditionResult { scalar = 0f, passed = false };
        public static ConditionResult PassWithTargets(float s, IReadOnlyList<Vector2Int> t)
            => new ConditionResult { scalar = s, passed = true, targets = t };
    }
}
