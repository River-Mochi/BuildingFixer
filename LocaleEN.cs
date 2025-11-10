// LocaleEN.cs
// Purpose: en-US strings for Abandoned Building Boss [ABB].

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
                // Settings title
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),     "Status" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // Main behavior dropdown
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Behavior)), "Handling behavior" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.Behavior)),
                    "What ABB should do to abandoned / condemned buildings each update.\n" +
                    "• Auto-demolish – bulldoze abandoned buildings (original mod behavior).\n" +
                    "• Restore buildings – keep the buildings, clear flags, and reset condition.\n" +
                    "• Do nothing – disable automatic handling; use the buttons instead."
                },

                // Behavior dropdown items (keys used by GetBehaviorDropdownItems)
                { "ABB.Behavior.AutoDemolish",     "Auto-demolish abandoned" },
                { "ABB.Behavior.RestoreBuildings", "Restore buildings (no demolish)" },
                { "ABB.Behavior.None",             "Do nothing (manual only)" },

                // Extra keys that match the auto-generated enum names in Options UI (for safety)
                {
                    "Options.AbandonedBuildingBoss.AbandonedBuildingBoss.Mod.ABANDONEDHANDLINGMODE[AutoDemolish]",
                    "Auto-demolish abandoned"
                },
                {
                    "Options.AbandonedBuildingBoss.AbandonedBuildingBoss.Mod.ABANDONEDHANDLINGMODE[DisableAbandonment]",
                    "Restore buildings (no demolish)"
                },
                {
                    "Options.AbandonedBuildingBoss.AbandonedBuildingBoss.Mod.ABANDONEDHANDLINGMODE[None]",
                    "Do nothing (manual only)"
                },

                // Also clear condemned
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AlsoClearCondemned)), "Also clear condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AlsoClearCondemned)),
                    "Include condemned-only buildings when counting or handling."
                },

                // Buttons
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CountAbandoned)), "Count abandoned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.CountAbandoned)),
                    "Scan the current city and show the number of abandoned / condemned buildings."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreNow)), "Restore buildings now" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreNow)),
                    "Restore abandoned (and optionally condemned) buildings without demolishing them."
                },

                // Status line
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedStatus)), "Abandoned buildings" },

                // About tab
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                // Links
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenMods)),    "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenMods)),     "Open author's Paradox Mods page." },
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
