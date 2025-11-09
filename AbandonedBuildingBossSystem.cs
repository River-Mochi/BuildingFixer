// File: AbandonedBuildingBossSystem.cs
// Purpose: runtime ECS logic for [ABB] — counts, clears, and auto-handles
//          abandoned / condemned buildings based on Setting.
//
// Notes:
//   • MUST be 'partial' so it can merge with the generated file in obj/
//   • Uses Colossal.Serialization.Entities.Purpose in OnGamePreload.

namespace AbandonedBuildingBoss
{
    using Colossal.Serialization.Entities; // Purpose
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
        private EntityQuery m_AnyBuildingQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            // All abandoned buildings
            m_AbandonedQuery = GetEntityQuery(new EntityQueryDesc
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
                }
            });

            // All condemned buildings
            m_CondemnedQuery = GetEntityQuery(new EntityQueryDesc
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
                }
            });

            // Any real building (for “No city loaded” check)
            m_AnyBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Building>(),
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                }
            });

            // IMPORTANT: no RequireForUpdate here.
            // We always want OnUpdate to run (when Enabled) so it can consume button clicks,
            // even when there are 0 abandoned / 0 condemned buildings.

#if DEBUG
            Mod.Log.Info("[System] AbandonedBuildingBossSystem created.");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // Only run inside a loaded game / editor map.
            Enabled = mode.IsGameOrEditor();

