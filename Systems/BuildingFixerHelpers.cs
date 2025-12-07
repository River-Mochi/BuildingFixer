// Systems/BuildingFixerHelpers.cs
// Shared helpers for fixing buildings (flags, emergency requests, nudges, Updated/road-edge refresh, icon scrub).

namespace BuildingFixer
{
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Objects;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Helper methods used by <see cref="BuildingFixerSystem"/> to restore / clean up buildings.
    /// </summary>
    internal static class BuildingFixerHelpers
    {
        /// <summary>
        /// Clears Abandoned/Condemned/Collapsed and related temp/emergency flags from a building.
        /// Also clears rescue / service requests and damage markers so debris and fire effects stop looping.
        /// </summary>
        public static void ClearProblemFlags(EntityManager em, Entity building)
        {
            // Core state flags
            if (em.HasComponent<Abandoned>(building))
            {
                em.RemoveComponent<Abandoned>(building);
            }

            if (em.HasComponent<Condemned>(building))
            {
                em.RemoveComponent<Condemned>(building);
            }

            // "Collapsed" in UI is Destroyed in code.
            if (em.HasComponent<Destroyed>(building))
            {
                em.RemoveComponent<Destroyed>(building);
            }

            // Temp = about-to-be-deleted / intermediate.
            if (em.HasComponent<Temp>(building))
            {
                em.RemoveComponent<Temp>(building);
            }

            // Emergency / rescue related
            if (em.HasComponent<RescueTarget>(building))
            {
                em.RemoveComponent<RescueTarget>(building);
            }

            if (em.HasComponent<FireRescueRequest>(building))
            {
                em.RemoveComponent<FireRescueRequest>(building);
            }

            if (em.HasComponent<ServiceRequest>(building))
            {
                em.RemoveComponent<ServiceRequest>(building);
            }

            // Damage marker on the building itself (debris / damaged visuals).
            if (em.HasComponent<Damaged>(building))
            {
                em.RemoveComponent<Damaged>(building);
            }
        }

        /// <summary>
        /// Tiny nudge to force re-batching / visual refresh. Used for general refresh paths.
        /// </summary>
        public static void NudgeBuildingTransform(EntityManager em, Entity building)
        {
            if (!em.HasComponent<Transform>(building))
            {
                return;
            }

            Transform transform = em.GetComponentData<Transform>(building);

            // Tiny nudge in X/Z, should be visually invisible.
            float2 delta = new float2(0.01f, -0.01f);
            transform.m_Position.xz += delta;

            em.SetComponentData(building, transform);
        }

        /// <summary>
        /// Nudge used by the SmartAccess path: slightly larger than the tiny refresh nudge,
        /// roughly ~0.25 world units (~10–12 inches) away from the edge.
        /// </summary>
        public static void NudgeBuildingTransformForAccess(EntityManager em, Entity building)
        {
            if (!em.HasComponent<Transform>(building))
            {
                return;
            }

            Transform transform = em.GetComponentData<Transform>(building);

            float2 delta = new float2(0.25f, -0.25f);
            transform.m_Position.xz += delta;

            em.SetComponentData(building, transform);
        }

        /// <summary>
        /// If the building has a construction / lot object, nudge that too so the
        /// road / lot visuals update consistently. Also clears Damaged on the lot entity.
        /// </summary>
        public static void NudgeAttachedLotObject(EntityManager em, Entity building)
        {
            if (!em.HasComponent<Attached>(building))
            {
                return;
            }

            Attached attached = em.GetComponentData<Attached>(building);
            Entity lotEntity = attached.m_Parent;

            if (lotEntity == Entity.Null || !em.Exists(lotEntity))
            {
                return;
            }

            if (!em.HasComponent<Transform>(lotEntity))
            {
                return;
            }

            Transform transform = em.GetComponentData<Transform>(lotEntity);

            float2 delta = new float2(0.01f, -0.01f);
            transform.m_Position.xz += delta;

            em.SetComponentData(lotEntity, transform);

            // Clear damage marker on the lot object as well (debris piles).
            if (em.HasComponent<Damaged>(lotEntity))
            {
                em.RemoveComponent<Damaged>(lotEntity);
            }
        }

