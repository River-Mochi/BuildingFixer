// File: Setting.cs
// Purpose: Options UI for [ABB] Abandoned Building Boss.
// Layout:
//   Tab 1 (Actions):
//     - Handling behavior (dropdown)
//     - Also clear condemned (toggle)
//     - Count abandoned (button)
//     - Abandoned buildings (read-only text; "Idle" | "No city loaded" | "123 buildings")
//     - Clear current abandoned now (button -> consumed by system)
//   Tab 2 (About):
//     - Mod name
//     - Mod version
//     - 2 link buttons (Discord + Paradox Mods) on same row

namespace AbandonedBuildingBoss
{
    using System;
    using Colossal.IO.AssetDatabase;
    using Game;
    using Game.Modding;
    using Game.SceneFlow;
    using Game.Settings;
    using Game.UI.Widgets;
    using Unity.Entities;
    using UnityEngine;

    [FileLocation("ModsSettings/AbandonedBuildingBoss/AbandonedBuildingBoss")]
    [SettingsUITabOrder(kActionsTab, kAboutTab)]
    [SettingsUIGroupOrder(kActionsGroup, kAboutInfoGroup, kAboutLinksGroup)]
    // do NOT show the Actions group name (you asked to hide it)
    [SettingsUIShowGroupName(kAboutInfoGroup, kAboutLinksGroup)]
    public sealed class Setting : ModSetting
    {
        // ---- tabs ----
        public const string kActionsTab = "Actions";
        public const string kAboutTab = "About";

        // ---- groups ----
        public const string kActionsGroup = "Actions";
        public const string kAboutInfoGroup = "AboutInfo";
        public const string kAboutLinksGroup = "AboutLinks";

        // ---- external links ----
        private const string kUrlMods = "TBD"; // you can fill later
        private const string kUrlDiscord = "https://discord.gg/HTav7ARPs2";

        // ---- backing fields ----
        private AbandonedHandlingMode m_Behavior = AbandonedHandlingMode.None;
        private bool m_AlsoClearCondemned = true;
        private bool m_ClearNow;
        private string m_AbandonedCountText = "Idle";

        public Setting(IMod mod) : base(mod)
        {
        }

        // ============================================================
        // ENUM used by dropdown
        // ============================================================
        public enum AbandonedHandlingMode
        {
            None = 0,               // do nothing
            AutoDemolish = 1,       // bulldoze abandoned (+ sub-areas/nets)
            DisableAbandonment = 2, // clear abandoned (and maybe condemned) + re-enable services
        }

        // ============================================================
        // ACTIONS TAB
        // ============================================================

        // 1) DROPDOWN
        [SettingsUISection(kActionsTab, kActionsGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetBehaviorItems))]
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

        // 2) TOGGLE
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

        // 3) COUNT BUTTON (you wanted this BELOW the toggle)
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool CountAbandoned
        {
            set
            {
                if (!value)
                    return;

                // 1. need game
                var gm = GameManager.instance;
                if (gm == null || !gm.gameMode.IsGameOrEditor())
                {
                    m_AbandonedCountText = "No city loaded";
                    Apply();
                    return;
                }

                // 2. need ECS world
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    m_AbandonedCountText = "No city loaded";
                    Apply();
                    return;
                }

                // 3. need our system
                var sys = world.GetExistingSystemManaged<AbandonedBuildingBossSystem>();
                if (sys == null)
                {
                    m_AbandonedCountText = "System not ready";
                    Apply();
                    return;
                }

                // 4. get count
                int count = sys.GetCurrentAbandonedCount();
                m_AbandonedCountText = (count == 1) ? "1 building" : $"{count} buildings";
                Apply();
            }
        }

        // 4) READ-ONLY DISPLAY (stays at bottom of tab, right after button)
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public string AbandonedCount => m_AbandonedCountText;

        // 5) CLEAR NOW BUTTON (last on Actions tab)
        [SettingsUIButton]
        [SettingsUISection(kActionsTab, kActionsGroup)]
        public bool ClearNow
        {
            set
            {
                if (!value)
                    return;
                m_ClearNow = true;   // system will Consume it
                Apply();
            }
        }

        // this is what the system calls each frame
        public bool TryConsumeClearRequest()
        {
            if (!m_ClearNow)
                return false;
            m_ClearNow = false;
            Apply();
            return true;
        }

        // system also needs this value
        public bool GetAlsoClearCondemned() => m_AlsoClearCondemned;

        // ============================================================
        // ABOUT TAB
        // ============================================================

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModName => Mod.ModName;

        [SettingsUISection(kAboutTab, kAboutInfoGroup)]
        public string ModVersion => Mod.ModVersion;

        // two buttons on same row
        [SettingsUIButtonGroup("Links")]
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
                    Application.OpenURL(kUrlDiscord);
                }
                catch (Exception) { }
            }
        }

        [SettingsUIButtonGroup("Links")]
        [SettingsUIButton]
        [SettingsUISection(kAboutTab, kAboutLinksGroup)]
        public bool OpenMods
        {
            set
            {
                if (!value)
                    return;
                try
                {
                    Application.OpenURL(kUrlMods);
                }
                catch (Exception) { }
            }
        }

        // ============================================================
        // DROPDOWN ITEMS
        // ============================================================
        public DropdownItem<AbandonedHandlingMode>[] GetBehaviorItems()
        {
            return new[]
            {
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.None,
                    displayName = GetOptionLabelLocaleID("Behavior.None"),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.AutoDemolish,
                    displayName = GetOptionLabelLocaleID("Behavior.AutoDemolish"),
                },
                new DropdownItem<AbandonedHandlingMode>
                {
                    value = AbandonedHandlingMode.DisableAbandonment,
                    displayName = GetOptionLabelLocaleID("Behavior.DisableAbandonment"),
                },
            };
        }

        // ============================================================
        // DEFAULTS
        // ============================================================
        public override void SetDefaults()
        {
            m_Behavior = AbandonedHandlingMode.None;
            m_AlsoClearCondemned = true;
            m_ClearNow = false;
            m_AbandonedCountText = "Idle";
        }
    }
}
