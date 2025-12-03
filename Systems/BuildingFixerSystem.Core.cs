// Systems/BuildingFixerSystem.Core.cs
// Core lifecycle + main OnUpdate + status counting.

namespace BuildingFixer
{
    using Colossal.Serialization.Entities;  // Purpose
    using Game;                            // GameSystemBase, GameManager, GameMode
    using Game.Buildings;                  // Building, Abandoned, Condemned, Destroyed
    using Game.Common;                     // Deleted, Temp, Updated
    using Game.SceneFlow;                  // SystemUpdatePhase
    using Game.Simulation;                 // CondemnedBuildingSystem, ZoneCheckSystem
    using Game.Tools;
    using Unity.Entities;

    /// <summary>
    /// Core system for Building Fixer:
    /// - Auto Remove Abandoned / Collapsed / Condemned.
    /// - Disable Abandonment / Collapsed / Condemned (no demolish, full restore).
    /// - Status counting (Abandoned / Condemned / Collapsed) on demand.
    /// - Optional one-shot global icon clear and road/lot nudge.
    /// - Extra: orphan-icon cleanup and better visual/road refresh on restore.
    /// </summary>
    public sealed partial class BuildingFixerSystem : GameSystemBase
    {
        private bool m_IsCityLoaded;
        private bool m_DoAutoCountOnce;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Only care about buildings.
            RequireForUpdate<Building>();

            Enabled = false;
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // Run reasonably often, but all work is filtered to small subsets
            // (Abandoned / Condemned / Destroyed / icon-bearing buildings).
            return 8;
        }

