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

                var (emptyCount, _) = CollectEmpty(owner, grid);

                int bonus = emptyCount * perEmptyCell;
                if (bonus > 0)
                {
                    ResourceManager.Instance.AddElectric(bonus);
                    Debug.Log($"[Special:{owner?.definition?.id}] 빈칸 {emptyCount}칸 → +{bonus} 전력");
                }
            });
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            /* EffectRuntime.UnhookAll(owner)가 SpecialBlockRegistry.DeactivateEffects에서 호출되며 정리됨. */
        }

        public override EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = base.BuildPreview(owner);

            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            var (emptyCount, cells) = grid != null ? CollectEmpty(owner, grid) : (0, new List<Vector2Int>());
            int bonus = emptyCount * perEmptyCell;

            preview.steps.Add($"<size=20>· 범위 내 빈칸 : <color=#A0E0FF>{emptyCount} 칸</color></size>");
            preview.steps.Add($"<size=20>· 일일 보너스 : {emptyCount} × {perEmptyCell} = <color=#FFE066>+{bonus}</color> 전력</size>");

            preview.impactCells = cells;
            return preview;
        }

        /// <summary>
        /// scope 범위 안에서 owner footprint 를 제외한 빈칸을 한 번의 순회로 수집.
        /// 훅/프리뷰 양쪽에서 공유.
        /// </summary>
        private (int count, List<Vector2Int> cells) CollectEmpty(SpecialBlockInstance owner, GridManager grid)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            if (owner == null || grid == null) return (0, cells);

            foreach (Vector2Int cell in ScopeEvaluator.CellsInRange(owner, rangeInCells, grid.width, grid.height))
            {
                if (owner.FootprintContains(cell)) continue;
                if (!grid.IsEmptyCell(cell)) continue;
                cells.Add(cell);
            }
            return (cells.Count, cells);
        }
    }
}
