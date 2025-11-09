// File: Mod.cs
// Purpose: Entrypoint for Abandoned Building Boss [ABB]; registers settings, locale,
//          and schedules the ECS system to run in GameSimulation.

namespace AbandonedBuildingBoss
{
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    public sealed class Mod : IMod
    {
        // About tab
        public const string ModName = "Abandoned Building Boss [ABB]";
        public const string ModVersion = "0.4.0";

        // Logger (single id, no ".Mod" suffix)
        public static readonly ILog Log =
            LogManager.GetLogger("AbandonedBuildingBoss").SetShowsErrorsInUI(
#if DEBUG
                true
#else
                false
#endif
            );

        // Settings instance exposed to the ECS system
        public static Setting? Settings;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"{ModName} v{ModVersion} OnLoad");

            // Create settings object
            var setting = new Setting(this);
            Settings = setting;

            // Register Options UI first
            setting.RegisterInOptionsUI();

            // Load saved values (or create new with defaults on first run)
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));

            // Register locale
            var lm = GameManager.instance?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // Explicitly schedule ECS system (like original mod did)
            // Run after vanilla building simulation.
            updateSystem.UpdateAfter<AbandonedBuildingBossSystem>(SystemUpdatePhase.GameSimulation);

            Log.Info("[ABB] AbandonedBuildingBossSystem scheduled after GameSimulation.");
        }

        public void OnDispose()
        {
            Log.Info("OnDispose");

            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
