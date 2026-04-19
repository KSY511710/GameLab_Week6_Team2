using System.Collections.Generic;
using Special.Runtime;
using UnityEngine;

namespace Special.Effects.Assets
{
    /// <summary>
    /// 예시 b) 범위 내 빈칸 수만큼 자기 자신이 전력 생산.
    /// PowerPlant role 블럭이 쓰는 표준 시나리오 — 일반 블럭과 BFS 로 섞이지 않고
    /// 자기 footprint 로만 이루어진 솔로 그룹의 생산량을 본 효과가 매 프레임 산출한다.
    /// EstimateLivePower 가 반환하는 값이 그대로 활성 그룹의 groupPower 로 쓰이며,
    /// PowerManager.GetTotalPower 에 합산되어 PowerText 실시간 표시와 SettlementUIController
    /// 색상 막대 양쪽에 즉시 반영된다. 별도의 HookProductionSettle / SubmitSpecialContribution 경로는 필요 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Produce From Empty Cells")]
    public class ProduceFromEmptyCellsEffect : EffectAsset
    {
        [Min(0)] public int perEmptyCell = 1;

        public override void Activate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            /* PowerPlant 솔로 그룹 경로로 집계되므로 런타임 훅 등록은 불필요. */
        }

        public override void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime)
        {
            /* EffectRuntime.UnhookAll(owner)가 SpecialBlockRegistry.DeactivateEffects에서 호출되며 정리됨. */
        }

        public override float EstimateLivePower(SpecialBlockInstance owner)
        {
            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null || owner == null) return 0f;
            var (emptyCount, _) = CollectEmpty(owner, grid);
            return emptyCount * perEmptyCell;
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
        /// EstimateLivePower / BuildPreview 양쪽에서 공유.
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
