// Setting.cs
// Purpose: Options UI — layout + status; schedules runs; RESCUE ALL button.
// Notes: Status group is now last; RESCUE is right above it.

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;     // [FileLocation]
    using Game;
    using Game.Modding;                  // IMod, ModSetting
    using Game.SceneFlow;                // GameManager, GameMode
    using Game.Settings;                 // Settings UI
    using Unity.Entities;                // World.DefaultGameObjectInjectionWorld
    using UnityEngine;                   // Application.OpenURL

    [FileLocation("ModsSettings/AbandonedBuildingBoss/ABB")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    // ↓↓↓ Group order changed: RESCUE before STATUS, so STATUS renders at the very bottom
    [SettingsUIGroupOrder(
        kAutoRemovalGroup, kAutoRestoreGroup, kCondemnedGroup, kRescueGroup, kStatusGroup,
        kAboutInfoGroup, kAboutLinksGroup)]
    [SettingsUIShowGroupName(kAutoRemovalGroup, kAutoRestoreGroup, kCondemnedGroup, kRescueGroup, kStatusGroup, kAboutInfoGroup)]
    public sealed class Setting : ModSetting
    {
        // Tabs
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // Action groups
        public const string kAutoRemovalGroup = "AUTO REMOVAL";
        public const string kAutoRestoreGroup = "AUTO RESTORE — No Demolish";
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";
        public const string kRescueGroup = "RESCUE";
        public const string kStatusGroup = "STATUS";     // last

        // About
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // Links
        private const string kUrlDiscord = "https://discord.gg/HTav7ARPs2";
        private const string kUrlParadox = "https://mods.paradoxplaza.com/uploaded?orderBy=desc&sortBy=popularity";

        // Backing
        private string m_StatusText = "No city loaded";
        private DateTime m_LastCountTime = DateTime.MinValue;
        private bool m_RequestRefresh;
        private bool m_RequestRescueAllNow;

        private bool m_ShowRefreshPrompt;
        private const string kPressRefreshPrompt = "Click Refresh for latest counts";

        private bool m_SuppressReapply;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            RemoveAbandoned = true;   // quick win
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

        public override void Apply()
        {
            base.Apply();

            if (m_SuppressReapply)
                return;

            // Don’t tease a refresh while not in-game
            bool inGame = GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game;
            if (!inGame)
            {
                SetStatus("No city loaded", countedNow: false);
                m_RequestRefresh = false;
                return;
            }

            // In-game → schedule immediate recount
            SetRefreshPrompt(true);     // hint until a real count runs
            m_RequestRefresh = true;

            var world = World.DefaultGameObjectInjectionWorld;
            var sys = world?.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
            sys?.RequestRunNextTick();
        }

        // ---- AUTO REMOVAL ----
        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableAbandonmentChecked))]
        public bool RemoveAbandoned { get; set; } = false;

        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCollapsedChecked))]
        public bool RemoveCollapsed { get; set; } = false;

        // ---- AUTO RESTORE — No Demolish ----
        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment { get; set; } = true;

        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCollapsedChecked))]
        public bool DisableCollapsed { get; set; } = false;

        // ---- CONDEMNED ----
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned { get; set; } = false;

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned { get; set; } = false;

        // ---- RESCUE (above Status) ----
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kRescueGroup)]
        public bool RescueAllNow
        {
            set
            {
                m_RequestRescueAllNow = true;
            }
        }

        // ---- STATUS (very bottom) ----
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
            m_ShowRefreshPrompt ? kPressRefreshPrompt :
            (string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText);

        // About
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
                if (value)
                    TryOpen(kUrlParadox);
            }
        }

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenDiscord
        {
            set
            {
                if (value)
                    TryOpen(kUrlDiscord);
            }
        }

        private static void TryOpen(string url)
        {
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception ex) { Mod.s_Log.Warn($"Open URL failed: {ex.Message}"); }
        }

        // Mutual exclusion helpers
        public bool IsRemoveAbandonedChecked() => RemoveAbandoned;
        public bool IsDisableAbandonmentChecked() => DisableAbandonment;

        public bool IsRemoveCollapsedChecked() => RemoveCollapsed;
        public bool IsDisableCollapsedChecked() => DisableCollapsed;

        public bool IsRemoveCondemnedChecked() => RemoveCondemned;
        public bool IsDisableCondemnedChecked() => DisableCondemned;

        // System handoff
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

        // Status text setter
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
                m_StatusText = (m_LastCountTime == DateTime.MinValue)
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
