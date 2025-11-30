// BuildingFixerSystem.cs
// Purpose: BF runtime — dependable cleanup/restore + deep “RESCUE ALL” + empty-zone cleanup.
// Notes: Pure SystemAPI queries; icon scrub via IconElement buffer; counts after real work.

namespace BuildingFixer
{
    using Game;                         // GameSystemBase, GameManager
    using Game.Buildings;               // Building, Abandoned, Condemned, Destroyed, Renter, PropertyOnMarket
    using Game.Common;                  // Deleted, Temp, Updated
    using Game.Notifications;           // IconElement (for scrub)
    using Game.Prefabs;                 // PrefabSystem, PrefabBase
    using Game.SceneFlow;               // GameMode
    using Game.Tools;
    using Unity.Entities;               // Entity, RefRO<>
#if DEBUG
    using UnityEngine;                  // Debug logging context in editor
#endif

    public sealed partial class BuildingFixerSystem : GameSystemBase
    {
        private const int kUpdateIntervalFrames = 8;
        private const int kBatchPerFrame = 2048;

        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;
        private EntityQuery m_DestroyedQuery;

        private PrefabSystem m_PrefabSystem = null!;

        private bool m_IsCityLoaded;
        private bool m_DoAutoCountOnce;

#if DEBUG
        private bool m_LoggedNoCityOnce;
#endif

        public void RequestRunNextTick()
        {
            m_DoAutoCountOnce = true;
            Enabled = true;
#if DEBUG
            Mod.s_Log.Info("[BF] RequestRunNextTick ⇒ scheduled");
#endif
        }

        protected override void OnCreate()
        {
            base.OnCreate();

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

            m_PrefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            if (Mod.Settings != null && GameManager.instance != null && GameManager.instance.gameMode == GameMode.MainMenu)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);

            Enabled = false;
#if DEBUG
            m_LoggedNoCityOnce = false;
            Mod.s_Log.Info("[BF] OnCreate");
#endif
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            m_IsCityLoaded = false;
            m_DoAutoCountOnce = false;
            Enabled = (mode == GameMode.Game);

            if (Mod.Settings != null && !m_IsCityLoaded)
                Mod.Settings.SetStatus("No city loaded", countedNow: false);
        }

        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            m_IsCityLoaded = (mode == GameMode.Game);
            m_DoAutoCountOnce = m_IsCityLoaded;
            if (m_IsCityLoaded)
                Enabled = true;

#if DEBUG
            Mod.s_Log.Info($"[BF] OnGameLoadingComplete → loaded={m_IsCityLoaded}, firstPass={m_DoAutoCountOnce}");
#endif
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
            => phase == SystemUpdatePhase.GameSimulation ? kUpdateIntervalFrames : 1;

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            Setting? setting = Mod.Settings;
            if (setting == null)
                return;

            if (setting.TryConsumeRefreshRequest())
            {
                DoCount(setting, countedNow: true);
                return;
            }

            GameManager gm = GameManager.instance;
            if (!m_IsCityLoaded || gm == null || gm.gameMode != GameMode.Game)
            {
                setting.SetStatus("No city loaded", countedNow: false);
                Enabled = false;
#if DEBUG
                if (!m_LoggedNoCityOnce)
                {
                    m_LoggedNoCityOnce = true;
                    Mod.s_Log.Info("[BF] OnUpdate bail: not in Game");
                }
#endif
                return;
            }

            if (m_DoAutoCountOnce)
            {
                m_DoAutoCountOnce = false;
                DoCount(setting, countedNow: true);
            }

            bool didWork = false;

            // CONDEMNED
            if (setting.RemoveCondemned)
                didWork |= Step_RemoveCondemned_NoCursor() > 0;
            else if (setting.DisableCondemned)
                didWork |= Step_DisableCondemned_NoCursor() > 0;

            // ABANDONED
            if (setting.RemoveAbandoned)
            {
                didWork |= Step_RemoveAbandoned_NoCursor() > 0;
            }
            else if (setting.DisableAbandonment)
            {
                didWork |= Step_DisableAbandoned_NoCursor() > 0;
                didWork |= Step_Restore_AbandonedIconOnly_NoCursor() > 0;
            }

            // COLLAPSED
            if (setting.RemoveCollapsed)
            {
                didWork |= Step_RemoveCollapsed_NoCursor() > 0;
                didWork |= Step_Remove_CollapseIconOnly_NoCursor() > 0;
            }
            else if (setting.DisableCollapsed)
            {
                didWork |= Step_DisableCollapsed_NoCursor() > 0;
                didWork |= Step_Restore_CollapseIconOnly_NoCursor() > 0;
            }

            // Empty-zone cleanup (bonus)
            if (setting.TryConsumeCleanupEmptyNowRequest())
            {
                int n = Step_RemoveEmptyZoned_NoCursor(setting.CleanCommercialEmpty, setting.CleanIndustrialEmpty);
#if DEBUG
                Mod.s_Log.Info($"[BF] CleanupEmpty → removed {n} empty buildings (C:{setting.CleanCommercialEmpty} I:{setting.CleanIndustrialEmpty})");
#endif
                if (n > 0)
                    didWork = true;
            }

            // Deep rescue
            if (setting.TryConsumeRescueAllNowRequest())
            {
#if DEBUG
                Mod.s_Log.Info("[BF] RESCUE ALL (deep) pressed → full city sweep");
#endif
                Step_RescueAll_NoCursorDeep();
                didWork = true;
            }

