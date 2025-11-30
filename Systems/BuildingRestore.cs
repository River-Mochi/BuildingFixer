// BuildingRestore.cs
// Purpose: Shared restore helpers — clear flags, scrub icons, nudge building + road.

namespace BuildingFixer
{
    using Game.Buildings;      // Building, BuildingCondition, Abandoned, Condemned, Destroyed
    using Game.Common;         // Updated
    using Game.Notifications;  // IconElement
    using Unity.Entities;      // EntityManager, Entity

    internal static class BuildingRestore
    {
        // Core rebuild used by Disable Abandonment/Collapsed and by RESCUE ALL.
        public static void RestoreCore(
            EntityManager em,
            Entity e,
            bool clearAbandoned,
            bool clearCondemned,
            bool clearDestroyed)
        {
            if (clearAbandoned && em.HasComponent<Abandoned>(e))
                em.RemoveComponent<Abandoned>(e);
            if (clearCondemned && em.HasComponent<Condemned>(e))
                em.RemoveComponent<Condemned>(e);
            if (clearDestroyed && em.HasComponent<Destroyed>(e))
                em.RemoveComponent<Destroyed>(e);

            // Reset basic condition if present.
            if (em.HasComponent<BuildingCondition>(e))
                em.SetComponentData(e, new BuildingCondition { m_Condition = 0 });

            // Scrub icon buffer; legit warnings will repopulate next tick.
            if (em.HasBuffer<IconElement>(e))
                em.GetBuffer<IconElement>(e).Clear();

            // Mark building (and its road edge) Updated — cheap and safe even if no road.
            NudgeBuildingAndRoad(em, e);
        }

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
                Building b = em.GetComponentData<Building>(e);
                if (b.m_RoadEdge != Entity.Null && !em.HasComponent<Updated>(b.m_RoadEdge))
                    em.AddComponent<Updated>(b.m_RoadEdge);
            }
        }
    }
}
