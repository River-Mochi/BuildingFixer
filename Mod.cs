// Mod.cs
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

            var setting = new Setting(this);
            Settings = setting;

            // 1) Register in Options UI
            setting.RegisterInOptionsUI();

            // 2) Load saved settings (or apply defaults)
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));

            // 3) Register en-US locale
            var gm = GameManager.instance;
            var lm = gm?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // 4) Register ECS system to run in GameSimulation phase
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
