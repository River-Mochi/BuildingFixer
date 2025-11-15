// AbandonedBuildingBossSystem.cs
// Purpose: ABB/BEC runtime — dependable cleanup/restore (allocation-free queries) + deep “RESCUE ALL”.
// Notes: Game-only gating; collapsed also heals icon-only cases; counts refreshed after real work.

namespace AbandonedBuildingBoss
{
    using Game;                     // Update cadence helpers
    using Game.Buildings;           // Building, Abandoned, Condemned, BuildingCondition, RescueTarget
    using Game.Common;              // Deleted, Temp, Updated, Destroyed
    using Game.Net;
    using Game.Notifications;       // IconCommandSystem, IconCommandBuffer, IconElement
    using Game.Objects;             // Damaged
    using Game.Prefabs;             // BuildingConfigurationData
    using Game.SceneFlow;           // GameManager, GameMode
    using Game.Tools;               // (edge nudges)
    using Unity.Entities;           // ECS core
    using Purpose = Colossal.Serialization.Entities.Purpose;

    public sealed partial class AbandonedBuildingBossSystem : GameSystemBase
    {
        // ---- Tuning ----
        private const int kUpdateIntervalFrames = 8;   // cadence for GameSimulation
        private const int kBatchPerFrame = 2048; // per-tick work cap (heavy saves)

        // ---- Queries ----
        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;
        private EntityQuery m_DestroyedQuery;
        private EntityQuery m_BuildCfgQuery;
        private EntityQuery m_HasIconBufferQuery;      // buildings with IconElement buffer

        // ---- Systems ----
        private IconCommandSystem? m_IconCmdSys;

        // ---- State ----
        private bool m_IsCityLoaded;
        private bool m_DoAutoCountOnce;

#if DEBUG
        private bool m_LoggedNoCityOnce;
#endif

