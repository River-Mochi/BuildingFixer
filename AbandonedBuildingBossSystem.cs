// File: AbandonedBuildingBossSystem.cs
// Purpose: runtime ECS logic for [ABB] — counts, clears, and auto-handles
//          abandoned / condemned buildings based on Setting.
// Notes:
//   • MUST be 'partial' so it can merge with the generated file in obj/
//   • needs Colossal.Serialization.Entities for OnGamePreload(Purpose,...)

namespace AbandonedBuildingBoss
{
    using Colossal.Serialization.Entities; // for Purpose
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

        protected override void OnCreate()
        {
            base.OnCreate();

            // Query: all abandoned, real buildings, not deleted/temp
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

            // Query: condemned-only buildings (for the "also clear condemned" option)
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

            // run only when there are abandoned buildings
            RequireForUpdate(m_AbandonedQuery);

#if DEBUG
            Mod.Log.Info("[System] AbandonedBuildingBossSystem created.");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // we only want to actually run inside a loaded map / editor
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

            // 2) user clicked "Clear current abandoned now"
            if (setting.TryConsumeClearRequest())
            {
                bool alsoCondemned = setting.GetAlsoClearCondemned();
                DoClear(setting, alsoCondemned);
                return;
            }

            // 3) automatic behavior
            var behavior = setting.Behavior;
            if (behavior == Setting.AbandonedHandlingMode.None)
                return;

            bool alsoClearCondemned = setting.GetAlsoClearCondemned();

            switch (behavior)
            {
                case Setting.AbandonedHandlingMode.AutoDemolish:
                    AutoDemolishAbandoned(alsoClearCondemned);
                    break;

                case Setting.AbandonedHandlingMode.DisableAbandonment:
                    ClearAbandonedWithoutBulldoze(alsoClearCondemned);
                    break;
            }
        }

        // ------------------------------------------------------------
        // Counting
        // ------------------------------------------------------------
        private void DoCount(Setting setting)
        {
            bool hasAbandoned = !m_AbandonedQuery.IsEmptyIgnoreFilter;
            bool hasCondemned = !m_CondemnedQuery.IsEmptyIgnoreFilter;

            if (!hasAbandoned && !hasCondemned)
            {
                // usually means "no city yet"
                setting.SetStatus("No city loaded");
                return;
            }

            int abandonedCount;
            using (var arr = m_AbandonedQuery.ToEntityArray(Allocator.Temp))
            {
                abandonedCount = arr.Length;
            }

            if (setting.GetAlsoClearCondemned())
            {
                using var arr2 = m_CondemnedQuery.ToEntityArray(Allocator.Temp);
                setting.SetStatus($"Abandoned: {abandonedCount}  |  Condemned: {arr2.Length}");
            }
            else
            {
                setting.SetStatus($"Abandoned: {abandonedCount}");
            }
        }

        // ------------------------------------------------------------
        // Clearing on button
        // ------------------------------------------------------------
        private void DoClear(Setting setting, bool alsoCondemned)
        {
            // nothing to clear and maybe no city
            if (m_AbandonedQuery.IsEmptyIgnoreFilter && (!alsoCondemned || m_CondemnedQuery.IsEmptyIgnoreFilter))
            {
                setting.SetStatus("No city loaded or nothing to clear");
                return;
            }

            ClearAbandonedWithoutBulldoze(alsoCondemned);

            // update the status to show remaining
            DoCount(setting);
        }

        // ------------------------------------------------------------
        // MODE 1: bulldoze
        // ------------------------------------------------------------
        private void AutoDemolishAbandoned(bool alsoCondemned)
        {
            var em = EntityManager;

            // 1) drop abandoned
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
