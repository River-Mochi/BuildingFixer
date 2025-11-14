// AbandonedBuildingBossSystem.cs
// Purpose: ABB runtime — dependable, chunked cleanup and restore (allocation-free, no cursors).
// Notes: Game-only gating; separate Collapsed handling; one-shot RestoreAbandonedNow; explicit icon removal.

namespace AbandonedBuildingBoss
{
    using Game;                    // Update cadence helpers
    using Game.Buildings;          // Building, Abandoned, Condemned, Destroyed, BuildingCondition, RescueTarget
    using Game.Common;             // Deleted, Temp, Updated
    using Game.Notifications;      // IconCommandSystem, IconCommandBuffer
    using Game.Objects;            // Damaged
    using Game.Prefabs;            // BuildingConfigurationData
    using Game.SceneFlow;          // GameManager, GameMode
    using Game.Tools;
    using Unity.Entities;          // ECS core
    using Purpose = Colossal.Serialization.Entities.Purpose;

    public sealed partial class AbandonedBuildingBossSystem : GameSystemBase
    {
        // ---- Tuning / options ----
        private const int kUpdateIntervalFrames = 8;   // cadence for simulation phase
        private const int kBatchPerFrame = 2048;       // per-frame work quota on heavy saves

        // ---- Cached queries ----
        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;
        private EntityQuery m_DestroyedQuery;
        private EntityQuery m_BuildConfigQuery;

        // ---- Systems ----
        private IconCommandSystem? m_IconCommandSystem;

        // ---- State ----
        private bool m_IsCityLoaded;
        private bool m_DoAutoCountOnce;

#if DEBUG
        private bool m_LoggedNoCityOnce;
#endif

        // Public: allow Mod/Setting to schedule a one-shot pass (next tick).
        public void RequestRunNextTick()
        {
            m_DoAutoCountOnce = true;
            Enabled = true;
#if DEBUG
            Mod.s_Log.Info("[ABB] RequestRunNextTick → scheduled.");
#endif
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            // Reusable queries (no allocations on count)
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

            m_BuildConfigQuery = GetEntityQuery(ComponentType.ReadOnly<BuildingConfigurationData>());
            m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Set a safe initial status if we can
            if (Mod.Settings != null && GameManager.instance != null && GameManager.instance.gameMode == GameMode.MainMenu)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);

            Enabled = false; // only run when flagged
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

            // Game-only (do NOT tick in Editor / MainMenu)
            Enabled = (mode == GameMode.Game);

            if (Mod.Settings != null && !m_IsCityLoaded)
            {
                Mod.Settings.SetStatus("No city loaded", countedNow: false);
            }

#if DEBUG
            Mod.s_Log.Info($"[ABB] OnGamePreload purpose={purpose} mode={mode} enabled={Enabled}");
#endif
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            m_IsCityLoaded = (mode == GameMode.Game);
            m_DoAutoCountOnce = m_IsCityLoaded;  // triggers first auto-count + cleanup
            if (m_IsCityLoaded)
                Enabled = true;

#if DEBUG
            Mod.s_Log.Info($"[ABB] OnGameLoadingComplete mode={mode} → isCityLoaded={m_IsCityLoaded}, firstPass={m_DoAutoCountOnce}, enabled={Enabled}");
#endif
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return phase == SystemUpdatePhase.GameSimulation ? kUpdateIntervalFrames : 1;
        }

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            Setting? setting = Mod.Settings;
            if (setting == null)
                return;

            // Manual refresh from Options UI
            if (setting.TryConsumeRefreshRequest())
            {
#if DEBUG
                Mod.s_Log.Info("[ABB] Refresh requested from Options → counting now.");
#endif
                DoCount(setting, countedNow: true);
                return;
            }

            // Keep status meaningful before city is loaded
            GameManager? gm = GameManager.instance;
            if (!m_IsCityLoaded || gm == null || gm.gameMode != GameMode.Game)
            {
                setting.SetStatus("No city loaded", countedNow: false);
                Enabled = false;
#if DEBUG
                if (!m_LoggedNoCityOnce)
                {
                    m_LoggedNoCityOnce = true;
                    Mod.s_Log.Info("[ABB] OnUpdate bail: not in Game → disabling system until next trigger.");
                }
#endif
                return;
            }

            // One-time auto-count on city enter
            if (m_DoAutoCountOnce)
            {
                m_DoAutoCountOnce = false;
#if DEBUG
                Mod.s_Log.Info("[ABB] First pass on city enter → counting.");
#endif
                DoCount(setting, countedNow: true);
            }

            // Prepare notification buffer + config (once per tick).
            if (m_IconCommandSystem == null)
            {
                m_IconCommandSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();
            }
            IconCommandBuffer iconCmd = m_IconCommandSystem.CreateCommandBuffer();

            bool haveCfg = !m_BuildConfigQuery.IsEmptyIgnoreFilter;
            BuildingConfigurationData cfg = default;
            if (haveCfg)
            {
                cfg = m_BuildConfigQuery.GetSingleton<BuildingConfigurationData>();
            }

            // ---- Work passes (allocation-free; each capped per frame) ----

            // Condemned branch
            if (setting.RemoveCondemned)
            {
                Step_RemoveCondemned_NoCursor();
            }
            else if (setting.DisableCondemned)
            {
                Step_DisableCondemned_NoCursor(iconCmd, cfg, haveCfg);
            }

