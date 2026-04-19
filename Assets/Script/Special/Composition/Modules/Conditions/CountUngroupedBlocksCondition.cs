using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope 내 미그룹 블럭(colorID&gt;0 &amp;&amp; !isGrouped) 개수를 scalar 로 출력. 항상 passed.
    /// "놀고 있는 블럭이 많을수록 강해짐" 류 효과의 입력.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Count Ungrouped Blocks")]
    public class CountUngroupedBlocksCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            int count = ScopeQueryService.QueryUngroupedBlocks(owner, scope, range).Count;
            return ConditionResult.Pass(ApplyCoefficient(count));
        }
    }
}
