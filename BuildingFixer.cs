// BuildingFixerSystem.cs
// Purpose: runtime ECS logic for BF – counts, auto-demolish, and remodel
//          abandoned / condemned buildings, and also counts collapsed ones.

namespace BuildingFixer
{
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Purpose = Colossal.Serialization.Entities.Purpose;

    public partial class BuildingFixer : GameSystemBase
    {
        private const int kUpdateIntervalFrames = 16;

        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;

        // “Collapsed” in BF = Destroyed + Building (same as CollapsedBuildingSystem’s logic)
        private EntityQuery m_CollapsedBuildingQuery;

        // GameMode-based "city loaded" flag
        private bool m_IsCityLoaded;
        private bool m_PendingInitialCount;

        protected override void OnCreate()
        {
            base.OnCreate();

            // Abandoned buildings
            m_AbandonedQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Abandoned>(),
                        ComponentType.ReadOnly<Building>(),
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            // Condemned-only buildings
            m_CondemnedQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Condemned>(),
                        ComponentType.ReadOnly<Building>(),
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            // Collapsed buildings: Destroyed + Building, not yet Deleted/Temp.
            m_CollapsedBuildingQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<Destroyed>(),
                        ComponentType.ReadOnly<Building>(),
                    },
                    None = new[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                    },
                });

            m_IsCityLoaded = false;
            m_PendingInitialCount = false;

#if DEBUG
            Mod.Log.Info("[BF] BuildingFixerSystem created.");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Run in Game or Editor scenes; actual “city loaded” is checked separately.
            Enabled = mode.IsGameOrEditor();
            m_IsCityLoaded = false;
            m_PendingInitialCount = false;

#if DEBUG
            Mod.Log.Info($"[BF] OnGamePreload: purpose={purpose}, mode={mode}, enabled={Enabled}");
#endif
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Agreed rule: GameMode.Game means a city is loaded.
            m_IsCityLoaded = (mode == GameMode.Game);
            m_PendingInitialCount = m_IsCityLoaded;

#if DEBUG
            Mod.Log.Info($"[BF] OnGameLoadingComplete: purpose={purpose}, mode={mode}, isCityLoaded={m_IsCityLoaded}");
