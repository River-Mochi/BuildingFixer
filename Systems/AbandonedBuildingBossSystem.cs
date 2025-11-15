// AbandonedBuildingBossSystem.cs
// Purpose: ABB runtime — dependable cleanup/restore passes (allocation-free queries) + deep “RESCUE ALL”.
// Notes: Game-only gating; collapsed gets extra cleanup; explicit recount after work; robust “city loaded” check.

namespace AbandonedBuildingBoss
{
    using Game;                     // Update cadence helpers
    using Game.Buildings;           // Building, Abandoned, Condemned, Destroyed, BuildingCondition, RescueTarget, Property*
    using Game.Common;              // Deleted, Temp, Updated
    using Game.Notifications;       // IconCommandSystem, IconCommandBuffer, IconElement
    using Game.Objects;             // Damaged
    using Game.Prefabs;             // BuildingConfigurationData
    using Game.SceneFlow;           // GameManager, GameMode
    using Game.Tools;               // Road edge references in Building
    using Unity.Entities;           // ECS core
    using Purpose = Colossal.Serialization.Entities.Purpose;

    public sealed partial class AbandonedBuildingBossSystem : GameSystemBase
    {
        // ---- Tuning ----
        private const int kUpdateIntervalFrames = 8;    // cadence for GameSimulation
        private const int kBatchPerFrame = 2048;  // per-tick work cap for regular passes

        // ---- Queries ----
        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;
        private EntityQuery m_DestroyedQuery;
        private EntityQuery m_AnyBuildingQuery;
        private EntityQuery m_BuildConfigQuery;

        // ---- Systems ----
        private IconCommandSystem? m_IconCommandSystem;

        // ---- State ----
        private bool m_IsCityLoaded;
        private bool m_RequestKickOnce;

#if DEBUG
        private bool m_LoggedNoCityOnce;
#endif

        // External nudge from Settings/Mod: run next tick.
        public void RequestRunNextTick()
        {
            m_RequestKickOnce = true;
            Enabled = true;
#if DEBUG
            Mod.s_Log.Info("[ABB] RequestRunNextTick → scheduled");
#endif
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            // Allocation-free queries reused every frame
            m_AbandonedQuery = SystemAPI.QueryBuilder()
                .WithAll<Building, Abandoned>()
                .WithNone<Deleted, Temp>()
                .Build();

            m_CondemnedQuery = SystemAPI.QueryBuilder()
                .WithAll<Building, Condemned>()
                .WithNone<Deleted, Temp>()
                .Build();

            m_DestroyedQuery = SystemAPI.QueryBuilder()
                .WithAll<Building, Destroyed>()
                .WithNone<Deleted, Temp>()
                .Build();

            // “Any city?” detector
            m_AnyBuildingQuery = SystemAPI.QueryBuilder()
                .WithAll<Building>()
                .WithNone<Temp>()
                .Build();

            m_BuildConfigQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Safe initial status at main menu
            if (Mod.Settings != null && GameManager.instance != null && GameManager.instance.gameMode == GameMode.MainMenu)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);

            Enabled = false;

#if DEBUG
            m_LoggedNoCityOnce = false;
            Mod.s_Log.Info("[ABB] OnCreate");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_IsCityLoaded = false;
            m_RequestKickOnce = false;
            Enabled = (mode == GameMode.Game);

            if (Mod.Settings != null)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_IsCityLoaded = (mode == GameMode.Game);
            if (m_IsCityLoaded)
                Enabled = true;
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return phase == SystemUpdatePhase.GameSimulation ? kUpdateIntervalFrames : 1;
        }

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            var setting = Mod.Settings;
            if (setting == null)
                return;

            // Robust “city active?” gate: must be Game mode and have ≥1 Building entity.
            GameManager? gm = GameManager.instance;
            bool inGame = (gm != null && gm.gameMode == GameMode.Game);
            bool haveAnyBuilding = !m_AnyBuildingQuery.IsEmptyIgnoreFilter;

            if (!inGame || !haveAnyBuilding)
            {
                setting.SetStatus("No city loaded", countedNow: false);
                Enabled = false; // woken again by RequestRunNextTick()
#if DEBUG
                if (!m_LoggedNoCityOnce)
                {
                    m_LoggedNoCityOnce = true;
                    Mod.s_Log.Info("[ABB] OnUpdate bail: not in Game or no buildings");
                }
#endif
                return;
            }

