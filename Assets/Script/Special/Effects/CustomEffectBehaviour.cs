using Special.Runtime;
using UnityEngine;

namespace Special.Effects
{
    public abstract class CustomEffectBehaviour : MonoBehaviour, IEffect
    {
        [SerializeField] protected EffectScope scope = EffectScope.Global;
        [SerializeField, Min(0)] protected int rangeInCells = 3;

        public EffectScope Scope => scope;
        public int RangeInCells => rangeInCells;

        public abstract void Activate(SpecialBlockInstance owner, EffectRuntime runtime);
        public abstract void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime);
    }
}
