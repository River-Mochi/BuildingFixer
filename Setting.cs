// File: Setting.cs
// Purpose: Options UI for ABB – 2 tabs, 1 dropdown, 1 toggle, 2 buttons on same row, status line.

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Widgets;
    using UnityEngine; // Application.OpenURL

    // Order is important; this follows the CO / AssetIconLibrary pattern.
    [FileLocation("ModsSettings/ABB/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kButtonsGroup, kStatusGroup, kAboutInfoGroup, kAboutLinksGroup)]
    // Do NOT show "Actions" header; Status + About Info still show their group names.
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

        // Backing fields
        // Default = AutoDemolish so behavior matches original mod.
        private AbandonedHandlingMode m_Behavior = AbandonedHandlingMode.AutoDemolish;
        private bool m_AlsoClearCondemned = true;
        private string m_StatusText = "Idle";

        // UI -> system flags (never saved)
        private bool m_RequestCount;
        private bool m_RequestClear;

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            m_Behavior = AbandonedHandlingMode.AutoDemolish; // original behavior
            m_AlsoClearCondemned = true;
            m_StatusText = "Idle";
            m_RequestCount = false;
            m_RequestClear = false;
        }

        // ====== ACTIONS TAB ======

        // 1) Dropdown – implemented exactly like Asset Icon Library:
        //    [SettingsUIDropdown(typeof(Setting), nameof(GetBehaviorDropdownItems))]
        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetBehaviorDropdownItems))]
        public AbandonedHandlingMode Behavior
        {
            get => m_Behavior;
            set
            {
                if (m_Behavior == value)
                    return;

                m_Behavior = value;
                ApplyAndSave();
            }
        }

        // 2) Toggle in the SAME group
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool AlsoClearCondemned
        {
            get => m_AlsoClearCondemned;
            set
            {
                if (m_AlsoClearCondemned == value)
                    return;

                m_AlsoClearCondemned = value;
                ApplyAndSave();
            }
        }

        // ====== BUTTONS ROW (same group → same row) ======

        // Left button: Count abandoned
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool CountAbandoned
        {
            set
            {
                // Options UI thread: just flag; ECS system will consume it.
                m_RequestCount = true;
            }
        }

        // Right button: Restore buildings (was ClearNow)
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RestoreBuildings
        {
            set
            {
                m_RequestClear = true;
            }
        }

        // ====== STATUS LINE ======
        [SettingsUISection(kActionsTab, kStatusGroup)]
        public string AbandonedStatus => m_StatusText;

        // ====== ABOUT TAB ======

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModName => Mod.ModName;

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModVersion => Mod.ModVersion;

        // 2 buttons on same row in About
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
                    Application.OpenURL("https://discord.gg/HTav7ARPs2");
                }
                catch (Exception) { }
            }
        }

        // ====== called by the system (simulation thread) ======

        // system asks: “did player press Count?”
        public bool TryConsumeCountRequest()
        {
            if (!m_RequestCount)
                return false;

            m_RequestCount = false;
            return true;
        }

        // system asks: “did player press Restore?”
        public bool TryConsumeClearRequest()
        {
            if (!m_RequestClear)
                return false;

            m_RequestClear = false;
            return true;
        }

        // system updates the status line
        public void SetStatus(string text)
        {
            m_StatusText = text ?? string.Empty;
            Apply();            // do NOT save to disk each frame
        }

        public bool GetAlsoClearCondemned() => m_AlsoClearCondemned;

        // ====== dropdown items – CO / AssetIconLibrary style ======
        // value = enum, displayName = GetOptionLabelLocaleID("Key") with LocaleEN entries.
        public DropdownItem<AbandonedHandlingMode>[] GetBehaviorDropdownItems()
        {
            return new[]
            {
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.AutoDemolish,
                    displayName = GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.AutoDemolish)),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.DisableAbandonment,
                    displayName = GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.DisableAbandonment)),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.None,
                    displayName = GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.None)),
                },
            };
        }

        // ====== enum – system reads this via Setting.Behavior ======
        public enum AbandonedHandlingMode
        {
            // Keep order in sync with dropdown items (value doesn’t matter much, but cleaner).
            AutoDemolish = 0, // default – like original mod
            DisableAbandonment = 1, // restore/keep buildings
            None = 2, // do nothing
        }
    }
}