        // Public: allow Setting/Mod to schedule a pass next tick.
        public void RequestRunNextTick()
        {
            m_DoAutoCountOnce = true;
            Enabled = true;
#if DEBUG
            Mod.s_Log.Info("[ABB] RequestRunNextTick ⇒ scheduled");
#endif
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            // Reusable, allocation-free queries
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

            m_HasIconBufferQuery = SystemAPI.QueryBuilder()
                .WithAll<Building>()
                .WithAllRW<IconElement>()     // buildings that currently have any icon buffer
                .WithNone<Deleted, Temp>()
                .Build();

            m_BuildCfgQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            m_IconCmdSys = World.GetOrCreateSystemManaged<IconCommandSystem>();

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
            m_DoAutoCountOnce = false;
            Enabled = (mode == GameMode.Game);

            if (Mod.Settings != null && !m_IsCityLoaded)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_IsCityLoaded = (mode == GameMode.Game);
            m_DoAutoCountOnce = m_IsCityLoaded; // triggers first auto count
            if (m_IsCityLoaded)
                Enabled = true;

#if DEBUG
            Mod.s_Log.Info($"[ABB] OnGameLoadingComplete → loaded={m_IsCityLoaded}, firstPass={m_DoAutoCountOnce}");
#endif
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
            => phase == SystemUpdatePhase.GameSimulation ? kUpdateIntervalFrames : 1;

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            var setting = Mod.Settings;
            if (setting == null)
                return;

            // Manual Refresh from Options
            if (setting.TryConsumeRefreshRequest())
            {
                DoCount(setting, countedNow: true);
                return;
            }

            // Bail cleanly if not in a running game
            var gm = GameManager.instance;
            if (!m_IsCityLoaded || gm == null || gm.gameMode != GameMode.Game)
            {
                setting.SetStatus("No city loaded", countedNow: false);
                Enabled = false;
#if DEBUG
                if (!m_LoggedNoCityOnce)
                {
                    m_LoggedNoCityOnce = true;
                    Mod.s_Log.Info("[ABB] OnUpdate bail: not in Game");
                }
#endif
                return;
            }

            // First count when city enters
            if (m_DoAutoCountOnce)
            {
                m_DoAutoCountOnce = false;
                DoCount(setting, countedNow: true);
            }

            // Prepare command buffer + cfg
            if (m_IconCmdSys == null)
                m_IconCmdSys = World.GetOrCreateSystemManaged<IconCommandSystem>();
            IconCommandBuffer iconCmd = m_IconCmdSys.CreateCommandBuffer();

            bool haveCfg = !m_BuildCfgQuery.IsEmptyIgnoreFilter;
            BuildingConfigurationData cfg = default;
            if (haveCfg)
                cfg = m_BuildCfgQuery.GetSingleton<BuildingConfigurationData>();

            // ---- Work passes (allocation-free; capped per frame) ----
            bool didWork = false;

            // CONDEMNED
            if (setting.RemoveCondemned)
                didWork |= Step_RemoveCondemned_NoCursor() > 0;
            else if (setting.DisableCondemned)
                didWork |= Step_DisableCondemned_NoCursor(iconCmd, in cfg, haveCfg) > 0;

            // ABANDONED
            if (setting.RemoveAbandoned)
                didWork |= Step_RemoveAbandoned_NoCursor() > 0;
            else if (setting.DisableAbandonment)
                didWork |= Step_DisableAbandoned_NoCursor(iconCmd, in cfg, haveCfg) > 0;

            // COLLAPSED (Destroyed) — include icon-only cases too
            if (setting.RemoveCollapsed)
            {
                didWork |= Step_RemoveCollapsed_NoCursor() > 0;
                didWork |= Step_Remove_CollapseIconOnly_NoCursor() > 0;
            }
            else if (setting.DisableCollapsed)
            {
                didWork |= Step_DisableCollapsed_NoCursor(iconCmd, in cfg, haveCfg) > 0;
                didWork |= Step_Restore_CollapseIconOnly_NoCursor(iconCmd, in cfg, haveCfg) > 0;
            }

            // One-shot: RESCUE ALL (deep)
            if (setting.TryConsumeRescueAllNowRequest())
            {
#if DEBUG
                Mod.s_Log.Info("[ABB] RESCUE ALL (deep) pressed → full city sweep");
#endif
                Step_RescueAll_NoCursorDeep(iconCmd, in cfg, haveCfg);
                didWork = true;
            }

            // If any pass actually changed the world, refresh counts once.
            if (didWork)
                DoCount(setting, countedNow: true);
        }

        // ---- Counts (no allocation) ----
        private void DoCount(Setting setting, bool countedNow)
        {
            int abandoned = m_AbandonedQuery.CalculateEntityCount();
            int condemned = m_CondemnedQuery.CalculateEntityCount();
            int collapsed = m_DestroyedQuery.CalculateEntityCount();
            string line = $"Abandoned: {abandoned} | Condemned: {condemned} | Collapsed: {collapsed}";
            setting.SetStatus(line, countedNow);
            setting.SetRefreshPrompt(false);   // clear the “stale” hint after a real count
        }

        // ---- Steps (each returns how many it processed) ----

        // Condemned → Deleted
        private int Step_RemoveCondemned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
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
            return processed;
        }

