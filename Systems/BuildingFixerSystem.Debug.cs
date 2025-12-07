// Systems/BuildingFixerSystem.Debug.cs
// Debug-only logging helpers for Building Fixer.

namespace BuildingFixer
{
    using System;
    using System.Diagnostics;

    public sealed partial class BuildingFixerSystem
    {
        [Conditional("DEBUG")]
        private static void DebugLog(string message)
        {
            Mod.s_Log.Debug($"[BF][DEBUG] {message}");
        }

        [Conditional("DEBUG")]
        private static void DebugLogException(string context, Exception ex)
        {
            Mod.s_Log.Debug(
                $"[BF][DEBUG] {context} exception: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
