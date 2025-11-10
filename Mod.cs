// File: Mod.cs
// Purpose: entrypoint for Abandoned Building Boss [ABB]; registers settings, locales, and ECS system.

namespace AbandonedBuildingBoss
{
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    public sealed class Mod : IMod
    {
        public const string ModName = "Abandoned Building Boss [ABB]";
        public const string ModVersion = "0.4.0";

        public static readonly ILog Log =
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
            Log.Info($"{ModName} v{ModVersion} OnLoad");

            // Create settings object
            var setting = new Setting(this);
            Settings = setting;

            // Register in Options UI first
            setting.RegisterInOptionsUI();

            // Load saved values (or defaults on first run)
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));

            // Register English locale
            var gm = GameManager.instance;
            var lm = gm?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // VERY IMPORTANT: register our ECS system so OnUpdate actually runs
            updateSystem.UpdateAfter<AbandonedBuildingBossSystem>(SystemUpdatePhase.GameSimulation);

            if (gm != null && gm.modManager.TryGetExecutableAsset(this, out var asset))
            {
                Log.Info($"Current mod asset at {asset.path}");
            }
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));

            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
