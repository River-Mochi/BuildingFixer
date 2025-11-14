// LocaleEN.cs
// Purpose: en-US labels/descriptions for ABB.

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
            Dictionary<string, string> d = new Dictionary<string, string>
            {
                // Option UI Title
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kAbandonedGroup), "ABANDONED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCollapsedGroup), "COLLAPSED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCondemnedGroup), "CONDEMNED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),    "STATUS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup), "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // Abandoned
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveAbandoned)), "Auto Remove Abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveAbandoned)),  "Automatically demolishes **Abandoned** buildings." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableAbandonment)), "Disable Abandonment" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableAbandonment)),  "Keeps buildings alive by clearing Abandoned flags and restoring services." },

                // One-shot
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreAbandonedNow)), "Restore Abandoned Now" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreAbandonedNow)),  "Immediately clears Abandoned from all current buildings and restores services." },

                // Collapsed
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCollapsed)), "Auto Remove Collapsed" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCollapsed)),  "Automatically demolishes **Collapsed** buildings." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCollapsed)), "Disable Collapsed" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCollapsed)),  "Clears Collapsed and cleans up damage/rescue requests over time." },

                // Condemned
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCondemned)), "Disable Condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCondemned)),  "Prevents removal by clearing the Condemned flag so zoning can be adjusted." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCondemned)), "Auto Remove Condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCondemned)),  "Automatically demolishes **Condemned** buildings." },

                // Status
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshStatus)), "Refresh Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RefreshStatus)), "Update counts for the currently loaded city; shows “No city loaded” when applicable." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Status)), "Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Status)),  "Shows counts and last-updated time; may display “stale — press Refresh” until updated." },

                // About
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                // Links
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenParadox)), "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenParadox)),  "Open author’s Paradox Mods page." },
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
