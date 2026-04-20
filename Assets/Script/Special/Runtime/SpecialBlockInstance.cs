using System.Collections.Generic;
using Special.Data;
using Special.Effects;
using UnityEngine;

namespace Special.Runtime
{
    /// <summary>
    /// 보드 위에 실제로 설치된 특수 블럭 1개. 설치 시 Registry 가 생성, 제거 시 파괴.
    /// footprint 는 배열 인덱스(0..W-1, 0..H-1) 기준.
    /// </summary>
    public class SpecialBlockInstance
    {
        public readonly int instanceId;
        public readonly SpecialBlockDefinition definition;
        public readonly Vector2Int anchorCell;
        public readonly IReadOnlyList<Vector2Int> footprint;
        public int zoneId;
        public int groupId;   // 0 = 미그룹
        public int placementDay; // SpecialBlockRegistry.RegisterPlacement 에서 ResourceManager.TotalDay 로 세팅. 드래그 프리뷰는 0.

        private readonly List<EffectAsset> effectInstances = new();

        public IReadOnlyList<EffectAsset> EffectInstances => effectInstances;

        public SpecialBlockInstance(int instanceId, SpecialBlockDefinition def, Vector2Int anchor, IReadOnlyList<Vector2Int> footprint, int zoneId)
        {
            this.instanceId = instanceId;
            this.definition = def;
            this.anchorCell = anchor;
            this.footprint = footprint;
            this.zoneId = zoneId;
        }

        public void AddEffect(EffectAsset effect) => effectInstances.Add(effect);
        public void ClearEffects() => effectInstances.Clear();

        public bool FootprintContains(Vector2Int cell)
        {
            for (int i = 0; i < footprint.Count; i++)
                if (footprint[i] == cell) return true;
            return false;
        }

        /// <summary>
        /// 드래그 중 스코프 계산용 일회성 인스턴스. 레지스트리에 등록되지 않으며
        /// instanceId/zoneId 는 0 으로 고정. footprint 는 def.shapeCoords 를 anchor 에서 오프셋 해 만들고
        /// 보드 경계를 벗어난 칸은 잘라낸다. 전부 잘리면 null 반환.
        /// </summary>
        public static SpecialBlockInstance CreateDragPreview(SpecialBlockDefinition def, Vector2Int anchorArrayCell, int width, int height)
        {
            if (def == null) return null;
            Vector2Int[] shape = def.shapeCoords;
            if (shape == null || shape.Length == 0) return null;

            List<Vector2Int> footprint = new List<Vector2Int>();
            for (int i = 0; i < shape.Length; i++)
            {
                Vector2Int cell = anchorArrayCell + shape[i];
                if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) continue;
                footprint.Add(cell);
            }
            if (footprint.Count == 0) return null;

            return new SpecialBlockInstance(0, def, anchorArrayCell, footprint, 0);
        }
    }
}
