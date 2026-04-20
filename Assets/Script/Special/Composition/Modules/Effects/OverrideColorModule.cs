using System.Collections.Generic;
using Special.Composition.Contexts;
using Special.Runtime;
using UnityEngine;

namespace Special.Composition.Modules.Effects
{
    /// <summary>
    /// 기획 효과 h/i) ColorOverrideContext.TargetCells/OverrideColorId 세팅.
    /// 선택 모드:
    ///  - Adjacent : owner 주변 1칸 셀 중 비어있지 않은(또는 모든) 셀을 color 로 오버라이드
    ///  - InScope  : CompositeEffectAsset 의 scope/range 가 가리키는 셀 전체
    ///  - ConditionTargets : condition.targets 를 그대로 사용 (예: AdjacentPowerPlantColorCondition 과 조합)
    ///  - OwnFootprint : owner 자신의 footprint 만 변경
    /// </summary>
    [CreateAssetMenu(menuName = "Special/Effects/Override Color")]
    public class OverrideColorModule : EffectModule
    {
        public enum TargetSelection { Adjacent, InScope, ConditionTargets, OwnFootprint }

        [Tooltip("1=Red, 2=Blue, 3=Yellow. 0 이하이면 동작하지 않음.")]
        [Range(0, 3)] public int overrideColorId = 2;
        public TargetSelection selection = TargetSelection.Adjacent;
        [Tooltip("true 면 이미 블럭이 있는 셀만 색 변경. false 면 빈 셀은 건너뜀.")]
        public bool onlyOccupied = true;

        public override EffectTriggerPhase Phase => EffectTriggerPhase.OnBlockPlacedColor;

        public override void Apply(SpecialBlockInstance owner, ConditionResult condition, IEffectContext ctx)
        {
            if (!(ctx is ColorOverrideContext co)) return;
            if (overrideColorId <= 0) return;

            co.OverrideColorId = overrideColorId;
            List<Vector2Int> targets = co.TargetCells ?? new List<Vector2Int>();

            GridManager grid = ScopeQueryService.Grid;

            switch (selection)
            {
                case TargetSelection.Adjacent:
                    foreach (Vector2Int cell in ScopeEvaluator.CellsInRange(owner, 1, grid != null ? grid.width : 0, grid != null ? grid.height : 0))
                    {
                        if (owner != null && owner.FootprintContains(cell)) continue;
                        if (onlyOccupied && (grid == null || grid.IsEmptyCell(cell))) continue;
                        targets.Add(cell);
                    }
                    break;

                case TargetSelection.InScope:
                    // CompositeEffectAsset 이 scope/range 정보를 모듈에 직접 넘기지 않으므로
                    // 대안으로 condition.targets 를 우선 사용하고, 없으면 owner footprint 만 사용.
                    if (condition.targets != null)
                    {
                        for (int i = 0; i < condition.targets.Count; i++) targets.Add(condition.targets[i]);
                    }
                    else if (owner != null && owner.footprint != null)
                    {
                        for (int i = 0; i < owner.footprint.Count; i++) targets.Add(owner.footprint[i]);
                    }
                    break;

                case TargetSelection.ConditionTargets:
                    if (condition.targets != null)
                    {
                        for (int i = 0; i < condition.targets.Count; i++) targets.Add(condition.targets[i]);
                    }
                    break;

                case TargetSelection.OwnFootprint:
                    if (owner != null && owner.footprint != null)
                    {
                        for (int i = 0; i < owner.footprint.Count; i++) targets.Add(owner.footprint[i]);
                    }
                    break;
            }

            co.TargetCells = targets;
        }

        public override string BuildPreviewLine(SpecialBlockInstance owner, ConditionResult condition)
        {
            if (!condition.passed) return "색상 변경 <color=#888888>효과 미발동</color>";
            string colorLabel = overrideColorId switch
            {
                1 => "빨강",
                2 => "파랑",
                3 => "노랑",
                _ => "없음"
            };
            string selectionLabel = selection switch
            {
                TargetSelection.Adjacent => "인접",
                TargetSelection.InScope => "범위 내",
                TargetSelection.ConditionTargets => "조건 타겟",
                TargetSelection.OwnFootprint => "자신",
                _ => selection.ToString()
            };
            return $"{selectionLabel} 블럭 → <color=#FF99CC>{colorLabel}</color> 으로 변경";
        }
    }
}
