// Mod.cs
// Purpose: Entrypoint for ABB — registers settings/locales; schedules system; triggers run if already in-game.

namespace AbandonedBuildingBoss
{
    using System.Reflection;
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Unity.Entities;

    public sealed class Mod : IMod
    {
        // ---- Metadata ----
        public const string ModName = "Abandoned Building Boss [ABB]";

        // Version from AssemblyVersion (<Version> in .csproj)
        public static readonly string ModVersion =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        // ---- Logging ----
        public static readonly ILog Log =
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
            Log.Info($"{ModName} v{ModVersion} OnLoad");

            var setting = new Setting(this);
            Settings = setting;

            // Locales first so the UI shows strings immediately
            var gm = GameManager.instance;
            var lm = gm?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // Load saved settings, then register Options UI
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));
            setting.RegisterInOptionsUI();

            // ECS system — run in GameSimulation at a steady cadence
            updateSystem.UpdateAt<AbandonedBuildingBossSystem>(SystemUpdatePhase.GameSimulation);

            // If we’re already in a running city when the mod loads, do one pass.
            if (gm != null && gm.gameMode.IsGame())
            {
                var world = World.DefaultGameObjectInjectionWorld;
                var sys = world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
                sys?.RequestRunNextTick();
            }

#if DEBUG
            if (gm != null && gm.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");
#endif
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
