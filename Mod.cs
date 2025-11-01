// File: Mod.cs
// Purpose: Entrypoint for [ABB] Abandoned Building Boss; registers settings + locale.

namespace AbandonedBuildingBoss
{
    using Colossal.IO.AssetDatabase;
    using Colossal.Logging;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;

    public sealed class Mod : IMod
    {
        public const string ModName = "[ABB] Abandoned Building Boss";
        public const string ModVersion = "0.3.0";

        // no ".Mod" suffix per your rule
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

            // settings
            var setting = new Setting(this);
            Settings = setting;

            // options UI
            setting.RegisterInOptionsUI();

            // load (or create) stored settings
            AssetDatabase.global.LoadSettings("AbandonedBuildingBoss", setting, new Setting(this));

            // locale
            var lm = GameManager.instance?.localizationManager;
            if (lm != null)
            {
                lm.AddSource("en-US", new LocaleEN(setting));
            }

            // systems are discovered automatically
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
