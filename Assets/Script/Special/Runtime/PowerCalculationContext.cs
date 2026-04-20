using System.Collections.Generic;
using Special.Composition;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 그룹 한 개의 전력 계산에 효과들이 끼어들어 누적 가능한 변형 컨텍스트.
    /// PowerManager.CreateNewGroup 이 raw 값을 채우고, EffectRuntime 이 훅을 적용한 뒤
    /// Compute() 로 최종값을 산출한다.
    ///
    /// 기획 효과 c(기본 완성)와 d(모양 완성)를 독립적으로 +/× 조작할 수 있도록
    /// 기존 단일 CompletionMultiplier 를 BaseCompletion + ShapeCompletion 로 분리.
    /// 호환성: 모듈이 아무 것도 수정하지 않으면 합산값은 기존(2 + shapeBonus)과 정확히 동일.
    /// </summary>
    public class PowerCalculationContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnPowerCalculation;

        public int BaseProductionRaw;
        public int UniquePartsRaw;
        public float ColorMultiplierRaw;

        // 효과 c) 기본 완성 (= 기존 상수 2)
        public int BaseCompletionRaw;
        public int BaseCompletionAdd;
        public float BaseCompletionMul = 1f;

        // 효과 d) 모양 완성 (= 기존 shapeBonus)
        public int ShapeCompletionRaw;
        public int ShapeCompletionAdd;
        public float ShapeCompletionMul = 1f;

        public int BaseProductionAdd;
        public int UniquePartsAdd;
        public float ColorMultiplierMul = 1f;
        public float FinalMultiplier = 1f;

        public IReadOnlyList<Vector2Int> ClusterPositions;

        /// <summary>
        /// null 이 아니면 Compute() 호출 시 최종 단계의 결과 요약을 기록한다.
        /// 효과 모듈들은 Apply 내에서 RecordAdd/RecordMul 을 직접 호출해 자신의 기여를 남긴다.
        /// 라이브 재계산 경로(CalculateTotalPower)는 null 로 두어 기존 동작과 동일하게 유지한다.
        /// </summary>
        public CalculationTrace Trace;

        public float Compute()
        {
            int baseAll = BaseProductionRaw + BaseProductionAdd;
            int partsAll = UniquePartsRaw + UniquePartsAdd;
            int baseC = Mathf.Max(0, Mathf.RoundToInt((BaseCompletionRaw + BaseCompletionAdd) * BaseCompletionMul));
            int shapeC = Mathf.Max(0, Mathf.RoundToInt((ShapeCompletionRaw + ShapeCompletionAdd) * ShapeCompletionMul));
            int completion = baseC + shapeC;
            float color = ColorMultiplierRaw * ColorMultiplierMul;
            float final = (baseAll + partsAll) * completion * color * FinalMultiplier;
            if (Trace != null) Trace.RecordFinal("최종 발전량", final);
            return final;
        }

        /// <summary>GroupInfo.completionMultiplier / debugMsg 용 합산 캐시값.</summary>
        public int ComputedCompletionMultiplier
        {
            get
            {
                int baseC = Mathf.Max(0, Mathf.RoundToInt((BaseCompletionRaw + BaseCompletionAdd) * BaseCompletionMul));
                int shapeC = Mathf.Max(0, Mathf.RoundToInt((ShapeCompletionRaw + ShapeCompletionAdd) * ShapeCompletionMul));
                return baseC + shapeC;
            }
        }
    }
}
