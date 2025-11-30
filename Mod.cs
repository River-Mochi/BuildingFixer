// Mod.cs
// Purpose: Entrypoint — registers settings/locales; schedules system; seeds status; nudges system if already in-game.

namespace BuildingFixer
{
    using System.Reflection;            // AssemblyVersion
    using Colossal.IO.AssetDatabase;    // Saved Settings
    using Colossal.Localization;
    using Colossal.Logging;             // ILog
    using Game;                         // UpdateSystem
    using Game.Modding;                 // IMod
    using Game.SceneFlow;               // GameManager, GameMode
    using Unity.Entities;               // World.Default

    public sealed class Mod : IMod
    {
        // Metadata
        public const string ModName = "Building Fixer";
        public const string ModId = "BuildingFixer";
        public const string ModTag = "[BF]";

        // Read <Version> from .csproj (3-part)
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // Private state
        private static bool s_BannerLogged;

        // Logging
        public static readonly ILog s_Log =
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
            if (!s_BannerLogged)
            {
                s_BannerLogged = true;
                s_Log.Info($"{ModName} {ModTag} v{ModVersion} OnLoad");
            }

            var setting = new Setting(this);
            Settings = setting;

            // Locales first so UI strings render immediately
            GameManager? gm = GameManager.instance;
            LocalizationManager? lm = gm?.localizationManager;
            lm?.AddSource("en-US", new LocaleEN(setting));

            // Load saved settings, then register Options UI
            AssetDatabase.global.LoadSettings("BuildingFixer", setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // Seed status text before first count
            setting.SetStatus("No city loaded", countedNow: false);

            // Register ECS system at GameSimulation cadence
            updateSystem.UpdateAt<BuildingFixerSystem>(SystemUpdatePhase.GameSimulation);

            // If already in-game when mod loads, schedule a run (no auto-count)
            if (gm != null && gm.gameMode == GameMode.Game)
            {
                World world = World.DefaultGameObjectInjectionWorld;
                BuildingFixerSystem? sys = world?.GetExistingSystemManaged<BuildingFixerSystem>();
                sys?.RequestRunNextTick();
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
