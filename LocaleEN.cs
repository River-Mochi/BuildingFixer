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
                // Mod name in Options list
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups (Actions/Buttons name hidden by attribute, but keys are harmless)
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),     "Status" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // ---- Handling behavior dropdown ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Behavior)), "Handling behavior" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Behavior)),
                  "Choose how ABB should treat abandoned buildings each update.\n" +
                  "• Do nothing – no automatic changes.\n" +
                  "• Auto-demolish abandoned – bulldoze abandoned buildings (like the original mod).\n" +
                  "• Clear flags (keep buildings) – try to restore buildings instead of demolishing them." },

                // Dropdown items (must match enum names)
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.None)),               "Do nothing" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.AutoDemolish)),       "Auto-demolish abandoned" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedHandlingMode.DisableAbandonment)), "Clear flags (keep buildings)" },

                // ---- Toggle: also clear condemned ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlsoClearCondemned)), "Also include condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AlsoClearCondemned)),
                  "When counting, restoring, or auto-handling, also include buildings that are Condemned.\n" +
                  "Condemned = wrong zone / outside zoning grid; normally slated for demolition by the game." },

                // ---- Buttons ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CountAbandoned)), "Count abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.CountAbandoned)),
                  "Scan the current city and show how many buildings are currently abandoned.\n" +
                  "If 'Also include condemned' is enabled, shows both Abandoned and Condemned counts.\n" +
                  "If no city is loaded yet, shows 'No city loaded'." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreBuildings)), "Restore buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreBuildings)),
                  "Experimental / alpha feature.\n" +
                  "Attempts to restore abandoned (and optionally condemned) buildings instead of demolishing them:\n" +
                  "• Removes Abandoned flag.\n" +
                  "• Optionally removes Condemned flag.\n" +
                  "• Resets building condition.\n" +
                  "• Re-adds basic service components if missing.\n\n" +
                  "Use with caution; this path is not as battle-tested as auto-demolish." },

                // ---- Status ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedStatus)), "Abandoned buildings" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AbandonedStatus)),
                  "Shows the last result from [Count abandoned] or [Restore buildings].\n" +
                  "Examples:\n" +
                  "• 'Abandoned: 12'\n" +
                  "• 'Abandoned: 10  |  Condemned: 3'\n" +
                  "• 'No city loaded'" },

                // ---- About tab: info ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                // ---- Links ----
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenMods)),    "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenMods)),
                  "Open the author's Paradox Mods page in your browser." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenDiscord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenDiscord)),
                  "Join the support / feedback channel on Discord." },
            };

            return d;
        }

        public void Unload()
        {
        }
    }
}
