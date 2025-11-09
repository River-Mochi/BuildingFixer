// File: LocaleEN.cs
// Purpose: en-US strings for ABB.

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
                // Settings title (Options UI list)
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups – Actions / Buttons group names are hidden, but labels are fine.
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),     "Status" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // Dropdown label/desc
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Behavior)), "Handling behavior" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Behavior)),
                  "Choose how ABB should treat abandoned / condemned buildings each update.\n\n" +
                  "• Auto-demolish abandoned – bulldoze abandoned buildings (like the original mod).\n" +
                  "• Restore buildings – clear flags and try to keep the building (alpha / experimental).\n" +
                  "• Do nothing – ABB is disabled." },

                // Dropdown items (must match GetBehaviorDropdownItems keys)
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.AutoDemolish)),
                  "Auto-demolish abandoned" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.DisableAbandonment)),
                  "Restore buildings (keep structure)" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.None)),
                  "Do nothing (disable ABB)" },

                // Toggle
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlsoClearCondemned)), "Also clear condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AlsoClearCondemned)),
                  "When counting or processing, also include condemned buildings.\n" +
                  "Condemned = buildings in the wrong zone or outside the zoning grid." },

                // Buttons
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CountAbandoned)), "Count abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CountAbandoned)),
                  "Scan the current city and show how many abandoned (and, optionally, condemned) buildings exist." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreBuildings)), "Restore buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreBuildings)),
                  "Alpha feature: attempt to restore abandoned / condemned buildings instead of bulldozing them.\n" +
                  "Clears flags and tries to put properties back on the market." },

                // Status line
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedStatus)), "Abandoned buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AbandonedStatus)),
                  "Shows the latest counts or messages from ABB.\n" +
                  "Examples: \"Abandoned: 0\", \"Abandoned: 12 | Condemned: 3\", or \"No city loaded\"." },

                // About tab
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                // Links
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenMods)),    "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenMods)),     "Open the author's Paradox Mods page." },
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
