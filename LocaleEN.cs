// LocaleEN.cs
// Purpose: en-US strings for Building Fixer [BF].namespace BuildingFixer

namespace BuildingFixer
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
                // Options title (single source of truth from Mod.cs)
                { m_Setting.GetSettingsLocaleID(), Mod.ModName + " " + Mod.ModTag },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kActionsGroup),    "Actions" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kButtonsGroup),    "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),     "STATUS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // Auto options
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AutoDemolishAbandoned)), "Auto demolish abandoned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AutoDemolishAbandoned)),
                    "While enabled, BF will automatically bulldoze abandoned buildings."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AutoDemolishCondemned)), "Auto demolish condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AutoDemolishCondemned)),
                    "While enabled, BF will automatically bulldoze condemned buildings."
                },

                // Buttons – remodel
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemodelAbandonedNow)), "Remodel Abandoned Now" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemodelAbandonedNow)),
                    "Restore abandoned buildings without demolishing them. Clears the abandoned flag, resets condition, and restores services."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemodelCondemnedNow)), "Remodel Condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemodelCondemnedNow)),
                    "Restore condemned buildings without demolishing them. Clears the condemned flag, resets condition, and restores services."
                },

                // Status + refresh
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshCount)), "Refresh Count" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RefreshCount)),
                    "Re-scan the current city and update abandoned / condemned / collapsed counts in the status line."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AbandonedStatus)), "STATUS" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.AbandonedStatus)),
                    "Shows the latest counts or result message (for example: \"Abandoned: 0 | Condemned: 1711 | Collapsed: 3\")."
                },

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
