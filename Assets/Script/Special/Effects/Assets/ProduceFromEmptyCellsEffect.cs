using System.Collections.Generic;
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

                int emptyCount = CountEmptyCells(owner, grid);

                int bonus = emptyCount * perEmptyCell;
                if (bonus > 0)
                {
                    ResourceManager.Instance.AddElectric(bonus);
                    Debug.Log($"[Special:{owner.definition.id}] 빈칸 {emptyCount}칸 → +{bonus} 전력");
                }
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime) { }

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);

            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            int emptyCount = grid != null ? CountEmptyCells(owner, grid) : 0;
            int bonus = emptyCount * perEmptyCell;

            preview.steps.Add($"<size=20>· 범위 내 빈칸 : <color=#A0E0FF>{emptyCount} 칸</color></size>");
            preview.steps.Add($"<size=20>· 일일 보너스 : {emptyCount} × {perEmptyCell} = <color=#FFE066>+{bonus}</color> 전력</size>");

            preview.impactCells = CollectEmptyCells(owner, grid);
            return preview;
        }

        private int CountEmptyCells(SpecialBlockInstance owner, GridManager grid)
        {
            int count = 0;
            foreach (Vector2Int cell in ScopeEvaluator.CellsInRange(owner, rangeInCells, grid.width, grid.height))
            {
                if (owner.FootprintContains(cell)) continue;
                if (grid.IsEmptyCell(cell)) count++;
            }
            return count;
        }

        private List<Vector2Int> CollectEmptyCells(SpecialBlockInstance owner, GridManager grid)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (grid == null) return cells;
            foreach (Vector2Int cell in ScopeEvaluator.CellsInRange(owner, rangeInCells, grid.width, grid.height))
            {
                if (owner.FootprintContains(cell)) continue;
                if (grid.IsEmptyCell(cell)) cells.Add(cell);
            }
            return cells;
        }
    }
}
