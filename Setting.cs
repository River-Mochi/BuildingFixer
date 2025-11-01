// File: Setting.cs
// Purpose: Options UI for [ABB] Abandoned Building Boss.

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Widgets;
    using UnityEngine;

    public enum AbandonedHandlingMode
    {
        None = 0,
        AutoDemolish = 1,
        DisableAbandonment = 2,
    }

    [FileLocation("ModsSettings/AbandonedBuildingBoss/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kAboutInfoGroup, kAboutLinksGroup)]

    // [SettingsUIShowGroupName(kActionsGroup)]     // do NOT show group name for the actions group
    [SettingsUIShowGroupName(kAboutInfoGroup, kAboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // tabs
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // groups
        public const string kActionsGroup = "Actions";
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        private const string UrlMods = "TBD";
        private const string UrlDiscord = "https://discord.gg/HTav7ARPs2";

        // backing
        private string m_Behavior = "None";
        public bool AlsoClearCondemned { get; set; } = false;
        private bool m_ClearRequested;
        private bool m_CountRequested;
        private string m_AbandonedCountDisplay = "Idle";

        public Setting(IMod mod) : base(mod) { }

        // ===== ACTIONS TAB =====

        [SettingsUISection(kActionsTab, kActionsGroup)]
        public string AbandonedCount => m_AbandonedCountDisplay;

        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool CountAbandoned
        {
            set
            {
                if (!value)
                    return;
                m_CountRequested = true;
                m_AbandonedCountDisplay = "Counting...";
                Apply();
            }
        }

        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetBehaviorDropdownItems))]
        public string Behavior
        {
            get => m_Behavior;
            set
            {
                m_Behavior = value;
                ApplyAndSave();
            }
        }

        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool AlsoClearCondemnedToggle
        {
            get => AlsoClearCondemned;
            set
            {
                AlsoClearCondemned = value;
                ApplyAndSave();
            }
        }

        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool ClearNow
        {
            set
            {
                if (!value)
                    return;
                m_ClearRequested = true;
                ApplyAndSave();
            }
        }

        // ===== ABOUT TAB =====

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
                    Application.OpenURL(UrlMods);
                }
                catch (Exception) { }
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
                    Application.OpenURL(UrlDiscord);
                }
                catch (Exception) { }
            }
        }

        // ===== dropdown items (plain strings) =====
        public DropdownItem<string>[] GetBehaviorDropdownItems()
        {
            return new[]
            {
                new DropdownItem<string> { value = "None",               displayName = "Do nothing" },
                new DropdownItem<string> { value = "AutoDemolish",       displayName = "Auto-demolish abandoned" },
                new DropdownItem<string> { value = "DisableAbandonment", displayName = "Clear abandoned flag (keep building)" },
            };
        }

        // ===== consumed by system =====
        public bool TryConsumeClearRequest()
        {
            if (!m_ClearRequested)
                return false;
            m_ClearRequested = false;
            return true;
        }

        public bool TryConsumeCountRequest()
        {
            if (!m_CountRequested)
                return false;
            m_CountRequested = false;
            return true;
        }

        public void SetAbandonedCount(int count)
        {
            m_AbandonedCountDisplay = (count < 0) ? "No city loaded" : $"{count} abandoned";
            Apply();
        }

        public AbandonedHandlingMode BehaviorMode =>
            m_Behavior switch
            {
                "AutoDemolish" => AbandonedHandlingMode.AutoDemolish,
                "DisableAbandonment" => AbandonedHandlingMode.DisableAbandonment,
                _ => AbandonedHandlingMode.None,
            };

        public override void SetDefaults()
        {
            m_Behavior = "None";
            AlsoClearCondemned = false;
            m_ClearRequested = false;
            m_CountRequested = false;
            m_AbandonedCountDisplay = "Idle";
        }
    }
}
