// File: LocaleEN.cs
// Purpose: en-US strings for [ABB] Abandoned Building Boss.

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
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About" },

                // groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "" },

                // ACTIONS TAB
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Behavior)), "Handling behavior" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Behavior)),
                  "How [ABB] should treat abandoned / condemned buildings:\n" +
                  "• Do nothing – leave them as vanilla.\n" +
                  "• Auto demolish – bulldoze abandoned buildings and their sub-objects.\n" +
                  "• Disable abandonment – just clear the flags and make the building usable again." },

                // dropdown items
                { m_Setting.GetOptionLabelLocaleID("Behavior.None"),               "Do nothing" },
                { m_Setting.GetOptionLabelLocaleID("Behavior.AutoDemolish"),       "Auto demolish" },
                { m_Setting.GetOptionLabelLocaleID("Behavior.DisableAbandonment"), "Disable abandonment" },

                // toggle
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlsoClearCondemned)), "Also clear condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AlsoClearCondemned)),  "When clearing, also remove the Condemned flag if the building has it." },

                // count button + display
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CountAbandoned)), "Count abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CountAbandoned)),  "Show total abandoned buildings in the current city." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedCount)), "Abandoned buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AbandonedCount)),  "Result of the last count (or error message)." },

                // clear-now button
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ClearNow)), "Clear current abandoned now" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ClearNow)),  "Run the selected behavior immediately on the loaded city." },

                // ABOUT TAB
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenDiscord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenDiscord)),  "Join the support / feedback channel." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenMods)),    "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenMods)),     "Open the mod page." },
            };

            return d;
        }

        public void Unload()
        {
        }
    }
}
