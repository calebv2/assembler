namespace ArrowAssemblerNative
{
    // Server-only caller owns this timer; completion consumes exactly one recipe.
    internal sealed class AssemblyCycle
    {
        internal const float Duration = 3f;
        internal bool Active { get; private set; }
        internal float Elapsed { get; private set; }
        internal bool Tick(bool ready, float delta)
        {
            if (!ready) { Reset(); return false; }
            if (!Active) { Active = true; Elapsed = 0; return false; }
            if (delta < 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return false;
            Elapsed += delta;
            if (Elapsed < Duration) return false;
            Reset();
            return true;
        }
        internal void Reset() { Active = false; Elapsed = 0; }
    }
}