#endif
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            return phase == SystemUpdatePhase.GameSimulation
                ? kUpdateIntervalFrames
                : 1;
        }

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            var setting = Mod.Settings;
            if (setting == null)
                return;

            // 0) One-time auto-count after a city is loaded
            if (m_IsCityLoaded && m_PendingInitialCount)
            {
                DoCount(setting);
                m_PendingInitialCount = false;
            }

            // 1) Manual actions (always respond; if no city -> "No city loaded")

            if (setting.TryConsumeRefreshCountRequest())
            {
                DoCount(setting);
                return;
            }

            if (setting.TryConsumeRemodelAbandonedRequest())
            {
                DoRemodelAbandoned(setting);
                return;
            }

            if (setting.TryConsumeRemodelCondemnedRequest())
            {
                DoRemodelCondemned(setting);
                return;
            }

            // 2) Automatic behavior – only when a city is loaded
            if (!m_IsCityLoaded)
                return;

            bool autoAbandoned = setting.AutoDemolishAbandoned;
            bool autoCondemned = setting.AutoDemolishCondemned;

            if (!autoAbandoned && !autoCondemned)
                return; // autos completely off

            // If autos are enabled, run them every tick; counts are cheap relative to ECS.
            if (autoAbandoned)
            {
                AutoDemolishAbandoned(includeCondemned: autoCondemned);
            }
            else if (autoCondemned)
            {
                AutoDemolishCondemnedOnly();
            }

            // After autos, refresh counts so UI reflects current state
            DoCount(setting);
        }

        // ------------------------------------------------------------
        // Counting  (Abandoned / Condemned / Collapsed)
        // ------------------------------------------------------------

        private void DoCount(Setting setting)
        {
            if (!m_IsCityLoaded)
            {
                setting.SetStatus("No city loaded");
                return;
            }

            int abandonedCount;
            using (NativeArray<Entity> arr = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                abandonedCount = arr.Length;
            }

            int condemnedCount;
            using (NativeArray<Entity> arr2 = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                condemnedCount = arr2.Length;
            }

            int collapsedCount;
            using (NativeArray<Entity> arr3 = m_CollapsedBuildingQuery.ToEntityArray(Allocator.Temp))
            {
                collapsedCount = arr3.Length;
            }

            string status = $"Abandoned: {abandonedCount} | Condemned: {condemnedCount} | Collapsed: {collapsedCount}";
            setting.SetStatus(status);

#if DEBUG
            Mod.Log.Info($"[BF] DoCount -> {status}");
#endif
        }

        // ------------------------------------------------------------
        // Manual remodel buttons
        // ------------------------------------------------------------

        private void DoRemodelAbandoned(Setting setting)
        {
            if (!m_IsCityLoaded)
            {
                setting.SetStatus("No city loaded");
                return;
            }

            if (m_AbandonedQuery.IsEmptyIgnoreFilter)
            {
                setting.SetStatus("Nothing to remodel (abandoned)");
                return;
            }

#if DEBUG
            Mod.Log.Info("[BF] DoRemodelAbandoned -> restoring abandoned buildings without demolish.");
#endif

            ClearAbandonedWithoutBulldoze(alsoCondemned: false);
            DoCount(setting);
        }

        private void DoRemodelCondemned(Setting setting)
        {
            if (!m_IsCityLoaded)
            {
                setting.SetStatus("No city loaded");
                return;
            }

            if (m_CondemnedQuery.IsEmptyIgnoreFilter)
            {
                setting.SetStatus("Nothing to remodel (condemned)");
                return;
            }

#if DEBUG
            Mod.Log.Info("[BF] DoRemodelCondemned -> restoring condemned buildings without demolish.");
#endif

            ClearCondemnedWithoutBulldoze();
            DoCount(setting);
        }

        // ------------------------------------------------------------
        // MODE 1: bulldoze
        // ------------------------------------------------------------

        private void AutoDemolishAbandoned(bool includeCondemned)
        {
            EntityManager em = EntityManager;

            int demolishedAbandoned = 0;
            int demolishedCondemned = 0;

            // Abandoned buildings
            using (NativeArray<Entity> abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                demolishedAbandoned = abandoned.Length;

                for (int i = 0; i < abandoned.Length; i++)
                {
                    Entity building = abandoned[i];

                    // Sub-areas
                    if (em.HasBuffer<SubArea>(building))
                    {
                        DynamicBuffer<SubArea> buf = em.GetBuffer<SubArea>(building);
                        for (int j = 0; j < buf.Length; j++)
                        {
                            em.AddComponent<Deleted>(buf[j].m_Area);
                        }
                    }

                    // Sub-nets
                    if (em.HasBuffer<SubNet>(building))
                    {
                        DynamicBuffer<SubNet> buf = em.GetBuffer<SubNet>(building);
                        for (int j = 0; j < buf.Length; j++)
                        {
                            em.AddComponent<Deleted>(buf[j].m_SubNet);
                        }
                    }

                    // Sub-lanes
                    if (em.HasBuffer<SubLane>(building))
                    {
                        DynamicBuffer<SubLane> buf = em.GetBuffer<SubLane>(building);
                        for (int j = 0; j < buf.Length; j++)
                        {
                            em.AddComponent<Deleted>(buf[j].m_SubLane);
                        }
                    }

                    // Main building
                    em.AddComponent<Deleted>(building);
                }
            }

            // Optional: condemned-only
            if (includeCondemned)
            {
                using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
                {
                    demolishedCondemned = condemned.Length;

                    for (int i = 0; i < condemned.Length; i++)
                    {
                        Entity building = condemned[i];
                        em.AddComponent<Deleted>(building);
                    }
                }
            }

#if DEBUG
            Mod.Log.Info($"[BF] AutoDemolishAbandoned -> demolished {demolishedAbandoned} abandoned, {demolishedCondemned} condemned (includeCondemned={includeCondemned}).");
#endif
        }

        private void AutoDemolishCondemnedOnly()
        {
            EntityManager em = EntityManager;
            int demolished = 0;

            using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                demolished = condemned.Length;

                for (int i = 0; i < condemned.Length; i++)
                {
                    Entity building = condemned[i];
                    em.AddComponent<Deleted>(building);
                }
            }

