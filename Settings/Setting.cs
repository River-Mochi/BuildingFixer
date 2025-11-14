// Setting.cs
// Purpose: Options UI for ABB — dependable toggles + status + refresh; Apply() nudges system.
// Notes: Single prompt helper (SetRefreshPrompt), one-shot RestoreAbandonedNow, and “stale” status hint.

namespace AbandonedBuildingBoss
{
    using System;                       // Exception handling (try/catch)
    using Colossal.IO.AssetDatabase;    // [FileLocation]
    using Game.Modding;                 // IMod, ModSetting
    using Game.Settings;                // SettingsUI
    using Unity.Entities;               // World.DefaultGameObjectInjectionWorld
    using UnityEngine;                  // Application.OpenURL

    [FileLocation("ModsSettings/AbandonedBuildingBoss/ABB")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(
        kAbandonedGroup, kCollapsedGroup, kCondemnedGroup, kStatusGroup,
        kAboutInfoGroup, kAboutLinksGroup
    )]
    [SettingsUIShowGroupName(
        kAbandonedGroup, kCollapsedGroup, kCondemnedGroup, kStatusGroup, kAboutInfoGroup
    )]
    public sealed class Setting : ModSetting
    {
        // ---- Tabs ----
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // ---- Tab Groups ----
        public const string kAbandonedGroup = "ABANDONED BUILDINGS";
        public const string kCollapsedGroup = "COLLAPSED BUILDINGS";
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";
        public const string kStatusGroup = "STATUS";
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // ---- External Links ----
        private const string kUrlDiscord = "https://discord.gg/HTav7ARPs2";
        private const string kUrlParadox = "https://mods.paradoxplaza.com/uploaded?orderBy=desc&sortBy=popularity";

        // ---- Backing fields ----
        private string m_StatusText = "No city loaded";
        private DateTime m_LastCountTime = DateTime.MinValue;
        private bool m_RequestRefresh;
        private bool m_RequestRestoreAbandonedNow;

        // UI prompt helper
        private bool m_ShowRefreshPrompt;
        private const string kPressRefreshPrompt = "Click Refresh for latest counts";

        // Guard to avoid scheduling runs when we re-Apply just to refresh UI text.
        private bool m_SuppressReapply;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            RemoveAbandoned = true;     // opt-in default for quick wins
            DisableAbandonment = false;

            RemoveCollapsed = false;
            DisableCollapsed = false;

            DisableCondemned = false;
            RemoveCondemned = false;

            m_StatusText = "No city loaded";
            m_LastCountTime = DateTime.MinValue;
            m_RequestRefresh = false;
            m_RequestRestoreAbandonedNow = false;
            m_ShowRefreshPrompt = false;
        }

        // Apply nudges the system to re-run next tick so changes take effect immediately.
        public override void Apply()
        {
            base.Apply();

            if (m_SuppressReapply)
                return;

            SetRefreshPrompt(true);   // UI hint now; cleared after next real count
            m_RequestRefresh = true;  // ask for immediate recount/run

            World? world = World.DefaultGameObjectInjectionWorld;
            AbandonedBuildingBossSystem? sys =
                world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
            sys?.RequestRunNextTick();
        }

        // ---- ABANDONED (mutually exclusive) ----
        [SettingsUISection(kActionsTab, kAbandonedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableAbandonmentChecked))]
        public bool RemoveAbandoned { get; set; } = false;

        [SettingsUISection(kActionsTab, kAbandonedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment { get; set; } = true;

        // One-time "Restore Abandoned Now"
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kAbandonedGroup)]
        public bool RestoreAbandonedNow
        {
            set
            {
                m_RequestRestoreAbandonedNow = true;
            }
        }

        // ---- COLLAPSED ----
        [SettingsUISection(kActionsTab, kCollapsedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCollapsedChecked))]
        public bool RemoveCollapsed { get; set; } = false;

        [SettingsUISection(kActionsTab, kCollapsedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCollapsedChecked))]
        public bool DisableCollapsed { get; set; } = false;

        // ---- CONDEMNED (mutually exclusive) ----
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned { get; set; } = false;

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned { get; set; } = false;

        // ---- STATUS (informational) ----
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
            m_ShowRefreshPrompt
                ? kPressRefreshPrompt
                : (string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText);

        // ---- ABOUT TAB ----
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
                catch (Exception ex) { Mod.s_Log.Warn($"Failed to open Paradox: {ex.Message}"); }
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
                catch (Exception ex) { Mod.s_Log.Warn($"Failed to open Discord: {ex.Message}"); }
            }
        }

        // ---- HELPERS (mutual exclusion) ----
        public bool IsRemoveAbandonedChecked() => RemoveAbandoned;
        public bool IsDisableAbandonmentChecked() => DisableAbandonment;

        public bool IsRemoveCollapsedChecked() => RemoveCollapsed;
        public bool IsDisableCollapsedChecked() => DisableCollapsed;

        public bool IsRemoveCondemnedChecked() => RemoveCondemned;
        public bool IsDisableCondemnedChecked() => DisableCondemned;

        // ---- SYSTEM HANDOFF ----
        public bool TryConsumeRefreshRequest()
        {
            if (!m_RequestRefresh)
                return false;
            m_RequestRefresh = false;
            return true;
        }

        public bool TryConsumeRestoreAbandonedNowRequest()
        {
            if (!m_RequestRestoreAbandonedNow)
                return false;
            m_RequestRestoreAbandonedNow = false;
            return true;
        }

        // Sets visible status text; stamps last-updated when countedNow == true
        public void SetStatus(string text, bool countedNow)
        {
            if (string.IsNullOrEmpty(text))
                text = "No city loaded";

            if (countedNow)
            {
                m_LastCountTime = DateTime.Now;
                m_StatusText = $"{text}  —  Last updated {m_LastCountTime:HH:mm}";
            }
            else
            {
                m_StatusText = (m_LastCountTime == DateTime.MinValue)
                    ? text
                    : $"{text}  (stale — Click Refresh)";
            }

            m_SuppressReapply = true;   // UI-only refresh
            base.Apply();
            m_SuppressReapply = false;
        }

        // ---- Prompt helper (combined) ----
        public void SetRefreshPrompt(bool show)
        {
            if (m_ShowRefreshPrompt == show)
                return;

            m_ShowRefreshPrompt = show;
            m_SuppressReapply = true;   // UI-only refresh
            base.Apply();
            m_SuppressReapply = false;
        }
    }
}
