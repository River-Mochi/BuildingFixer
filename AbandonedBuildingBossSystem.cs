// File: AbandonedBuildingBossSystem.cs
// Purpose: main GameSystemBase that enforces Settings.Behavior on abandoned buildings
//          and clears on demand from the Options UI.

namespace AbandonedBuildingBoss
{
    using Colossal.Serialization.Entities;
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

        private EntityQuery m_AbandonedBuildingQuery;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_AbandonedBuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<Abandoned>(),
                    ComponentType.ReadOnly<Building>()
                },
                None = new[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>()
                }
            });

            RequireForUpdate(m_AbandonedBuildingQuery);

#if DEBUG
            Mod.Log.Info("[System] AbandonedBuildingBossSystem created.");
#endif
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);

            // enable only in game/editor
            Enabled = mode.IsGameOrEditor();
#if DEBUG
            Mod.Log.Info($"[System] OnGamePreload: purpose={purpose}, mode={mode}, enabled={Enabled}");
#endif
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation)
                return kUpdateIntervalFrames;
            return 1;
        }

        protected override void OnUpdate()
        {
            if (!Enabled)
                return;

            var setting = Mod.Settings;
            if (setting == null)
                return;

            // 1) button from settings
            if (setting.TryConsumeClearRequest())
            {
#if DEBUG
                Mod.Log.Info("[System] Clear button consumed -> clearing abandoned now.");
#endif
                ClearAbandonedWithoutBulldoze(setting.GetAlsoClearCondemned());
            }

            // 2) nothing to do if no abandoned found and mode is passive
            if (m_AbandonedBuildingQuery.IsEmptyIgnoreFilter)
                return;

            // 3) auto behavior
            switch (setting.Behavior)
            {
                case Setting.AbandonedHandlingMode.AutoDemolish:
                    AutoDemolishAbandoned();
                    break;

                case Setting.AbandonedHandlingMode.DisableAbandonment:
                    ClearAbandonedWithoutBulldoze(setting.GetAlsoClearCondemned());
                    break;

                case Setting.AbandonedHandlingMode.None:
                default:
                    // do nothing
                    break;
            }
        }

        // ------------------------------------------------------------
        // public helper for the Settings UI
        // ------------------------------------------------------------
        public int GetCurrentAbandonedCount()
        {
            using var abandoned = m_AbandonedBuildingQuery.ToEntityArray(Allocator.Temp);
            return abandoned.Length;
        }

        // ------------------------------------------------------------
        // Mode 0: hard delete (bulldoze)
        // ------------------------------------------------------------
        private void AutoDemolishAbandoned()
        {
            var em = EntityManager;
            using var abandoned = m_AbandonedBuildingQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < abandoned.Length; i++)
            {
                Entity building = abandoned[i];

                // delete sub areas
                if (em.HasBuffer<SubArea>(building))
                {
                    var buf = em.GetBuffer<SubArea>(building);
                    foreach (var sub in buf)
                        em.AddComponent<Deleted>(sub.m_Area);
                }

                // delete sub nets
                if (em.HasBuffer<SubNet>(building))
                {
                    var buf = em.GetBuffer<SubNet>(building);
                    foreach (var sub in buf)
                        em.AddComponent<Deleted>(sub.m_SubNet);
                }

                // delete sub lanes
                if (em.HasBuffer<SubLane>(building))
                {
                    var buf = em.GetBuffer<SubLane>(building);
                    foreach (var sub in buf)
                        em.AddComponent<Deleted>(sub.m_SubLane);
                }

                // finally: delete building itself
                em.AddComponent<Deleted>(building);

#if DEBUG
                Mod.Log.Info($"[System] AutoDemolish -> Deleted abandoned building {building.Index}:{building.Version}");
#endif
            }
        }

        // ------------------------------------------------------------
        // Mode 1: "keep buildings, clear flags"
        // alsoCondemned=true -> we will also strip Condemned if present
        // ------------------------------------------------------------
        private void ClearAbandonedWithoutBulldoze(bool alsoCondemned)
        {
            var em = EntityManager;
            using var abandoned = m_AbandonedBuildingQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < abandoned.Length; i++)
            {
                var building = abandoned[i];

                // 1) drop Abandoned
                if (em.HasComponent<Abandoned>(building))
                {
                    em.RemoveComponent<Abandoned>(building);
                }

                // 1b) optionally drop Condemned
                if (alsoCondemned && em.HasComponent<Condemned>(building))
                {
                    em.RemoveComponent<Condemned>(building);
                }

                // 2) re-list on market (clean stale first)
                if (em.HasComponent<PropertyOnMarket>(building))
                {
                    em.RemoveComponent<PropertyOnMarket>(building);
                }
                if (!em.HasComponent<PropertyToBeOnMarket>(building))
                {
                    em.AddComponent<PropertyToBeOnMarket>(building);
                }

                // 3) reset condition so DirtynessSystem stops maxing it
                if (em.HasComponent<BuildingCondition>(building))
                {
                    em.SetComponentData(building, new BuildingCondition { m_Condition = 0 });
                }

                // 4) re-enable basic service producers/consumers (safe add)
                if (!em.HasComponent<GarbageProducer>(building))
                    em.AddComponentData(building, default(GarbageProducer));
                if (!em.HasComponent<MailProducer>(building))
                    em.AddComponentData(building, default(MailProducer));
                if (!em.HasComponent<ElectricityConsumer>(building))
                    em.AddComponentData(building, default(ElectricityConsumer));
                if (!em.HasComponent<WaterConsumer>(building))
                    em.AddComponentData(building, default(WaterConsumer));

#if DEBUG
                Mod.Log.Info($"[System] ClearAbandoned -> Cleared building {building.Index}:{building.Version} (alsoCondemned={alsoCondemned})");
#endif
            }
        }
    }
}