#if DEBUG
            Mod.Log.Info($"[BF] AutoDemolishCondemnedOnly -> demolished {demolished} condemned buildings.");
#endif
        }

        // ------------------------------------------------------------
        // MODE 2: keep buildings, clear flags / restore
        // ------------------------------------------------------------

        private void ClearAbandonedWithoutBulldoze(bool alsoCondemned)
        {
            EntityManager em = EntityManager;
            int restoredAbandoned = 0;
            int restoredCondemned = 0;

            // Abandoned -> normal
            using (NativeArray<Entity> abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                restoredAbandoned = abandoned.Length;

                for (int i = 0; i < abandoned.Length; i++)
                {
                    RestoreBuilding(em, abandoned[i], alsoCondemned);
                }
            }

            // Condemned-only -> normal
            if (alsoCondemned)
            {
                using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
                {
                    restoredCondemned = condemned.Length;

                    for (int i = 0; i < condemned.Length; i++)
                    {
                        RestoreBuilding(em, condemned[i], true);
                    }
                }
            }

#if DEBUG
            Mod.Log.Info($"[BF] ClearAbandonedWithoutBulldoze -> restored {restoredAbandoned} abandoned, {restoredCondemned} condemned (alsoCondemned={alsoCondemned}).");
#endif
        }

        private void ClearCondemnedWithoutBulldoze()
        {
            EntityManager em = EntityManager;
            int restoredCondemned = 0;

            using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                restoredCondemned = condemned.Length;

                for (int i = 0; i < condemned.Length; i++)
                {
                    RestoreBuilding(em, condemned[i], alsoCondemned: true);
                }
            }

#if DEBUG
            Mod.Log.Info($"[BF] ClearCondemnedWithoutBulldoze -> restored {restoredCondemned} condemned buildings.");
#endif
        }

        private static void RestoreBuilding(EntityManager em, Entity building, bool alsoCondemned)
        {
            // Remove abandoned flag
            if (em.HasComponent<Abandoned>(building))
            {
                em.RemoveComponent<Abandoned>(building);
            }

            // Optionally remove condemned too
            if (alsoCondemned && em.HasComponent<Condemned>(building))
            {
                em.RemoveComponent<Condemned>(building);
            }

            // Normalize market flags
            if (em.HasComponent<PropertyOnMarket>(building))
            {
                em.RemoveComponent<PropertyOnMarket>(building);
            }

            if (!em.HasComponent<PropertyToBeOnMarket>(building))
            {
                em.AddComponent<PropertyToBeOnMarket>(building);
            }

            // Reset condition so DirtynessSystem stops maxing it
            if (em.HasComponent<BuildingCondition>(building))
            {
                em.SetComponentData(building, new BuildingCondition { m_Condition = 0 });
            }

            // Safe-add basic services
            if (!em.HasComponent<GarbageProducer>(building))
            {
                em.AddComponentData(building, default(GarbageProducer));
            }

            if (!em.HasComponent<MailProducer>(building))
            {
                em.AddComponentData(building, default(MailProducer));
            }

            if (!em.HasComponent<ElectricityConsumer>(building))
            {
                em.AddComponentData(building, default(ElectricityConsumer));
            }

            if (!em.HasComponent<WaterConsumer>(building))
            {
                em.AddComponentData(building, default(WaterConsumer));
            }

#if DEBUG
            Mod.Log.Info($"[BF] RestoreBuilding -> {building.Index}:{building.Version} (alsoCondemned={alsoCondemned})");
#endif
        }
    }
}