            // Manual refresh from Options
            if (setting.TryConsumeRefreshRequest())
            {
                DoCount(setting, countedNow: true);
                return;
            }

            // Create command buffer once per tick
            if (m_IconCommandSystem == null)
                m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            IconCommandBuffer iconCmd = m_IconCommandSystem.CreateCommandBuffer();

            // Read config once per tick (for icon prefab links)
            bool haveCfg = !m_BuildConfigQuery.IsEmptyIgnoreFilter;
            BuildingConfigurationData cfg = default;
            if (haveCfg)
                cfg = m_BuildConfigQuery.GetSingleton<BuildingConfigurationData>();

            // ---- Work passes (each returns true if any entity changed) ----
            bool didWork = false;

            // CONDEMNED
            if (setting.RemoveCondemned)
                didWork |= Step_RemoveCondemned_NoCursor();
            else if (setting.DisableCondemned)
                didWork |= Step_DisableCondemned_NoCursor(iconCmd, in cfg, haveCfg);

            // ABANDONED
            if (setting.RemoveAbandoned)
                didWork |= Step_RemoveAbandoned_NoCursor();
            else if (setting.DisableAbandonment)
                didWork |= Step_DisableAbandoned_NoCursor(iconCmd, in cfg, haveCfg);

            // COLLAPSED (Destroyed)
            if (setting.RemoveCollapsed)
                didWork |= Step_RemoveCollapsed_NoCursor();
            else if (setting.DisableCollapsed)
                didWork |= Step_DisableCollapsed_NoCursor(iconCmd, in cfg, haveCfg);

            // One-shot deep rescue
            if (setting.TryConsumeRescueAllNowRequest())
            {
                Step_RescueAll_NoCursorDeep(iconCmd, in cfg, haveCfg);
                didWork = true;
            }

