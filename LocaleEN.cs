// File: LocaleEN.cs
// Purpose: English (en-US) strings for [ABB].

namespace AbandonedBuildingBoss
{
    using System.Collections.Generic;
    using Colossal;

    public sealed class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;

        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var d = new Dictionary<string, string>
            {
                // settings title
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss" },

                // tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "" },

                // actions tab
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedCount)),  "Abandoned buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AbandonedCount)),   "Last counted value or status." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CountAbandoned)),  "Count abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CountAbandoned)),   "Scan current city and show how many buildings are abandoned." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Behavior)),        "Handling behavior" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Behavior)),         "Choose what [ABB] should do with abandoned buildings." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlsoClearCondemnedToggle)), "Also clear condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AlsoClearCondemnedToggle)),  "When clearing abandoned, also remove the Condemned flag if present." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClearNow)),        "Clear current abandoned now" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClearNow)),         "Immediately process currently abandoned buildings.\nLoad a city first." },

                // about tab
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenMods)),    "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenMods)),     "Open the mod page." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenDiscord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenDiscord)),  "Join the support / feedback channel." },
            };

            return d;
        }

        public void Unload()
        {
        }
    }
}
