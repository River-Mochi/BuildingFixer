// File: Setting.cs
// Purpose: Options UI for ABB – 2 tabs, 1 dropdown, 1 toggle, 2 buttons on same row, status line.

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Localization;  // LocalizedString
    using Game.UI.Widgets;       // UI attributes
    using UnityEngine;           // Application.OpenURL

    // Order is important: tabs first, then groups.
    [FileLocation("ModsSettings/ABB/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kButtonsGroup, kStatusGroup, kAboutInfoGroup, kAboutLinksGroup)]
    // Show only Status + About info group headers; hide "Actions" and "Buttons" group headers.
    [SettingsUIShowGroupName(kStatusGroup, kAboutInfoGroup)]
    public sealed class Setting : ModSetting
    {
        // ---- Tabs ----
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // ---- Groups ----
        public const string kActionsGroup = "Actions";
        public const string kButtonsGroup = "Buttons";
        public const string kStatusGroup = "Status";
        public const string kAboutInfoGroup = "Info";
        public const string kAboutLinksGroup = "Links";

        // ---- Backing fields ----
        private AbandonedHandlingMode m_Behavior = AbandonedHandlingMode.AutoDemolish;
        private bool m_AlsoClearCondemned = true;
        private string m_StatusText = "Idle";

        // UI → system flags (not saved)
        private bool m_RequestCount;
        private bool m_RequestClear;   // "restore" request internally

        public Setting(IMod mod) : base(mod) { }

        public override void SetDefaults()
        {
            // Default = original mod behavior: auto-demolish abandoned
            m_Behavior = AbandonedHandlingMode.AutoDemolish;
            m_AlsoClearCondemned = true;
            m_StatusText = "Idle";
            m_RequestCount = false;
            m_RequestClear = false;
        }

        // ============================================================
        // ACTIONS TAB
        // ============================================================

        // 1) Handling behavior dropdown (CO-style, same as AssetIconLibrary)
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

        // 2) Also clear condemned toggle, same group
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

        // ============================================================
        // BUTTONS ROW (same group ⇒ same row)
        // ============================================================

        // Left button: Count abandoned / condemned
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool CountAbandoned
        {
            set
            {
                // Only set a flag; system will consume it on simulation side.
                m_RequestCount = true;
            }
        }

        // Right button: Restore buildings (uses the "clear flags" path)
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RestoreBuildings
        {
            set
            {
                // Internally still treated as "clear" request by the system
                m_RequestClear = true;
            }
        }

        // ============================================================
        // STATUS LINE
        // ============================================================

        [SettingsUISection(kActionsTab, kStatusGroup)]
        public string AbandonedStatus => m_StatusText;

        // ============================================================
        // ABOUT TAB
        // ============================================================

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
                catch (Exception)
                {
                    // swallow; don't crash options UI
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
                    // swallow; don't crash options UI
                }
            }
        }

        // ============================================================
        // Called by the system (simulation thread)
        // ============================================================

        // Did user press Count Abandoned?
        public bool TryConsumeCountRequest()
        {
            if (!m_RequestCount)
                return false;

            m_RequestCount = false;
            return true;
        }

        // Did user press Restore Buildings?
        public bool TryConsumeClearRequest()
        {
            if (!m_RequestClear)
                return false;

            m_RequestClear = false;
            return true;
        }

        // System updates the status line
        public void SetStatus(string text)
        {
            m_StatusText = text ?? string.Empty;
            Apply();    // don't spam disk
        }

        public bool GetAlsoClearCondemned() => m_AlsoClearCondemned;

        // ============================================================
        // Dropdown items – CO template style
        // ============================================================

        public DropdownItem<AbandonedHandlingMode>[] GetBehaviorDropdownItems()
        {
            return new[]
            {
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.None,
                    displayName = LocalizedString.Id(GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.None))),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.AutoDemolish,
                    displayName = LocalizedString.Id(GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.AutoDemolish))),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value       = AbandonedHandlingMode.DisableAbandonment,
                    displayName = LocalizedString.Id(GetOptionLabelLocaleID(nameof(AbandonedHandlingMode.DisableAbandonment))),
                },
            };
        }

        // Enum used by both UI and system
        public enum AbandonedHandlingMode
        {
            None = 0,
            AutoDemolish = 1,
            DisableAbandonment = 2,   // clear flags, keep buildings
        }
    }
}
