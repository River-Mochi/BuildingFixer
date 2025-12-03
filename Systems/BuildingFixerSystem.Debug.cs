// BuildingFixerSystem.Debug.cs
// Debug-only logging helpers for Building Fixer.

namespace BuildingFixer
{
    using System.Diagnostics;

    public sealed partial class BuildingFixerSystem
    {
        [Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
            Mod.s_Log.Debug($"[BF] {message}");
        }
    }
}
