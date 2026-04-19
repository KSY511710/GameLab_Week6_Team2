using System.Collections.Generic;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 그룹 한 개의 전력 계산에 효과들이 끼어들어 누적 가능한 변형 컨텍스트.
    /// PowerManager.CreateNewGroup 이 raw 값을 채우고, EffectRuntime 이 훅을 적용한 뒤
    /// ApplyToFinalPower 로 최종값을 산출한다.
    /// </summary>
    public class PowerCalculationContext
    {
        public int BaseProductionRaw;
        public int UniquePartsRaw;
        public int CompletionMultiplierRaw;
        public float ColorMultiplierRaw;

        public int BaseProductionAdd;
        public int UniquePartsAdd;
        public int CompletionMultiplierAdd;
        public float ColorMultiplierMul = 1f;
        public float FinalMultiplier = 1f;

        public IReadOnlyList<Vector2Int> ClusterPositions;

        public float Compute()
        {
            int baseAll = BaseProductionRaw + BaseProductionAdd;
            int partsAll = UniquePartsRaw + UniquePartsAdd;
            int completion = Mathf.Max(0, CompletionMultiplierRaw + CompletionMultiplierAdd);
            float color = ColorMultiplierRaw * ColorMultiplierMul;
            return (baseAll + partsAll) * completion * color * FinalMultiplier;
        }
    }
}