            if (didWork)
                DoCount(setting, countedNow: true);
        }

        private void DoCount(Setting setting, bool countedNow)
        {
            int abandoned = m_AbandonedQuery.CalculateEntityCount();
            int condemned = m_CondemnedQuery.CalculateEntityCount();
            int collapsed = m_DestroyedQuery.CalculateEntityCount();
            var line = $"Abandoned: {abandoned} | Condemned: {condemned} | Collapsed: {collapsed}";
            setting.SetStatus(line, countedNow);
            setting.SetRefreshPrompt(false);
        }

        // ===================== Passes =====================

        private int Step_RemoveAbandoned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> bRef, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                Building b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private int Step_DisableAbandoned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Abandoned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                BuildingRestore.RestoreCore(em, e,
                    clearAbandoned: true,
                    clearCondemned: false,
                    clearDestroyed: false);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private int Step_Restore_AbandonedIconOnly_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp, Abandoned>()
                     .WithEntityAccess())
            {
                if (em.HasBuffer<IconElement>(e))
                {
                    em.GetBuffer<IconElement>(e).Clear();
                    processed++;
                    if (processed >= kBatchPerFrame)
                        break;
                }
            }
            return processed;
        }

        private int Step_RemoveCollapsed_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> bRef, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Destroyed>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                Building b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private int Step_Remove_CollapseIconOnly_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp, Destroyed>()
                     .WithEntityAccess())
            {
                if (em.HasBuffer<IconElement>(e))
                {
                    em.GetBuffer<IconElement>(e).Clear();
                    processed++;
                    if (processed >= kBatchPerFrame)
                        break;
                }
            }
            return processed;
        }

        private int Step_DisableCollapsed_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Destroyed>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                BuildingRestore.RestoreCore(em, e,
                    clearAbandoned: false,
                    clearCondemned: false,
                    clearDestroyed: true);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private int Step_Restore_CollapseIconOnly_NoCursor()
        {
            return Step_Remove_CollapseIconOnly_NoCursor();
        }

        private int Step_RemoveCondemned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> bRef, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Condemned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                Building b = bRef.ValueRO;
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);

                if (!em.HasComponent<Deleted>(e))
                    em.AddComponent<Deleted>(e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private int Step_DisableCondemned_NoCursor()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithAll<Condemned>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                BuildingRestore.RestoreCore(em, e,
                    clearAbandoned: false,
                    clearCondemned: true,
                    clearDestroyed: false);

                if (++processed >= kBatchPerFrame)
                    break;
            }
            return processed;
        }

        private void Step_RescueAll_NoCursorDeep()
        {
            EntityManager em = EntityManager;
            int processed = 0;

            foreach ((RefRO<Building> _, Entity e) in SystemAPI
                     .Query<RefRO<Building>>()
                     .WithNone<Deleted, Temp>()
                     .WithEntityAccess())
            {
                BuildingRestore.RestoreCore(em, e,
                    clearAbandoned: true,
                    clearCondemned: true,
                    clearDestroyed: true);

                BuildingRestore.ScrubIconElements(em, e);

                if (++processed >= kBatchPerFrame)
                    break;
            }
        }

        // Remove empty Commercial/Industrial (heuristic: no company renters, not on market)
        private int Step_RemoveEmptyZoned_NoCursor(bool doCommercial, bool doIndustrial)
        {
            if (!doCommercial && !doIndustrial)
                return 0;

            EntityManager em = EntityManager;
            int processed = 0;

            // Commercial
            if (doCommercial)
            {
                foreach ((RefRO<Building> bRef, RefRO<PrefabRef> _, Entity e) in SystemAPI
                         .Query<RefRO<Building>, RefRO<PrefabRef>>()
                         .WithAll<CommercialProperty>()
                         .WithNone<Deleted, Temp, Destroyed>()
                         .WithEntityAccess())
                {
                    if (!IsEmptyZoned(em, e))
                        continue;

                    Building b = bRef.ValueRO;
                    if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                        em.AddComponent<Updated>(b.m_RoadEdge);

                    if (!em.HasComponent<Deleted>(e))
                        em.AddComponent<Deleted>(e);

                    if (++processed >= kBatchPerFrame)
                        return processed;
                }
            }

            // Industrial
            if (doIndustrial)
            {
                foreach ((RefRO<Building> bRef, RefRO<PrefabRef> _, Entity e) in SystemAPI
                         .Query<RefRO<Building>, RefRO<PrefabRef>>()
                         .WithAll<IndustrialProperty>()
                         .WithNone<Deleted, Temp, Destroyed>()
                         .WithEntityAccess())
                {
                    if (!IsEmptyZoned(em, e))
                        continue;

                    Building b = bRef.ValueRO;
                    if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                        em.AddComponent<Updated>(b.m_RoadEdge);

                    if (!em.HasComponent<Deleted>(e))
                        em.AddComponent<Deleted>(e);

                    if (++processed >= kBatchPerFrame)
                        return processed;
                }
            }

            return processed;
        }

        /// <summary>“Empty” = no company tenants AND not currently on market.</summary>
        private static bool IsEmptyZoned(EntityManager em, Entity building)
        {
            // If listed/pending on market, it's not a stuck empty
            if (em.HasComponent<PropertyOnMarket>(building))
                return false;

            // No renter buffer at all → empty
            if (!em.HasBuffer<Renter>(building))
                return true;

            var renters = em.GetBuffer<Renter>(building);
            if (renters.Length == 0)
                return true;

            // Any renter with CompanyData → not empty
            for (int i = 0; i < renters.Length; i++)
            {
                var renter = renters[i].m_Renter;
                if (em.HasComponent<Game.Companies.CompanyData>(renter))
                    return false;
            }

            // Renters exist but none are companies → treat as empty (C/I)
            return true;
        }
    }
}
