using Special.Runtime;
using UnityEngine;

namespace Special.Effects
{
    public abstract class EffectAsset : ScriptableObject, IEffect
    {
        [SerializeField] protected EffectScope scope = EffectScope.Global;
        [Tooltip("Range scope 에서만 사용. 자기 footprint 에서 뻗는 맨해튼 거리.")]
        [SerializeField, Min(0)] protected int rangeInCells = 3;

        public EffectScope Scope => scope;
        public int RangeInCells => rangeInCells;

        public abstract void Activate(SpecialBlockInstance owner, EffectRuntime runtime);
        public abstract void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime);
    }
}
