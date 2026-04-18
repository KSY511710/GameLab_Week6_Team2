using Special.Runtime;
using UnityEngine;

namespace Special.Effects.Assets
{
    /// <summary>
    /// 예시 b) 범위 내 빈칸 수만큼 자기 자신이 전력 생산.
    /// Independent 블럭이 쓰는 시나리오 — 그룹에 참여하지 않고 매일 정산 시 고정 보너스.
    /// DailySettle 훅에서 ResourceManager 에 전력을 직접 지급한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Produce From Empty Cells")]
    public class ProduceFromEmptyCellsEffect : EffectAsset
    {
        [Min(0)] public int perEmptyCell = 1;

        public override void Activate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            runtime.HookDailySettle(owner, () =>
            {
                GridManager grid = Object.FindFirstObjectByType<GridManager>();
                if (grid == null || ResourceManager.Instance == null) return;

                int emptyCount = 0;
                foreach (Vector2Int cell in ScopeEvaluator.CellsInRange(owner, rangeInCells, grid.width, grid.height))
                {
                    if (owner.FootprintContains(cell)) continue;
                    if (grid.IsEmptyCell(cell)) emptyCount++;
                }

                int bonus = emptyCount * perEmptyCell;
                if (bonus > 0)
                {
                    ResourceManager.Instance.AddElectric(bonus);
                    Debug.Log($"[Special:{owner.definition.id}] 빈칸 {emptyCount}칸 → +{bonus} 전력");
                }
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime) { }
    }
}
