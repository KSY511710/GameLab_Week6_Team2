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

        private readonly List<IEffect> effectInstances = new();

        public IReadOnlyList<IEffect> EffectInstances => effectInstances;

        public SpecialBlockInstance(int instanceId, SpecialBlockDefinition def, Vector2Int anchor, IReadOnlyList<Vector2Int> footprint, int zoneId)
        {
            this.instanceId = instanceId;
            this.definition = def;
            this.anchorCell = anchor;
            this.footprint = footprint;
            this.zoneId = zoneId;
        }

        public void AddEffect(IEffect effect) => effectInstances.Add(effect);
        public void ClearEffects() => effectInstances.Clear();

        public bool FootprintContains(Vector2Int cell)
        {
            for (int i = 0; i < footprint.Count; i++)
                if (footprint[i] == cell) return true;
            return false;
        }
    }
}
