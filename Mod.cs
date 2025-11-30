// Mod.cs
// Purpose: entrypoint for Building Fixer [BF]; registers settings, locales, and ECS system.

namespace BuildingFixer
{
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    public sealed class Mod : IMod
    {
        // ---- PUBLIC CONSTANTS / METADATA ----
        public const string ModName = "Building Fixer";
        public const string ModId = "BuildingFixer";
        public const string ModTag = "[BF]";

        /// <summary>
        /// Read Version from .csproj (3-part).
        /// </summary>
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";



        public static readonly ILog Log =
            LogManager.GetLogger("BuildingFixer")
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

            // Register in Options UI
            setting.RegisterInOptionsUI();

            // Load saved settings (or apply defaults)
            AssetDatabase.global.LoadSettings("BuildingFixer", setting, new Setting(this));

            // Locale
            var gm = GameManager.instance;
            var lm = gm?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // Register ECS system to run in GameSimulation phase
            updateSystem.UpdateAfter<BuildingFixer>(SystemUpdatePhase.GameSimulation);

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