        // Condemned → clear flag + remove icon + nudge
        private int Step_DisableCondemned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (bRef, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Condemned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                if (em.HasComponent<Condemned>(e))
                    em.RemoveComponent<Condemned>(e);

                // Nudge edge for UI refresh
                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);
                var b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (haveCfg && cfg.m_CondemnedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_CondemnedNotification);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // Abandoned → Deleted
        private int Step_RemoveAbandoned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (bRef, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                // Road nudge - neighbors benefit from refresh)
                var b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // Abandoned → clear + restore + remove icons
        private int Step_DisableAbandoned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: false, iconCmd, in cfg, haveCfg);
                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // Collapsed → Deleted
        private int Step_RemoveCollapsed_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
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
            return processed;
        }

        // Collapsed → clear + cleanup rescue/damage + remove collapsed icon + nudge
        private int Step_DisableCollapsed_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Destroyed>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: true, iconCmd, in cfg, haveCfg);

                if (em.HasComponent<RescueTarget>(e))
                    em.RemoveComponent<RescueTarget>(e);
                if (em.HasComponent<Damaged>(e))
                    em.RemoveComponent<Damaged>(e);

                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // Collapsed icon present (but NOT Destroyed) → remove building (for Auto-Remove Collapsed)
        private int Step_Remove_CollapseIconOnly_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            // Look for IconElement buffers that contain the AbandonedCollapsed prefab.
            if (m_BuildCfgQuery.IsEmptyIgnoreFilter)
                return 0;
            var cfg = m_BuildCfgQuery.GetSingleton<BuildingConfigurationData>();
            if (cfg.m_AbandonedCollapsedNotification == Entity.Null)
                return 0;

            foreach (var (bRef, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp, Destroyed>()
                     .WithEntityAccess())
            {
                if (!em.HasBuffer<IconElement>(e))
                    continue;
                var buf = em.GetBuffer<IconElement>(e);
                bool hasCollapsedIcon = false;
                for (int i = 0; i < buf.Length; i++)
                {
                    if (buf[i].m_Icon == cfg.m_AbandonedCollapsedNotification)
                    {
                        hasCollapsedIcon = true;
                        break;
                    }
                }
                if (!hasCollapsedIcon)
                    continue;

                // Nudge edge to force cluster/UI refresh for neighbors
                var b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // NEW: Collapsed icon present (but NOT Destroyed) → restore in place (for “Restore Collapsed”)
        private int Step_Restore_CollapseIconOnly_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            if (!haveCfg || cfg.m_AbandonedCollapsedNotification == Entity.Null)
                return 0;

            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (bRef, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp, Destroyed>()
                     .WithEntityAccess())
            {
                if (!em.HasBuffer<IconElement>(e))
                    continue;
                var buf = em.GetBuffer<IconElement>(e);
                bool hasCollapsedIcon = false;
                for (int i = 0; i < buf.Length; i++)
                {
                    if (buf[i].m_Icon == cfg.m_AbandonedCollapsedNotification)
                    {
                        hasCollapsedIcon = true;
                        break;
                    }
                }
                if (!hasCollapsedIcon)
                    continue;

                // “Restore” exactly like collapsed healing
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: true, iconCmd, in cfg, haveCfg);
                if (em.HasComponent<RescueTarget>(e))
                    em.RemoveComponent<RescueTarget>(e);
                if (em.HasComponent<Damaged>(e))
                    em.RemoveComponent<Damaged>(e);

                // Also scrub any stray icons so legit ones repopulate next tick
                if (em.HasBuffer<IconElement>(e))
                    em.GetBuffer<IconElement>(e).Clear();

                // Edge/UI refresh
                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);
                var b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        // RESCUE ALL (deep): legacy/messy saves — heal A/X, clear rescue/damage, scrub IconElement, nudge edges.
        private void Step_RescueAll_NoCursorDeep(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;

            foreach (var (bRef, e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                // Flags: heal Abandoned/Destroyed (do not touch Condemned here)
                if (em.HasComponent<Abandoned>(e))
                    em.RemoveComponent<Abandoned>(e);
                if (em.HasComponent<Destroyed>(e))
                    em.RemoveComponent<Destroyed>(e);

                // Market / condition / services
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

                // Remove icons cured, then deep-scrub the buffer
                if (haveCfg)
                {
                    if (cfg.m_AbandonedNotification != Entity.Null)
                        iconCmd.Remove(e, cfg.m_AbandonedNotification);
                    if (cfg.m_AbandonedCollapsedNotification != Entity.Null)
                        iconCmd.Remove(e, cfg.m_AbandonedCollapsedNotification);
                }
                if (em.HasBuffer<IconElement>(e))
                    em.GetBuffer<IconElement>(e).Clear();

                // Nudge building + road edge; if no road, this naturally no-ops
                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);
                var b = bRef.ValueRO;
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

            if (em.HasComponent<Building>(building))
            {
                var b = em.GetComponentData<Building>(building);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }

            // Explicit icon removals for the cured flags
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
