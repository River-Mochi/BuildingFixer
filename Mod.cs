// File: Mod.cs
// Purpose: entrypoint for Abandoned Building Boss [ABB]; registers settings, locales, and ensures ECS system is created.
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


        public static readonly ILog Log =
            LogManager.GetLogger("AbandonedBuildingBoss").SetShowsErrorsInUI(
#if DEBUG
                true
#else
                false
#endif
            );

        public static Setting? Settings;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"{ModName} v{ModVersion} OnLoad");

            // settings object
            var setting = new Setting(this);
            Settings = setting;

            // show in Options
            setting.RegisterInOptionsUI();

            // load saved values (or create with defaults)
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));

            // locale
            var lm = GameManager.instance?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }
            // ECS system is auto-discovered
        }

        public void OnDispose()
        {
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
