using System.Collections.Generic;
using UnityEngine;

namespace Special.Composition.Contexts
{
    /// <summary>
    /// GridManager.PlaceShape 직후 (RegisterPlacement 이후) 발화.
    /// 등록된 EffectModule 이 TargetCells 에 셀을 추가하고 OverrideColorId 를 설정하면
    /// GridManager 가 해당 셀들의 BlockData.attribute.colorID 와 비주얼을 갱신하고
    /// PowerManager.CheckAndFormGroups 를 재호출하여 BFS 를 다시 돌린다.
    /// OverrideColorId : 1=Red, 2=Blue, 3=Yellow, 0=무효 (변경 안 함)
    /// </summary>
    public class ColorOverrideContext : IEffectContext
    {
        public EffectTriggerPhase Phase => EffectTriggerPhase.OnBlockPlacedColor;
        public List<Vector2Int> TargetCells = new List<Vector2Int>();
        public int OverrideColorId;
    }
}