#if DEBUG
            Mod.Log.Info($"[System] OnGamePreload: purpose={purpose}, mode={mode}, enabled={Enabled}");
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

            // 1) user clicked "Count abandoned"
            if (setting.TryConsumeCountRequest())
            {
                DoCount(setting);
                return;
            }

            // 2) user clicked "Restore buildings"
            if (setting.TryConsumeClearRequest())
            {
                bool alsoCondemned = setting.GetAlsoClearCondemned();
                DoRestore(setting, alsoCondemned);
                return;
            }

            // 3) automatic behavior from dropdown
            var behavior = setting.Behavior;
            bool alsoClearCondemned = setting.GetAlsoClearCondemned();

            switch (behavior)
            {
                case Setting.AbandonedHandlingMode.AutoDemolish:
                    AutoDemolishAbandoned(alsoClearCondemned);
                    break;

                case Setting.AbandonedHandlingMode.DisableAbandonment:
                    ClearAbandonedWithoutBulldoze(alsoClearCondemned);
                    break;

                case Setting.AbandonedHandlingMode.None:
                default:
                    // do nothing
                    break;
            }
        }

        // ------------------------------------------------------------
        // Counting
        // ------------------------------------------------------------
        private void DoCount(Setting setting)
        {
            // “No city loaded” = literally no buildings in the world yet.
            bool anyBuildings = !m_AnyBuildingQuery.IsEmptyIgnoreFilter;
            if (!anyBuildings)
            {
                setting.SetStatus("No city loaded");
                return;
            }

            int abandonedCount = m_AbandonedQuery.CalculateEntityCount();
            int condemnedCount = setting.GetAlsoClearCondemned()
                                  ? m_CondemnedQuery.CalculateEntityCount()
                                  : 0;

            if (setting.GetAlsoClearCondemned())
            {
                setting.SetStatus($"Abandoned: {abandonedCount}  |  Condemned: {condemnedCount}");
            }
            else
            {
                setting.SetStatus($"Abandoned: {abandonedCount}");
            }
        }

        // ------------------------------------------------------------
        // Restore button
        // ------------------------------------------------------------
        private void DoRestore(Setting setting, bool alsoCondemned)
        {
            bool anyBuildings = !m_AnyBuildingQuery.IsEmptyIgnoreFilter;
            if (!anyBuildings)
            {
                setting.SetStatus("No city loaded");
                return;
            }

            // If there are no abandoned / condemned, just update status.
            if (m_AbandonedQuery.IsEmptyIgnoreFilter &&
                (!alsoCondemned || m_CondemnedQuery.IsEmptyIgnoreFilter))
            {
                setting.SetStatus("Nothing to restore");
                return;
            }

            ClearAbandonedWithoutBulldoze(alsoCondemned);

            // Recompute counts afterwards
            DoCount(setting);
        }

        // ------------------------------------------------------------
        // MODE 1: bulldoze (original behavior)
        // ------------------------------------------------------------
        private void AutoDemolishAbandoned(bool alsoCondemned)
        {
            var em = EntityManager;

            // 1) bulldoze abandoned
            using (var abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < abandoned.Length; i++)
                {
                    var building = abandoned[i];

                    // delete sub-areas
                    if (em.HasBuffer<SubArea>(building))
                    {
                        var buf = em.GetBuffer<SubArea>(building);
                        foreach (var sub in buf)
                            em.AddComponent<Deleted>(sub.m_Area);
                    }

                    // delete sub-nets
                    if (em.HasBuffer<SubNet>(building))
                    {
                        var buf = em.GetBuffer<SubNet>(building);
                        foreach (var sub in buf)
                            em.AddComponent<Deleted>(sub.m_SubNet);
                    }

                    // delete sub-lanes
                    if (em.HasBuffer<SubLane>(building))
                    {
                        var buf = em.GetBuffer<SubLane>(building);
                        foreach (var sub in buf)
                            em.AddComponent<Deleted>(sub.m_SubLane);
                    }

                    // delete main building
                    em.AddComponent<Deleted>(building);

#if DEBUG
                    Mod.Log.Info($"[System] AutoDemolish -> Deleted abandoned building {building.Index}:{building.Version}");
#endif
                }
            }

            // 2) optionally bulldoze condemned-only
            if (alsoCondemned)
            {
                using var condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < condemned.Length; i++)
                {
                    var building = condemned[i];
                    em.AddComponent<Deleted>(building);

#if DEBUG
                    Mod.Log.Info($"[System] AutoDemolish -> Deleted condemned building {building.Index}:{building.Version}");
#endif
                }
            }
        }

        // ------------------------------------------------------------
        // MODE 2: keep building, clear flags
        // ------------------------------------------------------------
        private void ClearAbandonedWithoutBulldoze(bool alsoCondemned)
        {
            var em = EntityManager;

            // abandoned -> normal
            using (var abandoned = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < abandoned.Length; i++)
                {
                    RestoreBuilding(em, abandoned[i], alsoCondemned);
                }
            }

            // condemned-only -> normal
            if (alsoCondemned)
            {
                using var condemned = m_CondemnedQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < condemned.Length; i++)
                {
                    RestoreBuilding(em, condemned[i], true);
                }
            }
        }

        private static void RestoreBuilding(EntityManager em, Entity building, bool alsoCondemned)
        {
            // remove abandoned flag
            if (em.HasComponent<Abandoned>(building))
            {
                em.RemoveComponent<Abandoned>(building);
            }

            // optionally remove condemned too
            if (alsoCondemned && em.HasComponent<Condemned>(building))
            {
                em.RemoveComponent<Condemned>(building);
            }

            // normalize market flags
            if (em.HasComponent<PropertyOnMarket>(building))
            {
                em.RemoveComponent<PropertyOnMarket>(building);
            }
            if (!em.HasComponent<PropertyToBeOnMarket>(building))
            {
                em.AddComponent<PropertyToBeOnMarket>(building);
            }

            // reset condition so DirtynessSystem stops maxing it
            if (em.HasComponent<BuildingCondition>(building))
            {
                em.SetComponentData(building, new BuildingCondition { m_Condition = 0 });
            }

            // safe-add basic service components
            if (!em.HasComponent<GarbageProducer>(building))
                em.AddComponentData(building, default(GarbageProducer));
            if (!em.HasComponent<MailProducer>(building))
                em.AddComponentData(building, default(MailProducer));
            if (!em.HasComponent<ElectricityConsumer>(building))
                em.AddComponentData(building, default(ElectricityConsumer));
            if (!em.HasComponent<WaterConsumer>(building))
                em.AddComponentData(building, default(WaterConsumer));

#if DEBUG
            Mod.Log.Info($"[System] RestoreBuilding -> {building.Index}:{building.Version} (alsoCondemned={alsoCondemned})");
#endif
        }
    }
}
