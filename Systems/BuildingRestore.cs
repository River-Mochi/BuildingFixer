// BuildingRestore.cs
// Purpose: Shared, allocation-free helpers to heal buildings and nudge graphs/UI.

namespace AbandonedBuildingBoss
{
    using Game.Buildings;      // Building, BuildingCondition
    using Game.Common;         // Updated
    using Game.Notifications;  // IconCommandBuffer
    using Game.Objects;        // Damaged
    using Game.Prefabs;        // BuildingConfigurationData
    using Unity.Entities;      // EntityManager, Entity

    internal static class BuildingRestore
    {
        // Core rebuild used by Disable Abandonment/Collapsed and by RESCUE ALL.
        public static void RestoreCore(
            EntityManager em,
            Entity e,
            bool clearAbandoned,
            bool clearCondemned,
            bool clearDestroyed,
            IconCommandBuffer iconCmd,
            in BuildingConfigurationData cfg,
            bool haveCfg)
        {
            if (clearAbandoned && em.HasComponent<Abandoned>(e))
                em.RemoveComponent<Abandoned>(e);
            if (clearCondemned && em.HasComponent<Condemned>(e))
                em.RemoveComponent<Condemned>(e);
            if (clearDestroyed && em.HasComponent<Destroyed>(e))
                em.RemoveComponent<Destroyed>(e);

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

            // Mark building (and its road edge) Updated — cheap and safe even if no road.
            NudgeBuildingAndRoad(em, e);

            // Targeted icon removals for the flags actually cleared.
            if (haveCfg)
            {
                if (clearAbandoned && cfg.m_AbandonedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_AbandonedNotification);
                if (clearDestroyed && cfg.m_AbandonedCollapsedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_AbandonedCollapsedNotification);
                if (clearCondemned && cfg.m_CondemnedNotification != Entity.Null)
                    iconCmd.Remove(e, cfg.m_CondemnedNotification);
            }
        }

        // Collapse/disaster extras.
        public static void ClearCollapseLeftovers(EntityManager em, Entity e)
        {
            if (em.HasComponent<RescueTarget>(e))
                em.RemoveComponent<RescueTarget>(e);
            if (em.HasComponent<Damaged>(e))
                em.RemoveComponent<Damaged>(e);
        }

        // Deep scrub of stale IconElement buffer (legit warnings repopulate next tick).
        public static void ScrubIconElements(EntityManager em, Entity e)
        {
            if (em.HasBuffer<IconElement>(e))
                em.GetBuffer<IconElement>(e).Clear();
        }

        public static void NudgeBuildingAndRoad(EntityManager em, Entity e)
        {
            if (!em.HasComponent<Updated>(e))
                em.AddComponent<Updated>(e);
            if (em.HasComponent<Building>(e))
            {
                var b = em.GetComponentData<Building>(e);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }
        }
    }
}
