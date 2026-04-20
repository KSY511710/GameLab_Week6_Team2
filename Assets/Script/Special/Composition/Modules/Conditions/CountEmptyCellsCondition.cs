using Special.Effects;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Conditions
{
    /// <summary>
    /// scope/range 내 빈칸 셀 개수를 scalar 로 출력. owner 자기 footprint 는 자동 제외.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Conditions/Count Empty Cells")]
    public class CountEmptyCellsCondition : ConditionModule
    {
        public override ConditionResult Evaluate(SpecialBlockInstance owner, EffectScope scope, int range)
        {
            var cells = ScopeQueryService.QueryEmptyCells(owner, scope, range);
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (owner != null && owner.FootprintContains(cells[i])) continue;
                count++;
            }
            return ConditionResult.Pass(ApplyCoefficient(count));
        }
    }
}
