// Setting.cs
// Purpose: Options UI — Actions | About; status + requests; Recommended + Reset Defaults + Restore Abandoned + Smart Access Nudge buttons.

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
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(
        kRecommendedGroup,
        kAutoRemovalGroup,
        kAutoRestoreGroup,
        kCondemnedGroup,
        kStatusGroup,
        kAboutInfoGroup,
        kAboutLinksGroup)]
    [SettingsUIShowGroupName(
        kRecommendedGroup,
        kAutoRemovalGroup,
        kAutoRestoreGroup,
        kCondemnedGroup,
        kStatusGroup,
        kAboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // Tabs
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // Groups (Actions)
        public const string kRecommendedGroup = "RECOMMENDED";
        public const string kAutoRemovalGroup = "AUTO REMOVAL";
        public const string kAutoRestoreGroup = "AUTO RESTORE — No Demolish";
        public const string kCondemnedGroup = "CONDEMNED BUILDINGS";
        public const string kStatusGroup = "STATUS";

        // Groups (About)
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // Links
        private const string kUrlDiscord = "https://discord.gg/HTav7ARPs2";

        private const string kUrlParadox =
            "https://mods.paradoxplaza.com/authors/kimosabe1/cities_skylines_2?games=cities_skylines_2&orderBy=desc&sortBy=best&time=alltime";

        // Backing
        private string m_StatusText = "No city loaded";
        private DateTime m_LastCountTime = DateTime.MinValue;

        private bool m_RequestRefresh;
        private bool m_RequestGlobalNudge;
        private bool m_RequestRestoreAbandonedOnce;
        private bool m_RequestSmartAccessNudge;

        private bool m_ShowRefreshPrompt;
        private const string kPressRefreshPrompt = "Click Refresh Status to update";

        private bool m_SuppressReapply;

        public Setting(IMod mod)
            : base(mod)
        {
        }

        public override void SetDefaults()
        {
            // Core
            RemoveAbandoned = true;
            RemoveCollapsed = false;

            DisableAbandonment = false;
            DisableCollapsed = false;

            DisableCondemned = false;
            RemoveCondemned = false;

            m_StatusText = "No city loaded";
            m_LastCountTime = DateTime.MinValue;
            m_RequestRefresh = false;
            m_RequestGlobalNudge = false;
            m_RequestRestoreAbandonedOnce = false;
            m_RequestSmartAccessNudge = false;
            m_ShowRefreshPrompt = false;
        }

        public override void Apply()
        {
            base.Apply();

            if (m_SuppressReapply)
            {
                return;
            }

            GameManager? gm = GameManager.instance;
            bool inGame = gm != null && gm.gameMode == GameMode.Game;

            if (!inGame)
            {
                SetStatus("No city loaded", countedNow: false);
                m_RequestRefresh = false;
                m_RequestGlobalNudge = false;
                m_RequestRestoreAbandonedOnce = false;
                m_RequestSmartAccessNudge = false;
                return;
            }

            // Options changed in-game:
            //  - Mark status as stale, but keep the last numbers visible.
            //  - Allow the next Refresh to trigger a new scan.
            SetRefreshPrompt(true);

            // Ensure the runtime system wakes up at least once.
            WakeBuildingFixerSystemIfInGame();
        }

        // ===== Actions tab =====

        // RECOMMENDED (one-click)
        [SettingsUIButtonGroup(kRecommendedGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kRecommendedGroup)]
        public bool RecommendedNow
        {
            set
            {
                if (!value)
                {
                    return;
                }

                // Sensible defaults
                RemoveAbandoned = true;
                RemoveCollapsed = true;

                DisableAbandonment = false;
                DisableCollapsed = false;

                DisableCondemned = true;
                RemoveCondemned = false;

                // Re-apply without scheduling changes twice.
                m_SuppressReapply = true;
                base.Apply();
                m_SuppressReapply = false;

                SetRefreshPrompt(true);
            }
        }

        // Reset all toggles to vanilla (no BF effect).
        [SettingsUIButtonGroup(kRecommendedGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kRecommendedGroup)]
        public bool ResetToGameDefaults
        {
            set
            {
                if (!value)
                {
                    return;
                }

                RemoveAbandoned = false;
                RemoveCollapsed = false;

                DisableAbandonment = false;
                DisableCollapsed = false;

                DisableCondemned = false;
                RemoveCondemned = false;

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
            get;
            set;
        }

        [SettingsUISection(kActionsTab, kAutoRemovalGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCollapsedChecked))]
        public bool RemoveCollapsed
        {
            get;
            set;
        }

        // ---- AUTO RESTORE — No Demolish ----
        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveAbandonedChecked))]
        public bool DisableAbandonment
        {
            get;
            set;
        }

        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCollapsedChecked))]
        public bool DisableCollapsed
        {
            get;
            set;
        }

        // One-shot heavy sweep: restore all existing Abandoned stock.
        [SettingsUIButtonGroup(kAutoRestoreGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kAutoRestoreGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsNoCityLoaded))]
        public bool RestoreExistingAbandonedNow
        {
            set
            {
                if (!value)
                {
                    return;
                }

                m_RequestRestoreAbandonedOnce = true;
                WakeBuildingFixerSystemIfInGame();
            }
        }

        // ---- CONDEMNED ----
        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsRemoveCondemnedChecked))]
        public bool DisableCondemned
        {
            get;
            set;
        }

        [SettingsUISection(kActionsTab, kCondemnedGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsDisableCondemnedChecked))]
        public bool RemoveCondemned
        {
            get;
            set;
        }

        // ---- STATUS ----
        [SettingsUIButtonGroup(kStatusGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kStatusGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsNoCityLoaded))]
        public bool RefreshStatus
        {
            set
            {
                if (!value)
                {
                    return;
                }

                m_RequestRefresh = true;
                WakeBuildingFixerSystemIfInGame();
            }
        }

        // One-shot global nudge + icon clear
        [SettingsUIButtonGroup(kStatusGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kStatusGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsNoCityLoaded))]
        public bool NudgeAndClearProblemIconsOnce
        {
            set
            {
                if (!value)
                {
                    return;
                }

                m_RequestGlobalNudge = true;
                WakeBuildingFixerSystemIfInGame();
            }
        }

        // One-shot NoPed/NoCar smart nudge
        [SettingsUIButtonGroup(kStatusGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kStatusGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(IsNoCityLoaded))]
        public bool SmartAccessNudgeOnce
        {
            set
            {
                if (!value)
                {
                    return;
                }

                m_RequestSmartAccessNudge = true;
                WakeBuildingFixerSystemIfInGame();
            }
        }

        [SettingsUISection(kActionsTab, kStatusGroup)]
        public string Status =>
            m_ShowRefreshPrompt
                ? (string.IsNullOrEmpty(m_StatusText)
                    ? kPressRefreshPrompt
                    : $"{m_StatusText} (stale)")
                : (string.IsNullOrEmpty(m_StatusText) ? "No city loaded" : m_StatusText);

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
                {
                    TryOpen(kUrlParadox);
                }
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
                {
                    TryOpen(kUrlDiscord);
                }
            }
        }

        private static void TryOpen(string url)
        {
            try
            {
                Application.OpenURL(url);
            }
            catch (Exception ex)
            {
                Mod.s_Log.Warn($"Open URL failed: {ex.Message}");
            }
        }

        // Mutual exclusion helpers
        public bool IsRemoveAbandonedChecked() => RemoveAbandoned;
        public bool IsDisableAbandonmentChecked() => DisableAbandonment;
        public bool IsRemoveCollapsedChecked() => RemoveCollapsed;
        public bool IsDisableCollapsedChecked() => DisableCollapsed;
        public bool IsRemoveCondemnedChecked() => RemoveCondemned;
        public bool IsDisableCondemnedChecked() => DisableCondemned;

        // Returns true when there is *no* city loaded.
        // Grey out buttons that require a live city (Restore / Refresh / Nudge).
        public bool IsNoCityLoaded()
        {
            GameManager gm = GameManager.instance;
            // When a real city is loaded, gameMode == Game even in Options menu.
            return gm == null || gm.gameMode != GameMode.Game;
        }

        // Wakes the BuildingFixerSystem once, but only when a city is loaded.
        private static void WakeBuildingFixerSystemIfInGame()

        {
            GameManager? gm = GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game)
            {
                return;
            }

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return;
            }

            BuildingFixerSystem? system =
                world.GetExistingSystemManaged<BuildingFixerSystem>();

            system?.RequestRunNextTick();
        }

        // System handoff
        public bool TryConsumeRefreshRequest()
        {
            if (!m_RequestRefresh)
            {
                return false;
            }

            m_RequestRefresh = false;
            return true;
        }

        public bool TryConsumeGlobalNudgeRequest()
        {
            if (!m_RequestGlobalNudge)
            {
                return false;
            }

            m_RequestGlobalNudge = false;
            return true;
        }

        public bool TryConsumeRestoreAbandonedOnce()
        {
            if (!m_RequestRestoreAbandonedOnce)
            {
                return false;
            }

            m_RequestRestoreAbandonedOnce = false;
            return true;
        }

        public bool TryConsumeSmartAccessNudgeRequest()
        {
            if (!m_RequestSmartAccessNudge)
            {
                return false;
            }

            m_RequestSmartAccessNudge = false;
            return true;
        }

        // Status setters
        public void SetStatus(string text, bool countedNow)
        {
            var display = string.IsNullOrEmpty(text) ? "No city loaded" : text;

            if (countedNow)
            {
                m_LastCountTime = DateTime.Now;
                m_StatusText = $"{display} — Updated {m_LastCountTime:HH:mm}";
                SetRefreshPrompt(false);
            }
            else
            {
                if (m_LastCountTime == DateTime.MinValue)
                {
                    m_StatusText = $"{display} (click Refresh Status)";
                }
                else
                {
                    m_StatusText = $"{display} — Updated {m_LastCountTime:HH:mm}";
                }
            }

            m_SuppressReapply = true;
            base.Apply();
            m_SuppressReapply = false;
        }

        public void SetRefreshPrompt(bool show)
        {
            if (m_ShowRefreshPrompt == show)
            {
                return;
            }

            m_ShowRefreshPrompt = show;

            m_SuppressReapply = true;
            base.Apply();
            m_SuppressReapply = false;
        }
    }
}
