// Mod.cs
// Purpose: Entrypoint — registers settings/locales; schedules system; no auto-count; runs passes only in-game.

namespace AbandonedBuildingBoss
{
    using System.Reflection;            // AssemblyVersion number
    using Colossal.IO.AssetDatabase;    // Saved Settings
    using Colossal.Logging;             // ILog
    using Game;                         // UpdateSystem
    using Game.Modding;                 // IMod
    using Game.SceneFlow;               // GameManager, GameMode
    using Unity.Entities;               // World.Default

    public sealed class Mod : IMod
    {
        public const string ModName = "Abandoned Building Boss [ABB]";

        // Read <Version> from .csproj (3-part)
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // Logging
        public static readonly ILog s_Log =
            LogManager.GetLogger("AbandonedBuildingBoss")
#if DEBUG
                .SetShowsErrorsInUI(true);
#else
                .SetShowsErrorsInUI(false);
#endif

        public static Setting? Settings
        {
            get; private set;
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{ModName} v{ModVersion} OnLoad");

            Setting setting = new Setting(this);
            Settings = setting;

            // Locales first so UI strings render immediately
            GameManager? gm = GameManager.instance;
            var lm = gm?.localizationManager; // UI manager
            if (lm != null)
                lm.AddSource("en-US", new LocaleEN(setting));

            // Load saved settings, then register Options UI
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // Seed status text before first count
            setting.SetStatus("No city loaded", countedNow: false);

            // Register ECS system at GameSimulation cadence
            updateSystem.UpdateAt<AbandonedBuildingBossSystem>(SystemUpdatePhase.GameSimulation);

            // If already in-game when mod loads, enable ticking (no auto-count)
            if (gm != null && gm.gameMode == GameMode.Game)
            {
                World? world = World.DefaultGameObjectInjectionWorld;
                world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>()?.RequestRunNextTick();
#if DEBUG
                s_Log.Info("[ABB] Detected active city → enabled system (no auto-count)");
#endif
            }
        }

        public void OnDispose()
        {
            s_Log.Info(nameof(OnDispose));
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
