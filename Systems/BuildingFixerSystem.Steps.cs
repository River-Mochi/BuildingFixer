// Systems/BuildingFixerSystem.Steps.cs
// Step_* methods for remove/disable/nudge + Smart Access nudge.

namespace BuildingFixer
{
    using Game.Buildings;      // Building, Abandoned, Condemned, Destroyed, UnderConstruction, BuildingUtils
    using Game.Common;         // Deleted, Temp, Updated
    using Game.Objects;        // Attached, Transform
    using Game.Tools;
    using Unity.Entities;

    public sealed partial class BuildingFixerSystem
    {
        // Cap how many Condemned we fully restore in one tick,
        // to avoid a huge hitch when there are thousands.
        private const int MaxCondemnedPerTick = 512;

#if DEBUG
        private const int MaxPerStepDebugEntities = 5;
#endif

        // --------------------------------------------------------------------
        // Remove (demolish)
        // --------------------------------------------------------------------

        private int Step_RemoveAbandoned(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                // Icons for Abandoned will be handled by vanilla once the building is deleted.
                count++;
            }

            return count;
        }

        private int Step_RemoveCollapsed(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Destroyed>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                count++;
            }

            return count;
        }

        private int Step_RemoveCondemned(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                // Proactively clear icons for this building before demolition,
                // so there are no orphaned Condemned icons.
                BuildingFixerHelpers.ClearNotificationIcons(em, entity);
                BuildingFixerHelpers.ClearNotificationIconsOnAttachedLot(em, entity);

                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                count++;
            }

            return count;
        }

        // --------------------------------------------------------------------
        // Disable / restore (no demolish)
        // --------------------------------------------------------------------

        /// <summary>
        /// Lightweight future-focused disable: reacts to fresh Abandoned+Updated buildings
        /// instead of sweeping the whole city every tick.
        /// </summary>
        private int Step_DisableAbandoned(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned, Updated>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Heavy sweep: restore ALL existing Abandoned buildings.
        /// Triggered only by the "Restore existing Abandoned now" button.
        /// </summary>
        private int Step_DisableAbandonedAll(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            return count;
        }

        private int Step_DisableCollapsed(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Destroyed>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            return count;
        }

        /// <summary>
        /// "Disable Condemned (no demolish)" path:
        /// - Clears Condemned / damage / rescue flags.
        /// - Scrubs notification icons on building + lot.
        /// - Adds Updated so vanilla recomputes visuals/areas.
        /// Work is batched to MaxCondemnedPerTick to avoid a long hitch.
        /// </summary>
        private int Step_DisableCondemned(EntityManager em)
        {
            var count = 0;

#if DEBUG
            int logged = 0;
#endif

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);

#if DEBUG
                if (logged < MaxPerStepDebugEntities)
                {
                    DebugLog($"Step_DisableCondemned: restored Condemned building entity={entity}.");
                    logged++;
                }
#endif
                count++;

                if (count >= MaxCondemnedPerTick)
                {
                    break;
                }
            }

#if DEBUG
            if (count > 0)
            {
                int remaining = 0;
                foreach (RefRO<Building> _ in
                         SystemAPI.Query<RefRO<Building>>()
                                  .WithAll<Condemned>()
                                  .WithNone<Deleted, Temp>())
                {
                    remaining++;
                }

                DebugLog(
                    $"Step_DisableCondemned: restored={count} this tick; remainingCondemnedAfterSweep={remaining}.");
            }
#endif

            return count;
        }

        // --------------------------------------------------------------------
        // Global nudge
        // --------------------------------------------------------------------

        /// <summary>
        /// One-shot global nudge: runs over all live buildings,
        /// nudges building + lot, and marks them Updated so vanilla
        /// systems recompute visuals and connections.
        ///
        /// Also scrubs IconElement buffers so problem icons are removed.
        /// </summary>
        private int Step_GlobalNudgeAndClearIcons(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.NudgeBuildingTransform(em, entity);
                BuildingFixerHelpers.NudgeAttachedLotObject(em, entity);
                BuildingFixerHelpers.MarkRestoreUpdated(em, entity);

                BuildingFixerHelpers.ClearNotificationIcons(em, entity);
                BuildingFixerHelpers.ClearNotificationIconsOnAttachedLot(em, entity);

                count++;
            }

            return count;
        }

        // --------------------------------------------------------------------
        // Smart Access nudge
        // --------------------------------------------------------------------

        /// <summary>
        /// One-shot "Smart Access" nudge for buildings that have a road edge
        /// but fail BuildingUtils.GetAddress (a good proxy for bad road / lot
        /// alignment, which often shows up as NoPedestrianAccess / NoCarAccess).
        ///
        /// We deliberately skip buildings with no m_RoadEdge, because those are
        /// truly unconnected (too far from a road, no services).
        /// </summary>
        private int Step_SmartAccessNudge(EntityManager em)
        {
            var count = 0;

#if DEBUG
            int logged = 0;
#endif

            foreach ((RefRO<Building> buildingRO, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                Building building = buildingRO.ValueRO;

                // Genuine "no road" cases: don't touch.
                Entity roadEdge = building.m_RoadEdge;
                if (roadEdge == Entity.Null || !em.Exists(roadEdge))
                {
                    continue;
                }

                // If the game can resolve a valid address, we assume road / lot
                // alignment is acceptable and skip.
                if (BuildingUtils.GetAddress(em, entity, out _, out _))
                {
                    continue;
                }

                // At this point: there IS a road edge, but no valid address.
                // This kind of misalignment often produces NoPedestrianAccess / NoCarAccess icons.
                BuildingFixerHelpers.NudgeBuildingTransformForAccess(em, entity);
                BuildingFixerHelpers.NudgeAttachedLotObject(em, entity);
                BuildingFixerHelpers.MarkRestoreUpdated(em, entity);

                // Clear icons on these problem buildings as well.
                BuildingFixerHelpers.ClearNotificationIcons(em, entity);
                BuildingFixerHelpers.ClearNotificationIconsOnAttachedLot(em, entity);

                count++;

#if DEBUG
                if (logged < MaxPerStepDebugEntities)
                {
                    DebugLog($"Step_SmartAccessNudge: nudged building entity={entity}.");
                    logged++;
                }
#endif
            }

            return count;
        }
    }
}
