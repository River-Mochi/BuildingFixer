// Setting.cs
// Purpose: Options UI — layout per spec; toggles + Status + Refresh; separate bottom “RESCUE” group.
// Notes: SetRefreshPrompt() is UI-only; Apply() nudges system to run next tick (only when in-game).

namespace AbandonedBuildingBoss
{
    using System;                        // Exception handling
    using Colossal.IO.AssetDatabase;     // [FileLocation]
    using Game;
    using Game.Modding;                  // IMod, ModSetting
    using Game.SceneFlow;                // GameManager, GameMode
    using Game.Settings;                 // Settings UI attributes
    using Unity.Entities;                // World.DefaultGameObjectInjectionWorld
    using UnityEngine;                   // Application.OpenURL

    [FileLocation("ModsSettings/AbandonedBuildingBoss/ABB")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(
        kAutoRemovalGroup, kAutoRestoreGroup, kCondemnedGroup, kStatusGroup,
        kRescueGroup, // always last
        kAboutInfoGroup, kAboutLinksGroup)]
    [SettingsUIShowGroupName(kAutoRemovalGroup, kAutoRestoreGroup, kCondemnedGroup, kStatusGroup, kRescueGroup, kAboutInfoGroup)]
    public sealed class Setting : ModSetting
    {
        // ---- Tabs ----
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // ---- Action groups (by spec) ----
        public const string kAutoRemovalGroup = "AUTO REMOVAL";                 // demolish
        public const string kAutoRestoreGroup = "AUTO RESTORE — No Demolish";   // heal
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";
        public const string kStatusGroup = "STATUS";                       // refresh + text
        public const string kRescueGroup = "RESCUE";                       // deep one-shot

        // ---- About groups ----
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // ---- External Links ----
        private const string kUrlDiscord = "https://discord.gg/HTav7ARPs2";
        private const string kUrlParadox = "https://mods.paradoxplaza.com/uploaded?orderBy=desc&sortBy=popularity";

        // ---- Backing fields ----
        private string m_StatusText = "No city loaded";
        private DateTime m_LastCountTime = DateTime.MinValue;
        private bool m_RequestRefresh;
        private bool m_RequestRescueAllNow;

        // UI prompt hint
        private bool m_ShowRefreshPrompt;
        private const string kPressRefreshPrompt = "Click Refresh for latest counts";

        // Guard: avoid rescheduling when Apply() is UI-only
        private bool m_SuppressReapply;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            // Defaults
            RemoveAbandoned = true;     // quick win
            DisableAbandonment = false;

            RemoveCollapsed = false;
            DisableCollapsed = false;

            DisableCondemned = false;
            RemoveCondemned = false;

            m_StatusText = "No city loaded";
            m_LastCountTime = DateTime.MinValue;
            m_RequestRefresh = false;
            m_RequestRescueAllNow = false;
            m_ShowRefreshPrompt = false;
        }

        // Apply nudges the system to re-run next tick so changes take effect immediately (in-game only).
        public override void Apply()
        {
            base.Apply();
            if (m_SuppressReapply)
                return;

            bool inGame = GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game;
            if (inGame)
            {
                SetRefreshPrompt(true);   // UI hint until next real count
                m_RequestRefresh = true;  // ask for immediate recount/run

                World? world = World.DefaultGameObjectInjectionWorld;
                world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>()?.RequestRunNextTick();
            }
        }

        // ---- AUTO REMOVAL ----
        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableAbandonmentChecked))]
        public bool RemoveAbandoned { get; set; } = false; // auto-demolish Abandoned

        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCollapsedChecked))]
        public bool RemoveCollapsed { get; set; } = false; // auto-demolish Collapsed

        // ---- AUTO RESTORE — No Demolish ----
        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment { get; set; } = true; // heal Abandoned and keep alive

        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCollapsedChecked))]
        public bool DisableCollapsed { get; set; } = false;  // heal Collapsed, clear rescue/damage

        // ---- CONDEMNED BUILDINGS ----
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned { get; set; } = false;

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned { get; set; } = false;

        // ---- STATUS (Refresh on top, Status text right below) ----
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kStatusGroup)]
        public bool RefreshStatus
        {
            set
            {
                m_RequestRefresh = true;
            }
        }

        [SettingsUISection(kActionsTab, kStatusGroup)]
        public string Status =>
            m_ShowRefreshPrompt && (GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game)
                ? kPressRefreshPrompt
                : (string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText);

        // ---- RESCUE (bottom) ----
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kRescueGroup)]
        public bool RescueAllNow
        {
            set
            {
                m_RequestRescueAllNow = true;
            }
        }

        // ---- ABOUT ----
        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModName => Mod.ModName;

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModVersion => Mod.ModVersion;

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenParadox
        {
            set
            {
                if (!value)
                    return;
                try
                {
                    Application.OpenURL(kUrlParadox);
                }
                catch (Exception ex) { Mod.s_Log.Warn($"Open Paradox failed: {ex.Message}"); }
            }
        }

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenDiscord
        {
            set
            {
                if (!value)
                    return;
                try
                {
                    Application.OpenURL(kUrlDiscord);
                }
                catch (Exception ex) { Mod.s_Log.Warn($"Open Discord failed: {ex.Message}"); }
            }
        }

        // ---- Helpers (mutual exclusion) ----
        public bool IsRemoveAbandonedChecked() => RemoveAbandoned;
        public bool IsDisableAbandonmentChecked() => DisableAbandonment;
        public bool IsRemoveCollapsedChecked() => RemoveCollapsed;
        public bool IsDisableCollapsedChecked() => DisableCollapsed;
        public bool IsRemoveCondemnedChecked() => RemoveCondemned;
        public bool IsDisableCondemnedChecked() => DisableCondemned;

        // ---- System handoff ----
        public bool TryConsumeRefreshRequest()
        {
            if (!m_RequestRefresh)
                return false;
            m_RequestRefresh = false;
            return true;
        }

        public bool TryConsumeRescueAllNowRequest()
        {
            if (!m_RequestRescueAllNow)
                return false;
            m_RequestRescueAllNow = false;
            return true;
        }

        // Sets visible status text; stamps last-updated when countedNow == true
        public void SetStatus(string text, bool countedNow)
        {
            string display = string.IsNullOrEmpty(text) ? "No city loaded" : text;

            if (countedNow)
            {
                m_LastCountTime = DateTime.Now;
                m_StatusText = $"{display}  —  Last updated {m_LastCountTime:HH:mm}";
            }
            else
            {
                // When not in a city, keep it simple; otherwise show stale hint
                bool inGame = GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game;
                m_StatusText = (!inGame || m_LastCountTime == DateTime.MinValue)
                    ? display
                    : $"{display}  (stale — Click Refresh)";
            }

            m_SuppressReapply = true;   // UI-only repaint
            base.Apply();
            m_SuppressReapply = false;
        }

        // UI-only prompt toggler
        public void SetRefreshPrompt(bool show)
        {
            if (m_ShowRefreshPrompt == show)
                return;
            m_ShowRefreshPrompt = show;

            m_SuppressReapply = true;   // UI-only repaint
            base.Apply();
            m_SuppressReapply = false;
        }
    }
}
