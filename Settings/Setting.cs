// Setting.cs
// Purpose: Options UI for ABB — dependable toggles + status + refresh; Apply() nudges system.
// Fixes: Status now uses [SettingsUIText] (reliable) and Apply-loop is guarded by m_SuppressReapply.

namespace AbandonedBuildingBoss
{
    using System;                       // Exception handling (try/catch)
    using Colossal.IO.AssetDatabase;    // [FileLocation]
    using Game.Modding;                 // IMod, ModSetting
    using Game.Settings;                // SettingsUI
    using Unity.Entities;
    using UnityEngine;                  // Application.OpenURL

    [FileLocation("ModsSettings/AbandonedBuildingBoss/ABB")]    // Saved settings path
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(
        kAbandonedGroup, kCondemnedGroup, kStatusGroup,
        kAboutInfoGroup, kAboutLinksGroup
    )]
    [SettingsUIShowGroupName(
        kAbandonedGroup,
        kCondemnedGroup,
        kStatusGroup,
        kAboutInfoGroup
        )]
    public sealed class Setting : ModSetting
    {
        // ---- Tabs ----
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // ---- Tab Groups ----
        public const string kAbandonedGroup = "ABANDONED BUILDINGS";
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";
        public const string kStatusGroup = "STATUS";
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // ---- External Links ----
        private const string UrlDiscord = "https://discord.gg/HTav7ARPs2";
        private const string UrlParadox = "https://mods.paradoxplaza.com/uploaded?orderBy=desc&sortBy=popularity";

        // ---- Backing fields ----
        private string m_StatusText = "No city loaded";
        private DateTime m_LastCountTime = DateTime.MinValue;
        private bool m_RequestRefresh;

        // Guard to avoid scheduling runs when we re-Apply just to refresh UI text.
        private bool m_SuppressReapply;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            RemoveAbandoned = false;
            DisableAbandonment = true;  // sensible default: keep buildings alive
            DisableCondemned = false;
            RemoveCondemned = false;

            m_StatusText = "No city loaded";
            m_LastCountTime = DateTime.MinValue;
            m_RequestRefresh = false;
        }

        // Apply nudges the system to re-run next tick so changes take effect immediately.
        public override void Apply()
        {
            base.Apply();

            if (m_SuppressReapply)
                return;

            // Ask for immediate recount/run (same as pressing Refresh).
            m_RequestRefresh = true;

            var world = World.DefaultGameObjectInjectionWorld;
            var sys = world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
            sys?.RequestRunNextTick();
        }

        // ---- Abandoned toggles (mutually exclusive)
        [SettingsUISection(kActionsTab, kAbandonedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableAbandonmentChecked))]
        public bool RemoveAbandoned { get; set; } = false;

        [SettingsUISection(kActionsTab, kAbandonedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment { get; set; } = true;

        // ---- Condemned toggles (mutually exclusive)
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned { get; set; } = false;

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned { get; set; } = false;

        // ---- Status (informational)
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
        [SettingsUITextInput] // Reliable in current CS2; wraps long strings fine.
        public string Status => string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText;

        // ---- About
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
                    Application.OpenURL(UrlParadox);
                }
                catch (Exception ex) { Mod.Log.Warn($"Failed to open Paradox: {ex.Message}"); }
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
                    Application.OpenURL(UrlDiscord);
                }
                catch (Exception ex) { Mod.Log.Warn($"Failed to open Discord: {ex.Message}"); }
            }
        }

        // ---- Mutual exclusion helpers
        public bool IsRemoveAbandonedChecked() => RemoveAbandoned;
        public bool IsDisableAbandonmentChecked() => DisableAbandonment;
        public bool IsRemoveCondemnedChecked() => RemoveCondemned;
        public bool IsDisableCondemnedChecked() => DisableCondemned;

        // ---- System handoff
        public bool TryConsumeRefreshRequest()
        {
            if (!m_RequestRefresh)
                return false;
            m_RequestRefresh = false;
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
                    : $"{text}  (stale — press Refresh)";
            }

            // Refresh UI without triggering another system pass.
            m_SuppressReapply = true;
            base.Apply();
            m_SuppressReapply = false;
        }
    }
}
