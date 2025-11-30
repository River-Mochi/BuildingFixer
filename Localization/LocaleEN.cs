// LocaleEN.cs
// Purpose: en-US strings (Actions | RESCUE | About). Includes Recommended and Empty Buildings bonus.

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
            IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            var d = new Dictionary<string, string>
            {
                // Title
                { m_Setting.GetSettingsLocaleID(), "Building Fixer [BF]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kRescueTab),  "RESCUE"  },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // ===== Actions tab groups =====
                { m_Setting.GetOptionGroupLocaleID(Setting.kRecommendedGroup), "RECOMMENDED" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRemovalGroup), "AUTO REMOVAL" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRestoreGroup), "AUTO RESTORE — No Demolish" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCondemnedGroup),   "CONDEMNED BUILDINGS" },

                // RECOMMENDED button
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RecommendedNow)), "RECOMMENDED (one-click)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RecommendedNow)), "Enable suggested defaults:\n• Auto Remove Abandoned\n• Auto Remove Collapsed\n• Disable Condemned" },

                // AUTO REMOVAL
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveAbandoned)), "Auto Remove Abandoned" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RemoveAbandoned)), "Automatically demolishes **Abandoned** buildings and nudges nearby edges." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCollapsed)), "Auto Remove Collapsed" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RemoveCollapsed)), "Automatically demolishes **Collapsed** buildings, including icon-only cases." },

                // AUTO RESTORE — No Demolish
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableAbandonment)), "Disable Abandoned (no demolish)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.DisableAbandonment)), "Clears Abandoned, restores services/icons, keeps the building alive." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCollapsed)),   "Disable Collapsed (no demolish)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.DisableCollapsed)),  "Clears Collapsed or collapsed-icon, removes Rescue/Damage + icons, refreshes roads." },

                // CONDEMNED
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCondemned)), "Disable Condemned" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.DisableCondemned)), "Clears Condemned so zoning can adjust; removes its icon." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCondemned)),  "Auto Remove Condemned" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RemoveCondemned)),  "Automatically demolishes **Condemned** buildings." },

                // ===== RESCUE tab groups =====
                { m_Setting.GetOptionGroupLocaleID(Setting.kRescueNowGroup),      "RESCUE NOW" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kEmptyBuildingsGroup), "EMPTY BUILDINGS" },

                // RESCUE NOW
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RescueAllNow)), "RESCUE ALL (deep restore)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RescueAllNow)), "Deep clean: heal Abandoned/Collapsed/Condemned, scrub icons, refresh edges." },

                // EMPTY BUILDINGS (bonus)
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanCommercialEmpty)), "Commercial (empty)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.CleanCommercialEmpty)), "Targets **Commercial** buildings with no tenants and not on/pending market." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanIndustrialEmpty)), "Industrial (empty)" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.CleanIndustrialEmpty)), "Targets **Industrial** buildings with no tenants and not on/pending market." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshStatus)), "Refresh Status" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.RefreshStatus)), "Recount Abandoned / Condemned / Collapsed or Empty (if selected)." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Status)), "Status" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.Status)),  "Shows the last count line and when it was updated." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.CleanupEmptyNow)), "Cleanup" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.CleanupEmptyNow)), "Deletes selected empty buildings; area is nudged so lots naturally re-grow." },

                // About
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.ModVersion)),  "Current mod version." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenParadox)), "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.OpenParadox)),  "Open author’s Paradox Mods page." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenDiscord)), "Discord" },
                { m_Setting.GetOptionDescLocaleID (nameof(Setting.OpenDiscord)),  "Join the support / feedback channel." },
            };
            return d;
        }

        public void Unload()
        {
        }
    }
}
