// AbandonedBuildingBossSystem.cs
// Purpose: runtime ECS logic for ABB – counts, restores, and auto-handles
//          abandoned / condemned buildings based on Setting.

namespace AbandonedBuildingBoss
{
    using Game;
    using Game.Areas;
    using Game.Buildings;
    using Game.Common;
    using Game.Net;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;

    public partial class AbandonedBuildingBossSystem : GameSystemBase
    {
        private const int kUpdateIntervalFrames = 16;

        private EntityQuery m_AbandonedQuery;
        private EntityQuery m_CondemnedQuery;

        // Set via OnGameLoadingComplete (GameMode.Game only)
        private bool m_IsGameLoaded;

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

            // Run only when there are abandoned buildings; buttons still work via city-loaded flag.
            RequireForUpdate(m_AbandonedQuery);

#if DEBUG
            Mod.Log.Info("[ABB] AbandonedBuildingBossSystem created.");
#endif
        }

        protected override void OnGamePreload(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Enable system in Game or Editor scenes; we'll still gate logic on Game-only.
            Enabled = mode.IsGameOrEditor();

#if DEBUG
            Mod.Log.Info($"[ABB] OnGamePreload: purpose={purpose}, mode={mode}, enabled={Enabled}");
#endif
        }

        protected override void OnGameLoadingComplete(Colossal.Serialization.Entities.Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);

            // Consider "city loaded" only when in Game mode (not main menu, not editor)
            m_IsGameLoaded = (mode & GameMode.Game) != 0;

#if DEBUG
            Mod.Log.Info($"[ABB] OnGameLoadingComplete: purpose={purpose}, mode={mode}, isGameLoaded={m_IsGameLoaded}");
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
            {
                return;
            }

            var setting = Mod.Settings;
            if (setting == null)
            {
                return;
            }

            // 1) Manual buttons (always available, when a city is loaded)

            if (setting.TryConsumeCountRequest())
            {
                DoCount(setting);
                return;
            }

            if (setting.TryConsumeClearRestoreRequest())
            {
                DoClearRestore(setting);
                return;
            }

            // 2) Automatic behavior (based on dropdown)

            var behavior = setting.Behavior;
            if (behavior == Setting.AbandonedHandlingMode.None)
            {
                // Do nothing – manual only
                return;
            }

            if (!IsCityLoaded())
            {
                // City not in Game mode; don't auto-handle.
                return;
            }

            bool alsoCondemned = setting.GetAlsoClearCondemned();

            switch (behavior)
            {
                case Setting.AbandonedHandlingMode.AutoDemolish:
                    AutoDemolishAbandoned(alsoCondemned);
                    break;

                case Setting.AbandonedHandlingMode.DisableAbandonment:
                    ClearAbandonedWithoutBulldoze(alsoCondemned);
                    break;
            }
        }

        // ------------------------------------------------------------
        // Counting
        // ------------------------------------------------------------

        private void DoCount(Setting setting)
        {
            if (!IsCityLoaded())
            {
                setting.SetStatus("No city loaded");
                return;
            }

            int abandonedCount;
            using (NativeArray<Entity> arr = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                abandonedCount = arr.Length;
            }

            if (!setting.GetAlsoClearCondemned())
            {
                setting.SetStatus($"Abandoned: {abandonedCount}");
                return;
            }

            int condemnedCount;
            using (NativeArray<Entity> arr2 = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                condemnedCount = arr2.Length;
            }

            setting.SetStatus($"Abandoned: {abandonedCount}  |  Condemned: {condemnedCount}");
        }

        // ------------------------------------------------------------
        // Manual restore button
        // ------------------------------------------------------------

        private void DoClearRestore(Setting setting)
        {
            if (!IsCityLoaded())
            {
                setting.SetStatus("No city loaded");
                return;
            }

            bool alsoCondemned = setting.GetAlsoClearCondemned();

            if (m_AbandonedQuery.IsEmptyIgnoreFilter &&
                (!alsoCondemned || m_CondemnedQuery.IsEmptyIgnoreFilter))
            {
                setting.SetStatus("Nothing to restore");
                return;
            }

            ClearAbandonedWithoutBulldoze(alsoCondemned);

            // Update status to show remaining counts
            DoCount(setting);
        }

        // ------------------------------------------------------------
        // MODE 1: bulldoze (original mod behavior)
        // ------------------------------------------------------------

        private void AutoDemolishAbandoned(bool alsoCondemned)
        {
            EntityManager em = EntityManager;

            // Abandoned buildings
            using (NativeArray<Entity> abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
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

#if DEBUG
                    Mod.Log.Info($"[ABB] AutoDemolish -> Deleted abandoned building {building.Index}:{building.Version}");
#endif
                }
            }

            // Optional: condemned-only
            if (!alsoCondemned)
            {
                return;
            }

            using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < condemned.Length; i++)
                {
                    Entity building = condemned[i];
                    em.AddComponent<Deleted>(building);

#if DEBUG
                    Mod.Log.Info($"[ABB] AutoDemolish -> Deleted condemned building {building.Index}:{building.Version}");
#endif
                }
            }
        }

        // ------------------------------------------------------------
        // MODE 2: keep buildings, clear flags / restore
        // ------------------------------------------------------------

        private void ClearAbandonedWithoutBulldoze(bool alsoCondemned)
        {
            EntityManager em = EntityManager;

            // Abandoned -> normal
            using (NativeArray<Entity> abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < abandoned.Length; i++)
                {
                    RestoreBuilding(em, abandoned[i], alsoCondemned);
                }
            }

            // Condemned-only -> normal
            if (!alsoCondemned)
            {
                return;
            }

            using (NativeArray<Entity> condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < condemned.Length; i++)
                {
                    RestoreBuilding(em, condemned[i], true);
                }
            }
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
            Mod.Log.Info($"[ABB] RestoreBuilding -> {building.Index}:{building.Version} (alsoCondemned={alsoCondemned})");
#endif
        }

        // ------------------------------------------------------------
        // City loaded check – GameMode-based
        // ------------------------------------------------------------

        private bool IsCityLoaded()
        {
            return m_IsGameLoaded;
        }
    }
}
