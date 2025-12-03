// LocaleEN.cs
// Purpose: en-US strings (Actions | About).
// Includes Recommended + Reset Defaults + Status group + "Restore existing Abandoned now" + Smart Access Nudge.

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
                // Title
                { m_Setting.GetSettingsLocaleID(), "Building Fixer [BF]" },

                // Tabs
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // ===== Actions tab groups =====
                { m_Setting.GetOptionGroupLocaleID(Setting.kRecommendedGroup), "RECOMMENDED" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRemovalGroup), "AUTO REMOVAL" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRestoreGroup), "AUTO RESTORE — No Demolish" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCondemnedGroup),   "CONDEMNED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),      "STATUS" },

                // RECOMMENDED row
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RecommendedNow)), "RECOMMENDED (one-click)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RecommendedNow)),
                    "Enable suggested defaults:\n• Auto Remove Abandoned\n• Auto Remove Collapsed\n• Disable Condemned"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetToGameDefaults)), "Reset to Game Defaults" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetToGameDefaults)),
                    "Turn all Building Fixer toggles OFF and go back to pure vanilla behaviour. Does not demolish or restore anything by itself."
                },

                // AUTO REMOVAL
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveAbandoned)), "Auto Remove Abandoned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveAbandoned)),
                    "Automatically demolishes **Abandoned** buildings and nudges nearby edges."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCollapsed)), "Auto Remove Collapsed" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCollapsed)),
                    "Automatically demolishes **Collapsed** buildings, including icon-only cases."
                },

                // AUTO RESTORE — No Demolish
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableAbandonment)), "Disable Abandoned (no demolish)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableAbandonment)),
                    "Prevents buildings from staying abandoned going forward (no demolish). For a one-time citywide cleanup, use \"Restore existing Abandoned now\". Turn this off if you want full disaster ruin visuals."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCollapsed)),   "Disable Collapsed (no demolish)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCollapsed)),
                    "Clears Collapsed or collapsed-icon, removes Rescue/Damage + icons, refreshes roads."
                },

                // One-shot restore existing Abandoned
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreExistingAbandonedNow)), "Restore existing Abandoned now" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreExistingAbandonedNow)),
                    "One-time fix for all currently abandoned buildings (including icon-only cases). Clears flags/icons, nudges, and refreshes visuals. Does not fix the underlying causes, so some may become abandoned again later."
                },

                // CONDEMNED
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCondemned)), "Disable Condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCondemned)),
                    "Clears Condemned so zoning can adjust; removes its icon."
                },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCondemned)),  "Auto Remove Condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCondemned)),
                    "Automatically demolishes **Condemned** buildings."
                },

                // STATUS
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshStatus)), "Refresh Status" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RefreshStatus)),
                    "Recount Abandoned / Condemned / Collapsed and update the status line."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NudgeAndClearProblemIconsOnce)), "Nudge + Clear Problem Icons (once)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.NudgeAndClearProblemIconsOnce)),
                    "One-shot cleanup: clear building problem icons, gently refresh road / lot visuals, and remove orphaned floating icons."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SmartAccessNudgeOnce)), "Smart Nudge: Ped & Car Access" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.SmartAccessNudgeOnce)),
                    "One-shot fix for buildings with **No Pedestrian Access** or **No Car Access** icons even though they have a road. Nudges them slightly back from the road edge, clears those icons, and refreshes access."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Status)), "Status" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.Status)),
                    "Shows the last count line and when it was updated."
                },

                // About
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

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
