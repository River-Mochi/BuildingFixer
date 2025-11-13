// AbandonedBuildingBossSystem.cs
// Purpose: ABB runtime — dependable, chunked cleanup and restore (allocation-free, no cursors).
// Notes: Keeps your passes/queries; safe lifecycle flags; “run next tick” API; richer DEBUG logs.

namespace AbandonedBuildingBoss
{
    using Game;                    // Update cadence helpers
    using Game.Buildings;          // Building, Abandoned, Condemned, Destroyed, BuildingCondition
    using Game.Common;             // Deleted, Temp, Updated
    using Game.Notifications;      // IconCommandSystem, IconCommandBuffer
    using Game.Prefabs;            // BuildingConfigurationData
    using Game.SceneFlow;          // GameManager, GameMode
    using Game.Tools;
    using Unity.Entities;          // ECS core
    using Purpose = Colossal.Serialization.Entities.Purpose;

    public partial class AbandonedBuildingBossSystem : GameSystemBase
    {
        // ---- Tuning / options ----
        private const int kUpdateIntervalFrames = 16;   // cadence for simulation phase
        private const int kBatchPerFrame = 256;         // per-frame work quota on heavy saves

        // Avoid const so branches are not constant-folded (prevents CS0162).
        private static readonly bool kDeleteChildren = true;

        // ---- Cached queries ----
        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;
        private EntityQuery m_DestroyedQuery;
        private EntityQuery m_BuildConfigQuery;

        // ---- Systems ----
        private IconCommandSystem? m_IconSystem;

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
            Mod.Log.Info("[ABB] RequestRunNextTick → scheduled.");
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
            m_IconSystem = World.GetOrCreateSystemManaged<IconCommandSystem>();

            // Set a safe initial status if we can
            if (Mod.Settings != null && GameManager.instance != null && GameManager.instance.gameMode == GameMode.MainMenu)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);

            Enabled = false; // only run when flagged
#if DEBUG
            m_LoggedNoCityOnce = false;
            Mod.Log.Info("[ABB] OnCreate");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            m_IsCityLoaded = false;
            m_DoAutoCountOnce = false;

            // System only ticks in Game/Editor scenes (cheap gate)
            Enabled = mode.IsGameOrEditor();

            if (Mod.Settings != null && mode == GameMode.MainMenu)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);

