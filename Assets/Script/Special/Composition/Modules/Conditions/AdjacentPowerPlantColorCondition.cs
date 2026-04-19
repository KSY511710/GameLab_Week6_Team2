using System.Collections.Generic;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// owner 에 인접한 발전소 중 finalColor==targetColorId 인 것이 하나라도 있으면 passed.
    /// targets = 인접한 대상 색 발전소들의 셀 합집합. scope/range 는 무시하고 AdjacentPowerPlant 규칙 사용.
    /// 예: "맞닿은 빨강 발전소 대상으로 생산횟수 +1".
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Adjacent Power Plant Color")]
    public class AdjacentPowerPlantColorCondition : ConditionModule
    {
        [Tooltip("대상 색상: 1=Red, 2=Blue, 3=Yellow, 0=모든 색")]
        [Range(0, 3)] public int targetColorId = 0;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            List<GroupInfo> adjacent = ScopeQueryService.QueryPowerPlants(owner, EffectScope.AdjacentPowerPlant, 1);
            List<Vector2Int> targets = new List<Vector2Int>();
            int hitCount = 0;
            for (int i = 0; i < adjacent.Count; i++)
            {
                if (targetColorId != 0 && adjacent[i].finalColor != targetColorId) continue;
                hitCount++;
                if (adjacent[i].clusterPositions != null) targets.AddRange(adjacent[i].clusterPositions);
            }
            if (hitCount == 0) return ConditionResult.Fail();
            return ConditionResult.PassWithTargets(ApplyCoefficient(hitCount), targets);
        }
    }
}
