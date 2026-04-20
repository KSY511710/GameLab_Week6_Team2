using System.Collections.Generic;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope 내 finalColor==targetColorId 발전소 비율이 threshold 이상이면 passed.
    /// targets = 해당 색 발전소 셀 합집합. scalar = 1 (효과 강도가 아닌 게이트).
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Color Dominance")]
    public class ColorDominanceCondition : ConditionModule
    {
        [Tooltip("기준 색상: 1=Red, 2=Blue, 3=Yellow")]
        [Range(1, 3)] public int targetColorId = 1;
        [Range(0f, 1f)] public float threshold = 0.5f;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            float ratio = ScopeQueryService.QueryDominantColorRatio(owner, scope, range, targetColorId);
            if (ratio < threshold) return ConditionResult.Fail();

            List<GroupInfo> hits = ScopeQueryService.QueryPowerPlantsOfColor(owner, scope, range, targetColorId);
            List<Vector2Int> targets = new List<Vector2Int>();
            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].clusterPositions != null) targets.AddRange(hits[i].clusterPositions);
            }
            return ConditionResult.PassWithTargets(ApplyCoefficient(1f), targets);
        }
    }
}
