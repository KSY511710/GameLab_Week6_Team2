using System.Collections.Generic;
using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope 내 finalColor==Red(1) 발전소 비율이 threshold 이상이면 passed.
    /// targets = 빨강 발전소들의 셀 합집합. scalar = 1 (효과 강도가 아닌 게이트).
    /// 레거시 BoostExchangeRatioByColorEffect 의 색상 우세 게이트 대응.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Red Dominance")]
    public class RedDominanceCondition : ConditionModule
    {
        [Range(0f, 1f)] public float threshold = 0.5f;

        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            float ratio = ScopeQueryService.QueryDominantColorRatio(owner, scope, range, 1);
            if (ratio < threshold) return ConditionResult.Fail();

            List<GroupInfo> reds = ScopeQueryService.QueryPowerPlantsOfColor(owner, scope, range, 1);
            List<Vector2Int> targets = new List<Vector2Int>();
            for (int i = 0; i < reds.Count; i++)
            {
                if (reds[i].clusterPositions != null) targets.AddRange(reds[i].clusterPositions);
            }
            return ConditionResult.PassWithTargets(ApplyCoefficient(1f), targets);
        }
    }
}
