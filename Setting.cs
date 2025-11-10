// Setting.cs
// Purpose: Options UI for Abandoned Building Boss [ABB].

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;           // [FileLocation]
    using Game.Modding;                        // IMod, ModSetting
    using Game.Settings;                       // SettingsUI*, DropdownItem<T>
    using Game.UI.Localization;               // LocalizedString
    using Game.UI.Widgets;                    // UI attributes
    using UnityEngine;                         // Application.OpenURL

    [FileLocation("ModsSettings/ABB/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kButtonsGroup, kStatusGroup, kAboutInfoGroup, kAboutLinksGroup)]
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

        // Example dropdowns
        private string m_TemplateString = "First";
        private string m_IconStyle = "ColoredPropless";

        // UI -> system flags (never saved)
        private bool m_RequestCount;
        private bool m_RequestClearRestore;

        public Setting(IMod mod)
            : base(mod)
        {
        }

        public override void SetDefaults()
        {
            // Main behavior
            m_Behavior = AbandonedHandlingMode.AutoDemolish;
            m_AlsoClearCondemned = true;
            m_StatusText = "Idle";

            // Example dropdowns
            m_TemplateString = "First";
            m_IconStyle = "ColoredPropless";

            // Buttons
            m_RequestCount = false;
            m_RequestClearRestore = false;
        }

        // ====== MAIN BEHAVIOR DROPDOWN ======
        // This follows CS2 dropdown pattern:
        // public SomeEnum EnumDropdown { get; set; } = SomeEnum.Value1;
        //
        // plus a SettingsUIDropdown with a method returning DropdownItem<AbandonedHandlingMode>[].

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

        // ====== EXAMPLE DROPDOWNS (for testing, exactly like wiki + AssetIcon) ======

        // CO wiki template example:
        // [SettingsUIDropdown(typeof(MyModSetting), nameof(GetStringDropdownItems))]
        // public string StringDropdown { get; set; } = "First";

        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetTemplateStringDropdownItems))]
        public string TemplateStringDropdown
        {
            get => m_TemplateString;
            set
            {
                if (m_TemplateString == value)
                    return;

                m_TemplateString = value;
                ApplyAndSave();
            }
        }

        public DropdownItem<string>[] GetTemplateStringDropdownItems()
        {
            var items = new[]
            {
                new DropdownItem<string>
                {
                    value = "First",
                    displayName = GetOptionLabelLocaleID("Template.First"),
                },
                new DropdownItem<string>
                {
                    value = "Second",
                    displayName = GetOptionLabelLocaleID("Template.Second"),
                },
                new DropdownItem<string>
                {
                    value = "Third",
                    displayName = GetOptionLabelLocaleID("Template.Third"),
                },
            };

            return items;
        }

        // AssetIcon-style example dropdown

        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetIconStyleDropdownItems))]
        public string IconStyleDropdown
        {
            get => m_IconStyle;
            set
            {
                if (m_IconStyle == value)
                    return;

                m_IconStyle = value;
                ApplyAndSave();
            }
        }

        public DropdownItem<string>[] GetIconStyleDropdownItems()
        {
            var items = new[]
            {
                new DropdownItem<string>
                {
                    value = "ColoredPropless",
                    displayName = GetOptionLabelLocaleID("ColoredPropless"),
                },
                new DropdownItem<string>
                {
                    value = "White",
                    displayName = GetOptionLabelLocaleID("White"),
                },
                new DropdownItem<string>
                {
                    value = "Colored",
                    displayName = GetOptionLabelLocaleID("Colored"),
                },
            };

            return items;
        }

        // ====== BUTTONS ROW ======

        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool CountAbandoned
        {
            set
            {
                m_RequestCount = true;
            }
        }

        [SettingsUIButtonGroup(kButtonsGroup)]
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kButtonsGroup)]
        public bool RestoreNow
        {
            set
            {
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

        // ====== System-facing helpers ======

        public bool TryConsumeCountRequest()
        {
            if (!m_RequestCount)
                return false;

            m_RequestCount = false;
            return true;
        }

        public bool TryConsumeClearRestoreRequest()
        {
            if (!m_RequestClearRestore)
                return false;

            m_RequestClearRestore = false;
            return true;
        }

        public void SetStatus(string text)
        {
            m_StatusText = text ?? string.Empty;
            Apply(); // do NOT spam disk with ApplyAndSave here
        }

        public bool GetAlsoClearCondemned()
        {
            return m_AlsoClearCondemned;
        }

        // ====== Dropdown items for Behavior (enum) ======
        // This uses explicit localized IDs so we control the 3 labels exactly.

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

        // Enum used by dropdown & system
        public enum AbandonedHandlingMode
        {
            None = 0,
            AutoDemolish = 1,
            DisableAbandonment = 2,
        }
    }
}
