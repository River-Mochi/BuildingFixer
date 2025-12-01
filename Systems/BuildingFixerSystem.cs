// Systems/BuildingFixerSystem.cs
// Main runtime system: auto-remove / disable problem buildings + status counts.

namespace BuildingFixer
{
    using Colossal.Serialization.Entities;  // Purpose
    using Game;                            // GameSystemBase, GameManager, GameMode
    using Game.Buildings;                  // Building, Abandoned, Condemned, Destroyed, RescueTarget, ZoneCheckSystem
    using Game.Common;                     // Deleted, Temp
    using Game.Notifications;              // IconElement
    using Game.Prefabs;                    // BuildingConfigurationData, PrefabRef
    using Game.SceneFlow;                  // SystemUpdatePhase
    using Game.Simulation;                 // CondemnedBuildingSystem
    using Game.Tools;
    using Unity.Entities;

    /// <summary>
    /// Core system for Building Fixer:
    /// - Auto Remove Abandoned / Collapsed / Condemned.
    /// - Disable Abandonment / Collapsed / Condemned (no demolish, full restore).
    /// - Status counting (Abandoned / Condemned / Collapsed) on demand.
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
            // Run reasonably often, but all work is filtered to tiny subsets
            // (Abandoned / Condemned / Destroyed).
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
        /// Called by Setting.Apply() to force a run on the very next tick (e.g. after
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

            // Keep vanilla Condemned systems in sync with our DisableCondemned toggle.
            SyncCondemnedSystems(setting);

            EntityManager em = EntityManager;

            bool didWork = false;

            // ---- AUTO REMOVE ----
            if (setting.RemoveAbandoned)
            {
                didWork |= Step_RemoveAbandoned(em) > 0;
            }

            if (setting.RemoveCollapsed)
            {
                didWork |= Step_RemoveCollapsed(em) > 0;
            }

            if (setting.RemoveCondemned)
            {
                didWork |= Step_RemoveCondemned(em) > 0;
            }

            // ---- AUTO RESTORE — No Demolish ----
            if (setting.DisableAbandonment)
            {
                didWork |= Step_DisableAbandoned(em) > 0;
            }

            if (setting.DisableCollapsed)
            {
                didWork |= Step_DisableCollapsed(em) > 0;
            }

            if (setting.DisableCondemned)
            {
                didWork |= Step_DisableCondemned(em) > 0;
            }

            // ---- STATUS UPDATE DECISION ----

            bool doCount = false;

            // One-time auto-count after load (or after Setting.Apply() called RequestRunNextTick()).
            if (m_DoAutoCountOnce)
            {
                doCount = true;
                m_DoAutoCountOnce = false;
            }

            // Manual refresh button always wins.
            if (setting.TryConsumeRefreshRequest())
            {
                doCount = true;
            }
            else if (didWork)
            {
                // If we just changed any buildings (removed or restored),
                // update counts so Status doesn't lag behind what you see in the city.
                doCount = true;
            }

            if (doCount)
            {
                CountAndSetStatus(setting);
            }

            // If no toggles are active and no pending auto-count, we can pause until
            // another city load or Setting.Apply() wakes us up.
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
        // Step helpers (SystemAPI query based)
        // --------------------------------------------------------------------

        private int Step_RemoveAbandoned(EntityManager em)
        {
            int count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                BuildingFixerHelpers.ScrubIconElements(em, entity);
                count++;
            }

            return count;
        }

        private int Step_RemoveCollapsed(EntityManager em)
        {
            int count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Destroyed>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                BuildingFixerHelpers.ScrubIconElements(em, entity);
                count++;
            }

            return count;
        }

        private int Step_RemoveCondemned(EntityManager em)
        {
            int count = 0;

            // 1) Buildings that actually have Condemned.
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                BuildingFixerHelpers.ScrubIconElements(em, entity);
                count++;
            }

            // 2) Buildings that only have a Condemned icon (IconElement) but no Condemned tag.
            if (TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                                  .WithNone<Deleted, Temp, Condemned>()
                                  .WithEntityAccess())
                {
                    if (!HasCondemnedIcon(em, iconBuffer, condemnedNotificationPrefab))
                    {
                        continue;
                    }

                    if (!em.HasComponent<Deleted>(entity))
                    {
                        em.AddComponent<Deleted>(entity);
                    }

                    BuildingFixerHelpers.ScrubIconElements(em, entity);
                    count++;
                }
            }

            return count;
        }

        private int Step_DisableAbandoned(EntityManager em)
        {
            int count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            return count;
        }

        private int Step_DisableCollapsed(EntityManager em)
        {
            int count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Destroyed>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            return count;
        }

        private int Step_DisableCondemned(EntityManager em)
        {
            int count = 0;

            // 1) Buildings that actually have Condemned.
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            // 2) Buildings that only have a Condemned icon but no Condemned tag.
            if (TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                                  .WithNone<Deleted, Temp, Condemned>()
                                  .WithEntityAccess())
                {
                    if (!HasCondemnedIcon(em, iconBuffer, condemnedNotificationPrefab))
                    {
                        continue;
                    }

                    BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                    count++;
                }
            }

            return count;
        }

        // --------------------------------------------------------------------
        // Condemned icon helpers
        // --------------------------------------------------------------------

        /// <summary>
        /// Tries to get the NotificationIconPrefab entity used for Condemned
        /// from the BuildingConfigurationData singleton.
        /// </summary>
        private bool TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab)
        {
            condemnedNotificationPrefab = Entity.Null;

            if (!SystemAPI.TryGetSingleton<BuildingConfigurationData>(out BuildingConfigurationData config))
            {
                return false;
            }

            if (config.m_CondemnedNotification == Entity.Null)
            {
                return false;
            }

            condemnedNotificationPrefab = config.m_CondemnedNotification;
            return true;
        }

        /// <summary>
        /// Checks whether a building's IconElement buffer contains an icon whose
        /// PrefabRef matches the Condemned notification prefab.
        /// </summary>
        private static bool HasCondemnedIcon(
            EntityManager em,
            DynamicBuffer<IconElement> iconBuffer,
            Entity condemnedNotificationPrefab)
        {
            if (condemnedNotificationPrefab == Entity.Null)
            {
                return false;
            }

            for (int i = 0; i < iconBuffer.Length; i++)
            {
                Entity iconEntity = iconBuffer[i].m_Icon;

                if (iconEntity == Entity.Null || !em.Exists(iconEntity))
                {
                    continue;
                }

                if (!em.HasComponent<PrefabRef>(iconEntity))
                {
                    continue;
                }

                PrefabRef prefabRef = em.GetComponentData<PrefabRef>(iconEntity);
                if (prefabRef.m_Prefab == condemnedNotificationPrefab)
                {
                    return true;
                }
            }

            return false;
        }

        // --------------------------------------------------------------------
        // Status counting
        // --------------------------------------------------------------------

        private void CountAndSetStatus(Setting setting)
        {
            int abandoned = 0;
            int condemned = 0;
            int collapsed = 0;

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

#if DEBUG
            Mod.s_Log.Debug(
                $"[BuildingFixer] Status scan: Abandoned={abandoned}, Condemned={condemned}, Collapsed={collapsed}");
#endif

            string text =
                $"Abandoned: {abandoned}  |  Condemned: {condemned}  |  Collapsed: {collapsed}";

            setting.SetStatus(text, countedNow: true);
        }
    }
}
