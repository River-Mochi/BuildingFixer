// Setting.cs
// Purpose: Options UI for Abandoned Building Boss [ABB].

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase; // [FileLocation]
    using Game.Modding;
    using Game.Settings;
    using UnityEngine;

    [FileLocation("ModsSettings/ABB/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kButtonsGroup, kStatusGroup, kAboutInfoGroup, kAboutLinksGroup)]
    [SettingsUIShowGroupName(kStatusGroup, kAboutInfoGroup)]
    public sealed class Setting : ModSetting
    {
        // Tabs
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // Groups
        public const string kActionsGroup = "Actions";
        public const string kButtonsGroup = "Buttons";
        public const string kStatusGroup = "Status";
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // ====== OPTIONS (saved) =====================================

        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool AutoDemolishAbandoned { get; set; } = true;

        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool AutoDemolishCondemned { get; set; } = false;

        // ====== STATUS BACKING ======================================

        private string m_StatusText = "Idle";

        // UI -> system flags (never saved)
        private bool m_RequestRefreshCount;
        private bool m_RequestRemodelAbandoned;
        private bool m_RequestRemodelCondemned;

        public Setting(IMod mod)
            : base(mod)
        {
        }

        public override void SetDefaults()
        {
            AutoDemolishAbandoned = true;
            AutoDemolishCondemned = false;

            m_StatusText = "Idle";

            m_RequestRefreshCount = false;
            m_RequestRemodelAbandoned = false;
            m_RequestRemodelCondemned = false;
        }

        // ====== BUTTONS ROW (same group -> same row) ================

        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RemodelAbandonedNow
        {
            set
            {
                m_RequestRemodelAbandoned = true;
            }
        }

        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RemodelCondemnedNow
        {
            set
            {
                m_RequestRemodelCondemned = true;
            }
        }

        // ====== STATUS GROUP ========================================

        // Refresh button (manual recalc)
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kStatusGroup)]
        public bool RefreshCount
        {
            set
            {
                m_RequestRefreshCount = true;
            }
        }

        // Read-only display string: "Abandoned: X | Condemned: Y" or "Idle"
        [SettingsUISection(kActionsTab, kStatusGroup)]
        public string AbandonedStatus => m_StatusText;

        // ====== ABOUT TAB ===========================================

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModName => Mod.ModName;

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModVersion => Mod.ModVersion;

        [SettingsUIButtonGroup(kAboutLinksGroup)]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenMods
        {
            set
            {
                try
                {
                    Application.OpenURL("https://mods.paradoxplaza.com/authors/kimosabe1?orderBy=desc&sortBy=best&time=alltime");
                }
                catch (Exception)
                {
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
                try
                {
                    Application.OpenURL("https://discord.gg/HTav7ARPs2");
                }
                catch (Exception)
                {
                }
            }
        }

        // ====== System-facing helpers ===============================

        public bool TryConsumeRefreshCountRequest()
        {
            if (!m_RequestRefreshCount)
                return false;

            m_RequestRefreshCount = false;
            return true;
        }

        public bool TryConsumeRemodelAbandonedRequest()
        {
            if (!m_RequestRemodelAbandoned)
                return false;

            m_RequestRemodelAbandoned = false;
            return true;
        }

        public bool TryConsumeRemodelCondemnedRequest()
        {
            if (!m_RequestRemodelCondemned)
                return false;

            m_RequestRemodelCondemned = false;
            return true;
        }

        public void SetStatus(string text)
        {
            m_StatusText = text ?? string.Empty;
            Apply(); // UI only, don't spam disk
        }
    }
}