        // Called before a game / map is loaded.
        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            m_IsCityLoaded = false;
            m_DoAutoCountOnce = false;
            Enabled = false;
        }

        // Called when the game / map has finished loading.
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            m_IsCityLoaded = mode == GameMode.Game;

            // Do one initial pass after the city is ready:
            // - Clean up anything according to toggles.
            // - Produce a fresh status snapshot.
            m_DoAutoCountOnce = m_IsCityLoaded;
            Enabled = m_IsCityLoaded;
        }

        /// <summary>
        /// Called by Setting.Apply() to force a run on the very next tick (for example after
        /// changing toggles or pressing Recommended).
        /// </summary>
        public void RequestRunNextTick()
        {
            m_DoAutoCountOnce = true;
            Enabled = true;
        }

        protected override void OnUpdate()
        {
            Setting? setting = Mod.Settings;
            if (setting is null)
            {
                Enabled = false;
                return;
            }

            GameManager gm = GameManager.instance;
            if (!m_IsCityLoaded || gm == null || gm.gameMode != GameMode.Game)
            {
                setting.SetStatus("No city loaded.", countedNow: false);
                Enabled = false;
                return;
            }

            // Keep vanilla Condemned systems in sync with the DisableCondemned toggle.
            SyncCondemnedSystems(setting);

            EntityManager em = EntityManager;

            bool didWork = false;

            // Per-step counts (for debug logging).
            int restoredExistingAbandoned = 0;
            int removedAbandoned = 0;
            int removedCollapsed = 0;
            int removedCondemned = 0;
            int disabledAbandoned = 0;
            int disabledCollapsed = 0;
            int disabledCondemned = 0;
            int globalNudgeCount = 0;

            // ---- ONE-SHOT EXISTING ABANDONED RESTORE ----
            if (setting.TryConsumeRestoreAbandonedOnce())
            {
                restoredExistingAbandoned = Step_DisableAbandonedAll(em);
                didWork |= restoredExistingAbandoned > 0;
                DebugLog($"RestoreExistingAbandonedNow: restored={restoredExistingAbandoned}");
            }

            // ---- AUTO REMOVE ----
            if (setting.RemoveAbandoned)
            {
                removedAbandoned = Step_RemoveAbandoned(em);
                didWork |= removedAbandoned > 0;
            }

            if (setting.RemoveCollapsed)
            {
                removedCollapsed = Step_RemoveCollapsed(em);
                didWork |= removedCollapsed > 0;
            }

            if (setting.RemoveCondemned)
            {
                removedCondemned = Step_RemoveCondemned(em);
                didWork |= removedCondemned > 0;
            }

            // ---- AUTO RESTORE — No Demolish ----
            if (setting.DisableAbandonment)
            {
                disabledAbandoned = Step_DisableAbandoned(em);
                didWork |= disabledAbandoned > 0;
            }

            if (setting.DisableCollapsed)
            {
                disabledCollapsed = Step_DisableCollapsed(em);
                didWork |= disabledCollapsed > 0;
            }

            if (setting.DisableCondemned)
            {
                disabledCondemned = Step_DisableCondemned(em);
                didWork |= disabledCondemned > 0;
            }

            // ---- ONE-SHOT GLOBAL NUDGE + ICON CLEAR + ORPHAN ICON CLEANUP ----
            if (setting.TryConsumeGlobalNudgeRequest())
            {
                globalNudgeCount = Step_GlobalNudgeAndClearIcons(em);
                didWork |= globalNudgeCount > 0;
                DebugLog($"GlobalNudge+ClearIcons: affectedBuildings={globalNudgeCount}");
            }

            // ---- STATUS UPDATE DECISION ----

            bool doCount = false;
            bool autoCountReason = false;
            bool manualRefreshReason = false;
            bool workReason = false;

            // One-time auto-count after load (or after Setting.Apply() called RequestRunNextTick()).
            if (m_DoAutoCountOnce)
            {
                doCount = true;
                autoCountReason = true;
                m_DoAutoCountOnce = false;
            }

            // Manual refresh button.
            bool refreshRequested = setting.TryConsumeRefreshRequest();
            if (refreshRequested)
            {
                doCount = true;
                manualRefreshReason = true;
            }
            else if (didWork)
            {
                // If any buildings changed (removed, restored, or globally nudged),
                // update counts so Status does not lag behind the city view.
                doCount = true;
                workReason = true;
            }

            if (doCount)
            {
                DebugLog(
                    $"Status recount trigger: autoLoad={autoCountReason}, manualRefresh={manualRefreshReason}, afterWork={workReason}");
                CountAndSetStatus(setting);
            }

            // Summary line when any step actually did something.
            if (restoredExistingAbandoned > 0 ||
                removedAbandoned > 0 ||
                removedCollapsed > 0 ||
                removedCondemned > 0 ||
                disabledAbandoned > 0 ||
                disabledCollapsed > 0 ||
                disabledCondemned > 0 ||
                globalNudgeCount > 0)
            {
                DebugLog(
                    $"Step summary: RestoreAllAbandoned={restoredExistingAbandoned}, RemoveAbandoned={removedAbandoned}, RemoveCollapsed={removedCollapsed}, RemoveCondemned={removedCondemned}, DisableAbandoned={disabledAbandoned}, DisableCollapsed={disabledCollapsed}, DisableCondemned={disabledCondemned}, GlobalNudge={globalNudgeCount}");
            }

            // If no toggles are active and no pending auto-count, pause until
            // another city load or Setting.Apply() wakes the system up.
            if (!HasAnyActions(setting) && !m_DoAutoCountOnce)
            {
                Enabled = false;
            }
        }

        // --------------------------------------------------------------------
        // Vanilla system sync
        // --------------------------------------------------------------------

        private void SyncCondemnedSystems(Setting setting)
        {
            World world = World;
            if (world == null)
            {
                return;
            }

            bool disableCondemned = setting.DisableCondemned;

            // ZoneCheckSystem decides when buildings become Condemned based on zones.
            ZoneCheckSystem? zoneCheck = world.GetExistingSystemManaged<ZoneCheckSystem>();
            if (zoneCheck != null)
            {
                zoneCheck.Enabled = !disableCondemned;
            }

            // CondemnedBuildingSystem is the vanilla "demolish condemned" pipeline.
            CondemnedBuildingSystem? condemnedSystem =
                world.GetExistingSystemManaged<CondemnedBuildingSystem>();
            if (condemnedSystem != null)
            {
                condemnedSystem.Enabled = !disableCondemned;
            }
        }

        private static bool HasAnyActions(Setting setting)
        {
            return setting.RemoveAbandoned ||
                   setting.RemoveCollapsed ||
                   setting.RemoveCondemned ||
                   setting.DisableAbandonment ||
                   setting.DisableCollapsed ||
                   setting.DisableCondemned;
        }

        // --------------------------------------------------------------------
        // Status counting
        // --------------------------------------------------------------------

        private void CountAndSetStatus(Setting setting)
        {
            var abandoned = 0;
            var condemned = 0;
            var collapsed = 0;

            // Abandoned (alive, non-temp, non-deleted)
            foreach (RefRO<Building> _ in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp>())
            {
                abandoned++;
            }

            // Condemned (alive, non-temp, non-deleted)
            foreach (RefRO<Building> _ in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>())
            {
                condemned++;
            }

            // Collapsed (Destroyed flag, alive, non-temp, non-deleted)
            foreach (RefRO<Building> _ in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Destroyed>()
                              .WithNone<Deleted, Temp>())
            {
                collapsed++;
            }

            DebugLog(
                $"Status scan: Abandoned={abandoned}, Condemned={condemned}, Collapsed={collapsed}");

            var text =
                $"Abandoned: {abandoned}  |  Condemned: {condemned}  |  Collapsed: {collapsed}";

            setting.SetStatus(text, countedNow: true);
        }
    }
}
