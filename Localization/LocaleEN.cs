// LocaleEN.cs
// Purpose: en-US strings (renamed “Restore … (and disable)” + RESCUE group).

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
                // Title
                { m_Setting.GetSettingsLocaleID(), "Abandoned Building Boss [ABB]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // Groups
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRemovalGroup), "AUTO REMOVAL" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRestoreGroup), "AUTO RESTORE — No Demolish" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCondemnedGroup),   "CONDEMNED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),      "STATUS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kRescueGroup),      "RESCUE" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),   "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup),  "Links" },

                // AUTO REMOVAL
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveAbandoned)), "Auto Remove Abandoned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveAbandoned)),  "Automatically demolishes **Abandoned** buildings and nudges nearby edges." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCollapsed)), "Auto Remove Collapsed" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCollapsed)),  "Automatically demolishes **Collapsed** buildings, including icon-only cases." },

                // AUTO RESTORE — No Demolish
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableAbandonment)), "Restore Abandoned (and disable)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableAbandonment)),  "Clears Abandoned, restores services, removes icon, and keeps buildings alive." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCollapsed)), "Restore Collapsed (and disable)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCollapsed)),  "Clears Collapsed (or collapsed icon-only), removes Rescue/Damage + icons, and refreshes roads." },

                // CONDEMNED
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCondemned)), "Disable Condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCondemned)),  "Clears Condemned so zoning can adjust; removes its icon." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCondemned)), "Auto Remove Condemned" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCondemned)),  "Automatically demolishes **Condemned** buildings." },

                // STATUS
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshStatus)), "Refresh Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RefreshStatus)), "Recount Abandoned / Condemned / Collapsed for the loaded city." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Status)), "Status" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Status)),  "Counts + last updated; may show “stale — Click Refresh” until updated." },

                // RESCUE
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RescueAllNow)), "RESCUE ALL (deep restore)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RescueAllNow)),  "Deep clean legacy saves: heal Abandoned/Collapsed, clear Rescue/Damage, scrub icons, and refresh roads." },

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
