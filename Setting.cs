// Setting.cs
// Purpose: Options UI for Abandoned Building Boss [ABB].

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase; // [FileLocation]
    using Game.Modding;
    using Game.Settings;
    using Game.UI.Localization;
    using Game.UI.Widgets;
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

        // Backing fields
        private AbandonedHandlingMode m_Behavior = AbandonedHandlingMode.AutoDemolish;
        private bool m_AlsoClearCondemned = true;
        private string m_StatusText = "Idle";

        // UI -> system flags (never saved)
        private bool m_RequestCount;
        private bool m_RequestClearRestore;

        public Setting(IMod mod)
            : base(mod)
        {
        }

        public override void SetDefaults()
        {
            // Default: behave like original mod – auto-demolish abandoned buildings.
            m_Behavior = AbandonedHandlingMode.AutoDemolish;
            m_AlsoClearCondemned = true;
            m_StatusText = "Idle";

            m_RequestCount = false;
            m_RequestClearRestore = false;
        }

        // ====== ACTIONS TAB ======

        // Main behavior dropdown
        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetBehaviorDropdownItems))]
        public AbandonedHandlingMode Behavior
        {
            get => m_Behavior;
            set
            {
                if (m_Behavior == value)
                {
                    return;
                }

                m_Behavior = value;
                ApplyAndSave();
            }
        }

        // Also clear condemned
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool AlsoClearCondemned
        {
            get => m_AlsoClearCondemned;
            set
            {
                if (m_AlsoClearCondemned == value)
                {
                    return;
                }

                m_AlsoClearCondemned = value;
                ApplyAndSave();
            }
        }

        // ====== BUTTONS ROW (same group → same row) ======

        // Left button – Count abandoned
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool CountAbandoned
        {
            set
            {
                // UI thread: just flag; system consumes it.
                m_RequestCount = true;
            }
        }

        // Right button – Restore buildings now (one-shot, non-demolish)
        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RestoreNow
        {
            set
            {
                // UI thread: just flag; system consumes it.
                m_RequestClearRestore = true;
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

        // ====== called by the system (simulation thread) ======

        // System asks: “did player press Count?”
        public bool TryConsumeCountRequest()
        {
            if (!m_RequestCount)
            {
                return false;
            }

            m_RequestCount = false;
            return true;
        }

        // System asks: “did player press Restore Now?”
        public bool TryConsumeClearRestoreRequest()
        {
            if (!m_RequestClearRestore)
            {
                return false;
            }

            m_RequestClearRestore = false;
            return true;
        }

        // System updates the status line
        public void SetStatus(string text)
        {
            m_StatusText = text ?? string.Empty;
            Apply(); // do NOT save to disk each frame
        }

        public bool GetAlsoClearCondemned()
        {
            return m_AlsoClearCondemned;
        }

        // ====== dropdown items ======

        public DropdownItem<AbandonedHandlingMode>[] GetBehaviorDropdownItems()
        {
            return new[]
            {
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.AutoDemolish,
                    displayName = LocalizedString.Id("ABB.Behavior.AutoDemolish"),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.DisableAbandonment,
                    displayName = LocalizedString.Id("ABB.Behavior.RestoreBuildings"),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.None,
                    displayName = LocalizedString.Id("ABB.Behavior.None"),
                },
            };
        }

        // ====== enum stays here so system can read Setting.AbandonedHandlingMode ======
        public enum AbandonedHandlingMode
        {
            None = 0,
            AutoDemolish = 1,
            DisableAbandonment = 2,
        }
    }
}
