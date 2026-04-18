using Special.Runtime;

namespace Special.Effects
{
    public interface IEffect
    {
        EffectScope Scope { get; }
        void Activate(SpecialBlockInstance owner, EffectRuntime runtime);
        void Deactivate(SpecialBlockInstance owner, EffectRuntime runtime);
    }
}
