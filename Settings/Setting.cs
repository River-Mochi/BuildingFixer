// Setting.cs
// Purpose: Options UI — Actions | RESCUE | About; status + requests; Recommended button; Empty-buildings bonus.

namespace BuildingFixer
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using Unity.Entities;
    using UnityEngine;

    [FileLocation("ModsSettings/BuildingFixer/BuildingFixer")]
    [SettingsUITabOrder(kActionsTab, kRescueTab, kAboutTab)]
    [SettingsUIGroupOrder(
        kRecommendedGroup,
        kAutoRemovalGroup,
        kAutoRestoreGroup,
        kCondemnedGroup,
        kRescueNowGroup,
        kEmptyBuildingsGroup,
        kAboutInfoGroup,
        kAboutLinksGroup)]
    [SettingsUIShowGroupName(
        kRecommendedGroup,
        kAutoRemovalGroup,
        kAutoRestoreGroup,
        kCondemnedGroup,
        kRescueNowGroup,
        kEmptyBuildingsGroup,
        kAboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // Tabs
        public const string kActionsTab = "Actions";
        public const string kRescueTab = "RESCUE";
        public const string kAboutTab = "About";

        // Groups (Actions)
        public const string kRecommendedGroup = "RECOMMENDED";
        public const string kAutoRemovalGroup = "AUTO REMOVAL";
        public const string kAutoRestoreGroup = "AUTO RESTORE — No Demolish";
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";

        // Groups (RESCUE)
        public const string kRescueNowGroup = "RESCUE NOW";
        public const string kEmptyBuildingsGroup = "EMPTY BUILDINGS";

        // Groups (About)
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
        private bool m_RequestCleanupEmptyNow;

        private bool m_ShowRefreshPrompt;
        private const string kPressRefreshPrompt = "Click Refresh Status to update";

        private bool m_SuppressReapply;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            // Core
            RemoveAbandoned = true;
            RemoveCollapsed = false;

            DisableAbandonment = false;
            DisableCollapsed = false;

            DisableCondemned = false;
            RemoveCondemned = false;

            // Bonus (Empty)
            CleanCommercialEmpty = true;
            CleanIndustrialEmpty = false;

            m_StatusText = "No city loaded";
            m_LastCountTime = DateTime.MinValue;
            m_RequestRefresh = false;
            m_RequestRescueAllNow = false;
            m_RequestCleanupEmptyNow = false;
            m_ShowRefreshPrompt = false;
        }

        public override void Apply()
        {
            base.Apply();

            if (m_SuppressReapply)
                return;

            bool inGame = GameManager.instance != null && GameManager.instance.gameMode == GameMode.Game;
            if (!inGame)
            {
                SetStatus("No city loaded", countedNow: false);
                m_RequestRefresh = false;
                return;
            }

            SetRefreshPrompt(true);
            m_RequestRefresh = true;

            World world = World.DefaultGameObjectInjectionWorld;
            world?.GetExistingSystemManaged<BuildingFixerSystem>()?.RequestRunNextTick();
        }

        // ===== Actions tab =====

        // RECOMMENDED (one-click)
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kRecommendedGroup)]
        public bool RecommendedNow
        {
            set
            {
                if (!value)
                    return;

                // Sensible defaults
                RemoveAbandoned = true;
                RemoveCollapsed = true;

                DisableAbandonment = false;
                DisableCollapsed = false;

                DisableCondemned = true;
                RemoveCondemned = false;

                // Re-apply without scheduling more changes
                m_SuppressReapply = true;
                base.Apply();
                m_SuppressReapply = false;

                SetRefreshPrompt(true);
            }
        }

        // ---- AUTO REMOVAL ----
        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableAbandonmentChecked))]
        public bool RemoveAbandoned
        {
            get; set;
        }

        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCollapsedChecked))]
        public bool RemoveCollapsed
        {
            get; set;
        }

        // ---- AUTO RESTORE — No Demolish ----
        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment
        {
            get; set;
        }

        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCollapsedChecked))]
        public bool DisableCollapsed
        {
            get; set;
        }

        // ---- CONDEMNED ----
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned
        {
            get; set;
        }

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned
        {
            get; set;
        }

        // ===== RESCUE tab =====

        // Deep rescue button
        [SettingsUIButton]
        [SettingsUISection(kRescueTab, kRescueNowGroup)]
        public bool RescueAllNow
        {
            set
            {
                if (value)
                    m_RequestRescueAllNow = true;
            }
        }

        // Empty-buildings toggles + actions
        [SettingsUISection(kRescueTab, kEmptyBuildingsGroup)]
        public bool CleanCommercialEmpty
        {
            get; set;
        }

        [SettingsUISection(kRescueTab, kEmptyBuildingsGroup)]
        public bool CleanIndustrialEmpty
        {
            get; set;
        }

        [SettingsUIButton]
        [SettingsUISection(kRescueTab, kEmptyBuildingsGroup)]
        public bool RefreshStatus
        {
            set
            {
                if (value)
                    m_RequestRefresh = true;
            }
        }

        [SettingsUISection(kRescueTab, kEmptyBuildingsGroup)]
        public string Status =>
            m_ShowRefreshPrompt ? kPressRefreshPrompt :
            (string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText);

        [SettingsUIButton]
        [SettingsUISection(kRescueTab, kEmptyBuildingsGroup)]
        public bool CleanupEmptyNow
        {
            set
            {
                if (value)
                    m_RequestCleanupEmptyNow = true;
            }
        }

        // ===== About =====

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

        public bool TryConsumeCleanupEmptyNowRequest()
        {
            if (!m_RequestCleanupEmptyNow)
                return false;
            m_RequestCleanupEmptyNow = false;
            return true;
        }

        // Status setters
        public void SetStatus(string text, bool countedNow)
        {
            var display = string.IsNullOrEmpty(text) ? "No city loaded" : text;

            if (countedNow)
            {
                m_LastCountTime = DateTime.Now;
                m_StatusText = $"{display}  —  Last updated {m_LastCountTime:HH:mm}";
                SetRefreshPrompt(false);
            }
            else
            {
                m_StatusText = (m_LastCountTime == DateTime.MinValue)
                    ? display
                    : $"{display}  (stale — Click Refresh Status)";
            }

            m_SuppressReapply = true;
            base.Apply();
            m_SuppressReapply = false;
        }

        public void SetRefreshPrompt(bool show)
        {
            if (m_ShowRefreshPrompt == show)
                return;
            m_ShowRefreshPrompt = show;

            m_SuppressReapply = true;
            base.Apply();
            m_SuppressReapply = false;
        }
    }
}