#if DEBUG
            m_LoggedNoCityOnce = false;
            Mod.Log.Info($"[ABB] OnGamePreload purpose={purpose} mode={mode} enabled={Enabled}");
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
            Mod.Log.Info($"[ABB] OnGameLoadingComplete mode={mode} → isCityLoaded={m_IsCityLoaded}, firstPass={m_DoAutoCountOnce}, enabled={Enabled}");
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

            var setting = Mod.Settings;
            if (setting == null)
                return;

            // Manual refresh from Options UI
            if (setting.TryConsumeRefreshRequest())
            {
#if DEBUG
                Mod.Log.Info("[ABB] Refresh requested from Options → counting now.");
#endif
                DoCount(setting, countedNow: true);
                return;
            }

            // Keep status meaningful before city is loaded
            var gm = GameManager.instance;
            if (!m_IsCityLoaded || gm == null || !gm.gameMode.IsGame())
            {
                setting.SetStatus("No city loaded", countedNow: false);
                Enabled = false;
#if DEBUG
                if (!m_LoggedNoCityOnce)
                {
                    m_LoggedNoCityOnce = true;
                    Mod.Log.Info("[ABB] OnUpdate bail: no city loaded or not in Game mode → disabling system until next trigger.");
                }
#endif
                return;
            }

            // One-time auto-count on city enter
            if (m_DoAutoCountOnce)
            {
                m_DoAutoCountOnce = false;
#if DEBUG
                Mod.Log.Info("[ABB] First pass on city enter → counting.");
#endif
                DoCount(setting, countedNow: true);
            }

            // Prepare icon buffer and abandoned icon handle if available
            IconCommandBuffer? icb = null;
            Entity abandonedIcon = Entity.Null;
            if (m_IconSystem != null && !m_BuildConfigQuery.IsEmptyIgnoreFilter)
            {
                icb = m_IconSystem.CreateCommandBuffer();
                var cfg = m_BuildConfigQuery.GetSingleton<BuildingConfigurationData>();
                abandonedIcon = cfg.m_AbandonedNotification;
            }

            // ---- Work passes (allocation-free; each capped per frame) ----

            // Condemned branch
            if (setting.RemoveCondemned)
            {
                Step_RemoveCondemned_NoCursor();
            }
            else if (setting.DisableCondemned)
            {
                Step_DisableCondemned_NoCursor();
            }

            // Abandoned branch
            if (setting.RemoveAbandoned)
            {
                Step_RemoveAbandoned_NoCursor();                 // demolish
            }
            else if (setting.DisableAbandonment)
            {
                Step_DisableAbandoned_NoCursor(icb, abandonedIcon); // clear Abandoned, restore services
                Step_RestoreDestroyed_NoCursor(icb, abandonedIcon); // clear Destroyed, restore services
            }
        }

        // ---- Counts (no allocation) ----
        private void DoCount(Setting setting, bool countedNow)
        {
            int abandoned = m_AbandonedQuery.CalculateEntityCount();
            int condemned = m_CondemnedQuery.CalculateEntityCount();
            int collapsed = m_DestroyedQuery.CalculateEntityCount();

            var line = $"Abandoned: {abandoned} | Condemned: {condemned} | Collapsed: {collapsed}";
#if DEBUG
            Mod.Log.Info($"[ABB] Count → A:{abandoned} C:{condemned} X:{collapsed} (countedNow={countedNow})");
#endif
            setting.SetStatus(line, countedNow);
        }

        // ---- Steps (allocation-free; capped per frame; no cursors)

        private void Step_RemoveCondemned_NoCursor()
        {
            var em = EntityManager;
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
                Mod.Log.Info($"[ABB] RemoveCondemned processed {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        private void Step_DisableCondemned_NoCursor()
        {
            var em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Condemned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                if (em.HasComponent<Condemned>(e))
                    em.RemoveComponent<Condemned>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.Log.Info($"[ABB] DisableCondemned cleared {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        private void Step_RemoveAbandoned_NoCursor()
        {
            var em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Abandoned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                if (kDeleteChildren)
                    DeleteBuildingWithChildren(em, e);
                else
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.Log.Info($"[ABB] RemoveAbandoned processed {processed}/{kBatchPerFrame} this tick (deleteChildren={kDeleteChildren}).");
#endif
        }

        private void Step_DisableAbandoned_NoCursor(IconCommandBuffer? icb, Entity abandonedIcon)
        {
            var em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Abandoned>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: false, icb, abandonedIcon);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.Log.Info($"[ABB] DisableAbandoned restored {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        private void Step_RestoreDestroyed_NoCursor(IconCommandBuffer? icb, Entity abandonedIcon)
        {
            var em = EntityManager;
            int processed = 0;

            foreach (var (_, e) in SystemAPI
                .Query<RefRO<Building>>()
                .WithAll<Destroyed>()
                .WithNone<Deleted, Temp>()
                .WithEntityAccess())
            {
                RestoreBuilding(em, e, clearCondemned: false, clearDestroyed: true, icb, abandonedIcon);

                if (++processed >= kBatchPerFrame)
                    break;
            }

#if DEBUG
            if (processed > 0)
                Mod.Log.Info($"[ABB] RestoreDestroyed restored {processed}/{kBatchPerFrame} this tick.");
#endif
        }

        // ---- Helpers ----

        private static void DeleteBuildingWithChildren(EntityManager em, Entity building)
        {
            if (em.HasBuffer<Game.Areas.SubArea>(building))
            {
                var buf = em.GetBuffer<Game.Areas.SubArea>(building);
                for (int j = 0; j < buf.Length; j++)
                    em.AddComponent<Deleted>(buf[j].m_Area);
            }

            if (em.HasBuffer<Game.Net.SubNet>(building))
            {
                var buf = em.GetBuffer<Game.Net.SubNet>(building);
                for (int j = 0; j < buf.Length; j++)
                    em.AddComponent<Deleted>(buf[j].m_SubNet);
            }

            if (em.HasBuffer<Game.Net.SubLane>(building))
            {
                var buf = em.GetBuffer<Game.Net.SubLane>(building);
                for (int j = 0; j < buf.Length; j++)
                    em.AddComponent<Deleted>(buf[j].m_SubLane);
            }

            em.AddComponent<Deleted>(building);
        }

        private static void RestoreBuilding(
            EntityManager em,
            Entity building,
            bool clearCondemned,
            bool clearDestroyed,
            IconCommandBuffer? icb,
            Entity abandonedIcon)
        {
            if (em.HasComponent<Abandoned>(building))
            {
                em.RemoveComponent<Abandoned>(building);
                if (icb.HasValue && abandonedIcon != Entity.Null)
                    icb.Value.Remove(building, abandonedIcon);
            }

            if (clearCondemned && em.HasComponent<Condemned>(building))
                em.RemoveComponent<Condemned>(building);

            if (clearDestroyed && em.HasComponent<Destroyed>(building))
                em.RemoveComponent<Destroyed>(building);

            if (em.HasComponent<PropertyOnMarket>(building))
                em.RemoveComponent<PropertyOnMarket>(building);
            if (!em.HasComponent<PropertyToBeOnMarket>(building))
                em.AddComponent<PropertyToBeOnMarket>(building);

            if (em.HasComponent<BuildingCondition>(building))
                em.SetComponentData(building, new BuildingCondition { m_Condition = 0 });

            if (!em.HasComponent<GarbageProducer>(building))
                em.AddComponentData(building, default(GarbageProducer));
            if (!em.HasComponent<MailProducer>(building))
                em.AddComponentData(building, default(MailProducer));
            if (!em.HasComponent<ElectricityConsumer>(building))
                em.AddComponentData(building, default(ElectricityConsumer));
            if (!em.HasComponent<WaterConsumer>(building))
                em.AddComponentData(building, default(WaterConsumer));

            if (em.HasComponent<Building>(building))
            {
                var b = em.GetComponentData<Building>(building);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }
        }
    }
}
