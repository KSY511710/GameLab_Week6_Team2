using System.Collections.Generic;
using Special.Runtime;
using UnityEngine;

namespace Special.Effects
{
    public abstract class EffectAsset : ScriptableObject
    {
        [SerializeField] protected EffectScope scope = EffectScope.Global;
        [Tooltip("Range scope 에서만 사용. 자기 footprint 에서 뻗는 맨해튼 거리.")]
        [SerializeField, Min(0)] protected int rangeInCells = 3;

        [Header("Visual (시퀀서 표시용)")]
        [Tooltip("시퀀서 패널 헤더에 표시될 효과 이름. 비워두면 클래스 이름이 쓰인다.")]
        [SerializeField] protected string displayName;
        [Tooltip("기본 단계 설명. 효과별 BuildPreview 가 이 줄을 첫 단계로 사용한다.")]
        [SerializeField, TextArea(1, 4)] protected string description;
        [Tooltip("영향 범위 오버레이 색상 (알파 포함).")]
        [SerializeField] protected Color overlayColor = new Color(1f, 0.85f, 0.2f, 0.32f);

        public EffectScope Scope => scope;
        public int RangeInCells => rangeInCells;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? GetType().Name : displayName;
        public string Description => description;
        public Color OverlayColor => overlayColor;

        public abstract void Activate(SpecialBlockInstance owner, EffectRuntime runtime);
        public abstract void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime);

        /// <summary>
        /// PowerPlant role 의 솔로 그룹이 "현재 이 효과가 지금 당장 얼마나 생산 중인지" 를 질의할 때 사용.
        /// Grouping 계열 그룹에는 영향을 주지 않으며(그쪽은 PowerCalculationContext 훅으로 조율), 기본값은 0.
        /// PowerPlant 에서 쓰이는 효과만 오버라이드해 실제 생산량을 반환한다.
        /// 반환값은 PowerManager.CalculateTotalPower / RecalculateAllGroupPowers 흐름에서 매 프레임 재질의되므로
        /// 보드 상태에 따라 자연스럽게 PowerText 실시간 합계에도 반영된다.
        /// </summary>
        public virtual float EstimateLivePower(SpecialBlockInstance owner) => 0f;

        /// <summary>
        /// 시퀀서가 호출. 효과별 계산 결과를 사람이 읽을 수 있게 풀어 EffectPreview 에 담아 반환한다.
        /// 기본 구현은 description 한 줄과 scope 기반의 영역 셀만 채워준다.
        /// 하위 클래스는 실제 수치(범위 내 발전소 수, 빈칸 수 등)를 계산해 steps 에 추가하는 것을 권장.
        /// </summary>
        public virtual EffectPreview BuildPreview(SpecialBlockInstance owner)
        {
            EffectPreview preview = new EffectPreview
            {
                title = DisplayName,
                overlayColor = overlayColor,
                scopeCells = ResolveDefaultScopeCells(owner)
            };
            if (!string.IsNullOrEmpty(description)) preview.steps.Add(description);
            return preview;
        }

        /// <summary>
        /// 드래그 중 프리뷰용으로 형태적 영향 범위 셀만 얇게 계산해 반환.
        /// BuildPreview 는 PowerManager.activeGroups 조회 등 무거운 작업을 포함하므로 드래그 경로에서는 쓰지 않는다.
        /// </summary>
        public List<Vector2Int> BuildDragPreviewCells(SpecialBlockInstance previewOwner)
            => ResolveDefaultScopeCells(previewOwner);

        /// <summary>
        /// scope/rangeInCells 만으로 산출되는 기본 영역 셀.
        /// Global / Zone 처럼 "전체"에 해당하는 경우엔 빈 리스트(=텍스트만 표시)를 돌려준다.
        /// </summary>
        protected List<Vector2Int> ResolveDefaultScopeCells(SpecialBlockInstance owner)
        {
            List<Vector2Int> list = new List<Vector2Int>();
            if (owner == null) return list;
            GridManager grid = Object.FindFirstObjectByType<GridManager>();
            if (grid == null) return list;

            switch (scope)
            {
                case EffectScope.Range:
                    foreach (Vector2Int c in ScopeEvaluator.CellsInRange(owner, rangeInCells, grid.width, grid.height))
                        list.Add(c);
                    break;
                case EffectScope.AdjacentPowerPlant:
                    foreach (Vector2Int c in ScopeEvaluator.CellsInRange(owner, 1, grid.width, grid.height))
                        list.Add(c);
                    break;
                case EffectScope.OwnPowerPlant:
                    if (owner.footprint != null)
                        for (int i = 0; i < owner.footprint.Count; i++) list.Add(owner.footprint[i]);
                    break;
                case EffectScope.Global:
                case EffectScope.Zone:
                    // 보드 전체를 깔면 시야를 가리므로 비워둔다. 시퀀서가 텍스트로만 강조.
                    break;
            }
            return list;
        }
    }
}