            // Abandoned branch
            if (setting.RemoveAbandoned)
            {
                Step_RemoveAbandoned_NoCursor();
            }
            else if (setting.DisableAbandonment)
            {
                Step_DisableAbandoned_NoCursor(iconCmd, cfg, haveCfg);
            }

            // Collapsed branch (separate control)
            if (setting.RemoveCollapsed)
            {
                Step_RemoveCollapsed_NoCursor();
            }
            else if (setting.DisableCollapsed)
            {
                Step_DisableCollapsed_NoCursor(iconCmd, cfg, haveCfg);
            }

            // One-shot button: Restore Abandoned Now (requested in Options)
            if (setting.TryConsumeRestoreAbandonedNowRequest())
            {
#if DEBUG
                Mod.s_Log.Info("[ABB] RestoreAbandonedNow pressed → clearing Abandoned (one-shot).");
#endif
                Step_DisableAbandoned_NoCursor(iconCmd, cfg, haveCfg);
                DoCount(setting, countedNow: true);
            }
        }

        // ---- Counts (no allocation) ----
        private void DoCount(Setting setting, bool countedNow)
        {
            int abandoned = m_AbandonedQuery.CalculateEntityCount();
            int condemned = m_CondemnedQuery.CalculateEntityCount();
            int collapsed = m_DestroyedQuery.CalculateEntityCount();

            string line = $"Abandoned: {abandoned} | Condemned: {condemned} | Collapsed: {collapsed}";
#if DEBUG
            Mod.s_Log.Info($"[ABB] Count → A:{abandoned} C:{condemned} X:{collapsed} (countedNow={countedNow})");
#endif
            setting.SetStatus(line, countedNow);
            setting.SetRefreshPrompt(false);   // clear “stale” hint after a real count
        }

        // ---- Steps (allocation-free; capped per frame; no cursors)

        // Condemned → Deleted
        private void Step_RemoveCondemned_NoCursor()
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

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] RemoveCondemned processed {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // Condemned → clear flag (keep building) + remove Condemned notification
        private void Step_DisableCondemned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Condemned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                if (em.HasComponent<Condemned>(e))
                    em.RemoveComponent<Condemned>(e);

                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                // Remove Condemned notification if we have config.
                if (haveCfg && cfg.m_CondemnedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_CondemnedNotification);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] DisableCondemned cleared {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // Abandoned → Deleted (ECS will cascade cleanup)
        private void Step_RemoveAbandoned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Abandoned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
#if DEBUG
                int subAreas = em.HasBuffer<Game.Areas.SubArea>(e) ? em.GetBuffer<Game.Areas.SubArea>(e).Length : 0;
                int subNets = em.HasBuffer<Game.Net.SubNet>(e) ? em.GetBuffer<Game.Net.SubNet>(e).Length : 0;
                int subLanes = em.HasBuffer<Game.Net.SubLane>(e) ? em.GetBuffer<Game.Net.SubLane>(e).Length : 0;
                if ((subAreas + subNets + subLanes) > 0)
                    Mod.s_Log.Info($"[ABB] Removing Abandoned: child buffers => Areas:{subAreas} Nets:{subNets} Lanes:{subLanes}");
#endif
                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] RemoveAbandoned processed {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // Abandoned → clear + restore services + remove Abandoned notifications
        private void Step_DisableAbandoned_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Abandoned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: false, iconCmd, cfg, haveCfg);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] DisableAbandoned restored {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // Collapsed (Destroyed) → Deleted
        private void Step_RemoveCollapsed_NoCursor()
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

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] RemoveCollapsed processed {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // Collapsed (Destroyed) → clear + cleanup requests/damage + remove AbandonedCollapsed notification
        private void Step_DisableCollapsed_NoCursor(IconCommandBuffer iconCmd, in BuildingConfigurationData cfg, bool haveCfg)
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Destroyed>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                // Reuse core restoration (clears Destroyed, resets market/condition/services, nudges edges)
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: true, iconCmd, cfg, haveCfg);

                // Extra cleanup for collapsed buildings
                if (em.HasComponent<RescueTarget>(e))
                    em.RemoveComponent<RescueTarget>(e);
                if (em.HasComponent<Damaged>(e))
                    em.RemoveComponent<Damaged>(e);

                if (!em.HasComponent<Updated>(e))
                    em.AddComponent<Updated>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.s_Log.Info($"[ABB] DisableCollapsed restored {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // ---- Restore helper ----
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

            // Services (idempotent)
            if (!em.HasComponent<GarbageProducer>(building))
                em.AddComponentData(building, default(GarbageProducer));
            if (!em.HasComponent<MailProducer>(building))
                em.AddComponentData(building, default(MailProducer));
            if (!em.HasComponent<ElectricityConsumer>(building))
                em.AddComponentData(building, default(ElectricityConsumer));
            if (!em.HasComponent<WaterConsumer>(building))
                em.AddComponentData(building, default(WaterConsumer));

            // UI/graph refresh
            if (!em.HasComponent<Updated>(building))
                em.AddComponent<Updated>(building);

            if (em.HasComponent<Building>(building))
            {
                Building b = em.GetComponentData<Building>(building);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }

            // Explicit notification removal (per PTG pattern)
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