        /// <summary>
        /// Adds Updated to building / lot / road-edge so vanilla systems recompute
        /// visuals, areas and road/connection state after a restore or big tweak.
        /// </summary>
        public static void MarkRestoreUpdated(EntityManager em, Entity building)
        {
            AddUpdatedIfExists(em, building);

            // Lot / construction object
            if (em.HasComponent<Attached>(building))
            {
                Attached attached = em.GetComponentData<Attached>(building);
                Entity lotEntity = attached.m_Parent;

                if (lotEntity != Entity.Null && em.Exists(lotEntity))
                {
                    AddUpdatedIfExists(em, lotEntity);
                }
            }

            // Road edge used for connection / utilities
            if (em.HasComponent<Building>(building))
            {
                Building buildingData = em.GetComponentData<Building>(building);
                Entity roadEdge = buildingData.m_RoadEdge;

                if (roadEdge != Entity.Null && em.Exists(roadEdge))
                {
                    AddUpdatedIfExists(em, roadEdge);
                }
            }
        }

        /// <summary>
        /// Convenience helper: clear flags, scrub icons, optionally nudge transforms,
        /// then mark building / lot / road-edge Updated so vanilla systems re-evaluate.
        /// Used by the “full restore” / rescue paths.
        /// </summary>
        public static void FullRestore(EntityManager em, Entity building, bool nudgeTransforms)
        {
            ClearProblemFlags(em, building);
            ClearNotificationIcons(em, building);
            ClearNotificationIconsOnAttachedLot(em, building);

            if (nudgeTransforms)
            {
                NudgeBuildingTransform(em, building);
                NudgeAttachedLotObject(em, building);
            }

            MarkRestoreUpdated(em, building);
        }

        /// <summary>
        /// Clears any notification icons attached directly to the building (via IconElement buffer).
        /// We don't try to distinguish types here – it's a general "problem icon scrub".
        /// </summary>
        internal static void ClearNotificationIcons(EntityManager em, Entity building)
        {
            try
            {
                if (!em.HasBuffer<IconElement>(building))
                {
                    return;
                }

                DynamicBuffer<IconElement> icons = em.GetBuffer<IconElement>(building);
#if DEBUG
                int before = icons.Length;
#endif
                if (icons.Length > 0)
                {
                    icons.Clear();
#if DEBUG
                    Mod.s_Log.Debug(
                        $"[BF][DEBUG] ClearNotificationIcons: entity={building} removedIconCount={before}");
#endif
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                Mod.s_Log.Debug(
                    $"[BF][DEBUG] ClearNotificationIcons: exception {ex.GetType().Name}: {ex.Message}");
#endif
            }
        }

        /// <summary>
        /// Clears notification icons attached to the building's lot / construction object, if any.
        /// </summary>
        internal static void ClearNotificationIconsOnAttachedLot(EntityManager em, Entity building)
        {
            if (!em.HasComponent<Attached>(building))
            {
                return;
            }

            Attached attached = em.GetComponentData<Attached>(building);
            Entity lotEntity = attached.m_Parent;

            if (lotEntity == Entity.Null || !em.Exists(lotEntity))
            {
                return;
            }

            try
            {
                if (!em.HasBuffer<IconElement>(lotEntity))
                {
                    return;
                }

                DynamicBuffer<IconElement> icons = em.GetBuffer<IconElement>(lotEntity);
#if DEBUG
                int before = icons.Length;
#endif
                if (icons.Length > 0)
                {
                    icons.Clear();
#if DEBUG
                    Mod.s_Log.Debug(
                        $"[BF][DEBUG] ClearNotificationIconsOnAttachedLot: lotEntity={lotEntity} removedIconCount={before}");
#endif
                }
            }
            catch (System.Exception ex)
            {
#if DEBUG
                Mod.s_Log.Debug(
                    $"[BF][DEBUG] ClearNotificationIconsOnAttachedLot: exception {ex.GetType().Name}: {ex.Message}");
#endif
            }
        }

        private static void AddUpdatedIfExists(EntityManager em, Entity entity)
        {
            if (entity == Entity.Null || !em.Exists(entity))
            {
                return;
            }

            if (!em.HasComponent<Updated>(entity))
            {
                em.AddComponent<Updated>(entity);
            }
        }
    }
}