            // If any pass changed entities, recount immediately so Status is correct now.
            if (didWork)
                DoCount(setting, countedNow: true);
        }

        // ---- Counts (no allocation) ----
        private void DoCount(Setting setting, bool countedNow)
        {
            int abandoned = m_AbandonedQuery.CalculateEntityCount();
            int condemned = m_CondemnedQuery.CalculateEntityCount();
            int collapsed = m_DestroyedQuery.CalculateEntityCount();
            setting.SetStatus($"Abandoned: {abandoned} | Condemned: {condemned} | Collapsed: {collapsed}", countedNow);
            setting.SetRefreshPrompt(false);   // clear “stale” hint after a real count
        }

        // ---- Steps (allocation-free; capped per frame) ----

        // Condemned → Deleted
        private bool Step_RemoveCondemned_NoCursor()
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Condemned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // Condemned → clear + remove icon + nudge
        private bool Step_DisableCondemned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Condemned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                if (em.HasComponent<Condemned>(e))
                    em.RemoveComponent<Condemned>(e);

                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                if (haveCfg && cfg.m_CondemnedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_CondemnedNotification);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // Abandoned → Deleted
        private bool Step_RemoveAbandoned_NoCursor()
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // Abandoned → clear + restore + remove icons
        private bool Step_DisableAbandoned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: false, iconCmd, in cfg, haveCfg);
                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // Collapsed → Deleted
        private bool Step_RemoveCollapsed_NoCursor()
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Destroyed>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // Collapsed → clear + cleanup rescue/damage + remove collapsed icon + nudge
        private bool Step_DisableCollapsed_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            var em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Destroyed>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                // Core restoration (flags/services/market/condition + icon removal + edge nudge)
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: true, iconCmd, in cfg, haveCfg);

                // Collapse leftovers
                if (em.HasComponent<RescueTarget>(e))
                    em.RemoveComponent<RescueTarget>(e);
                if (em.HasComponent<Damaged>(e))
                    em.RemoveComponent<Damaged>(e);
                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed > 0;
        }

        // RESCUE ALL (deep): heal A/X, clear rescue/damage, scrub IconElement, nudge edges.
        private void Step_RescueAll_NoCursorDeep(IconCommandBuffer iconCmd, in BuildingConfigurationData bcfg, bool haveBcfg)
        {
            var em = EntityManager;

            foreach ((RefRO<Building> bRef, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                // Flags: heal Abandoned/Destroyed (we leave Condemned alone here)
                if (em.HasComponent<Abandoned>(e))
                    em.RemoveComponent<Abandoned>(e);
                if (em.HasComponent<Destroyed>(e))
                    em.RemoveComponent<Destroyed>(e);

                // Market / condition / services (idempotent)
                if (em.HasComponent<PropertyOnMarket>(e))
                    em.RemoveComponent<PropertyOnMarket>(e);
                if (!em.HasComponent<PropertyToBeOnMarket>(e))
                    em.AddComponent<PropertyToBeOnMarket>(e);

                if (em.HasComponent<BuildingCondition>(e))
                    em.SetComponentData(e, new BuildingCondition { m_Condition = 0 });

                if (!em.HasComponent<GarbageProducer>(e))
                    em.AddComponentData(e, default(GarbageProducer));
                if (!em.HasComponent<MailProducer>(e))
                    em.AddComponentData(e, default(MailProducer));
                if (!em.HasComponent<ElectricityConsumer>(e))
                    em.AddComponentData(e, default(ElectricityConsumer));
                if (!em.HasComponent<WaterConsumer>(e))
                    em.AddComponentData(e, default(WaterConsumer));

                // Remove collapse leftovers
                if (em.HasComponent<RescueTarget>(e))
                    em.RemoveComponent<RescueTarget>(e);
                if (em.HasComponent<Damaged>(e))
                    em.RemoveComponent<Damaged>(e);

                // Remove icons we cured
                if (haveBcfg)
                {
                    if (bcfg.m_AbandonedNotification != Entity.Null)
                        iconCmd.Remove(e, bcfg.m_AbandonedNotification);
                    if (bcfg.m_AbandonedCollapsedNotification != Entity.Null)
                        iconCmd.Remove(e, bcfg.m_AbandonedCollapsedNotification);
                }

                // Deep: scrub any leftover IconElement entries (legit warnings repopulate next tick)
                if (em.HasBuffer<IconElement>(e))
                    em.GetBuffer<IconElement>(e).Clear();

                // Nudge building + its road edge; no-op if no road edge
                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                Building b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }
        }

        // ---- Shared restore helper ----
        private static void RestoreBuilding(
            EntityManager em,
            Entity building,
            bool clearCondemned,
            bool clearDestroyed,
            IconCommandBuffer iconCmd,
            in BuildingConfigurationData cfg,
            bool haveCfg)
        {
            // Flags
            if (em.HasComponent<Abandoned>(building))
                em.RemoveComponent<Abandoned>(building);
            if (clearCondemned && em.HasComponent<Condemned>(building))
                em.RemoveComponent<Condemned>(building);
            if (clearDestroyed && em.HasComponent<Destroyed>(building))
                em.RemoveComponent<Destroyed>(building);

            // Market
            if (em.HasComponent<PropertyOnMarket>(building))
                em.RemoveComponent<PropertyOnMarket>(building);
            if (!em.HasComponent<PropertyToBeOnMarket>(building))
                em.AddComponent<PropertyToBeOnMarket>(building);

            // Condition
            if (em.HasComponent<BuildingCondition>(building))
                em.SetComponentData(building, new BuildingCondition { m_Condition = 0 });

            // Services
            if (!em.HasComponent<GarbageProducer>(building))
                em.AddComponentData(building, default(GarbageProducer));
            if (!em.HasComponent<MailProducer>(building))
                em.AddComponentData(building, default(MailProducer));
            if (!em.HasComponent<ElectricityConsumer>(building))
                em.AddComponentData(building, default(ElectricityConsumer));
            if (!em.HasComponent<WaterConsumer>(building))
                em.AddComponentData(building, default(WaterConsumer));

            // Refresh UI/graphs
            if (!em.HasComponent<Updated>(building))
                em.AddComponent<Updated>(building);

            // Nudge connected road edge to trigger service recompute / UI refresh
            if (em.HasComponent<Building>(building))
            {
                Building b = em.GetComponentData<Building>(building);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }

            // Explicit icon removals for flags we cured
            if (haveCfg)
            {
                if (cfg.m_AbandonedNotification != Entity.Null)
                    iconCmd.Remove(building, cfg.m_AbandonedNotification);
                if (cfg.m_AbandonedCollapsedNotification != Entity.Null)
                    iconCmd.Remove(building, cfg.m_AbandonedCollapsedNotification);
                if (clearCondemned && cfg.m_CondemnedNotification != Entity.Null)
                    iconCmd.Remove(building, cfg.m_CondemnedNotification);
            }
        }
    }
}
