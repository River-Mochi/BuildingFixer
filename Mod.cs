// Mod.cs
// Purpose: Entrypoint for ABB — registers settings/locales; schedules system; triggers run if already in-game.

namespace AbandonedBuildingBoss
{
    using System.Reflection;            // AssemblyVersion
    using Colossal.IO.AssetDatabase;    // Saved Settings
    using Colossal.Logging;             // ILog
    using Game;                         // UpdateSystem
    using Game.Modding;                 // IMod
    using Game.SceneFlow;               // GameManager, GameMode
    using Unity.Entities;               // World.Default

    public sealed class Mod : IMod
    {
        // ---- Metadata ----
        public const string ModName = "Abandoned Building Boss [ABB]";

        // Version from AssemblyVersion (<Version> in .csproj)
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // ---- Logging ----
        public static readonly ILog s_Log =
            LogManager.GetLogger("AbandonedBuildingBoss")
#if DEBUG
                .SetShowsErrorsInUI(true);
#else
                .SetShowsErrorsInUI(false);
#endif

        // ---- Settings instance ----
        public static Setting? Settings
        {
            get; private set;
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            s_Log.Info($"{ModName} v{ModVersion} OnLoad");

            Setting setting = new Setting(this);
            Settings = setting;

            // Locales first so the UI shows strings immediately
            GameManager? gm = GameManager.instance;
            var lm = gm?.localizationManager; // explicit type isn't critical here
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // Load saved settings, then register Options UI
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // Seed Status so row is not blank before first pass
            setting.SetStatus("No city loaded", countedNow: false);

            // ECS system — run in GameSimulation at a steady cadence
            updateSystem.UpdateAt<AbandonedBuildingBossSystem>(SystemUpdatePhase.GameSimulation);

            // If already in a running city when the mod loads, do one pass.
            if (gm != null && gm.gameMode == GameMode.Game)
            {
                World? world = World.DefaultGameObjectInjectionWorld;
                AbandonedBuildingBossSystem? sys =
                    world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
                sys?.RequestRunNextTick();

#if DEBUG
                s_Log.Info("[ABB] Detected active city at mod load -> scheduled first pass (RequestRunNextTick).");
#endif
            }

#if DEBUG
            if (gm != null && gm.modManager.TryGetExecutableAsset(this, out var asset))
                s_Log.Info($"Current mod asset at {asset.path}");
#endif
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
