// LocaleEN.cs
// Purpose: en-US strings (Actions | About) for Building Fixer [BF].

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
                // ===== Title =====
                { m_Setting.GetSettingsLocaleID(), "Building Fixer [BF]" },

                // ===== Tabs =====
                { m_Setting.GetOptionTabLocaleID(Setting.kActionsTab), "Actions" },
                { m_Setting.GetOptionTabLocaleID(Setting.kAboutTab),   "About"   },

                // ===== Actions tab groups =====
                { m_Setting.GetOptionGroupLocaleID(Setting.kRecommendedGroup), "RECOMMENDED" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRemovalGroup), "AUTO REMOVAL" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAutoRestoreGroup), "AUTO RESTORE — No Demolish" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kCondemnedGroup),   "CONDEMNED BUILDINGS" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kStatusGroup),      "STATUS" },

                // ===== About tab groups =====
                // Info header intentionally blank (group header hidden).
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutInfoGroup),  "" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAboutLinksGroup), "Links" },

                // =====================================================================
                // RECOMMENDED
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RecommendedNow)), "RECOMMENDED (one-click)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RecommendedNow)),
                    "Enable suggested defaults:\n" +
                    "• Auto Remove Abandoned\n" +
                    "• Auto Remove Collapsed\n" +
                    "• Disable Condemned"
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResetToGameDefaults)), "Reset to Game Defaults" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.ResetToGameDefaults)),
                    "Turn all Building Fixer toggles OFF and go back to pure vanilla behaviour. " +
                    "Does not demolish or restore anything by itself."
                },

                // =====================================================================
                // AUTO REMOVAL
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveAbandoned)), "Auto Remove Abandoned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveAbandoned)),
                    "Automatically demolishes **Abandoned** buildings."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCollapsed)), "Auto Remove Collapsed" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCollapsed)),
                    "Automatically demolishes **Collapsed** buildings."
                },

                // =====================================================================
                // AUTO RESTORE — No Demolish
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableAbandonment)), "Disable Abandoned (no demolish)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableAbandonment)),
                    "Prevents buildings from staying abandoned going forward (no demolish).\n" +
                    "For a one-time citywide cleanup, use \"Restore existing Abandoned now\".\n" +
                    "Turn this off if you want full disaster ruin visuals."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCollapsed)), "Disable Collapsed (no demolish)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCollapsed)),
                    "Clears Collapsed flags and damage, then refreshes roads."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RestoreExistingAbandonedNow)), "Restore existing Abandoned now" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RestoreExistingAbandonedNow)),
                    "One-time fix for all currently abandoned buildings (including icon-only cases).\n" +
                    "Clears flags/icons, nudges, and refreshes visuals.\n" +
                    "Does not fix the underlying causes, so some may become abandoned again later."
                },

                // =====================================================================
                // CONDEMNED BUILDINGS
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.DisableCondemned)), "Disable Condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.DisableCondemned)),
                    "Stops the vanilla Condemned demolish pipeline and clears the Condemned state " +
                    "so zoning can adjust instead of auto-demolishing."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RemoveCondemned)), "Auto Remove Condemned" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RemoveCondemned)),
                    "Automatically demolishes **Condemned** buildings."
                },

                // =====================================================================
                // STATUS
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RefreshStatus)), "Refresh Status" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.RefreshStatus)),
                    "Recount Abandoned / Condemned / Collapsed and update the status line."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Status)), "Status" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.Status)),
                    "Shows the last count line (Abandoned / Condemned / Collapsed) " +
                    "and when it was updated."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NudgeAndClearProblemIconsOnce)), "Nudge + Clear Problem Icons (once)" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.NudgeAndClearProblemIconsOnce)),
                    "One-shot cleanup: gently refresh building + lot, mark them Updated, " +
                    "and let the game refresh problem icons (helps with stuck icons)."
                },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SmartAccessNudgeOnce)), "Smart Nudge: Ped & Car Access" },
                {
                    m_Setting.GetOptionDescLocaleID(nameof(Setting.SmartAccessNudgeOnce)),
                    "One-shot nudge for buildings that have a road edge but still report " +
                    "\"No Pedestrian Access\" or \"No Car Access\". Moves them slightly and " +
                    "refreshes access checks."
                },

                // =====================================================================
                // About tab
                // =====================================================================

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModName)),    "Mod name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModName)),     "Display name of this mod." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModVersion)), "Version" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ModVersion)),  "Current mod version." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.OpenParadox)), "Paradox Mods" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.OpenParadox)),  "Open author's Paradox Mods page." },
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
