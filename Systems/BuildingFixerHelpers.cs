// Systems/BuildingFixerHelpers.cs
// Shared helpers for fixing buildings (flags, icons, emergency requests, small nudges).

namespace BuildingFixer
{
    using Game.Buildings;
    using Game.Common;
    using Game.Notifications;
    using Game.Objects;
    using Game.Prefabs;
    using Game.Simulation;
    using Game.Tools;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    /// <summary>
    /// Helper methods used by <see cref="BuildingFixerSystem"/> to restore / clean up buildings.
    /// </summary>
    internal static class BuildingFixerHelpers
    {
        /// <summary>
        /// Clears Abandoned/Condemned/Collapsed and related temp/emergency flags from a building.
        /// Also clears rescue / service requests and damage markers so debris and fire icons stop looping.
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
        /// Scrubs notification icons attached to a building:
        /// - Marks each icon entity as Deleted (if it exists).
        /// - Clears the IconElement buffer on the building.
        /// Safe to call even if the buffer is missing.
        /// </summary>
        public static void ScrubIconElements(EntityManager em, Entity building)
        {
            if (!em.HasBuffer<IconElement>(building))
            {
                return;
            }

            DynamicBuffer<IconElement> iconBuffer = em.GetBuffer<IconElement>(building);

            // For each referenced icon, mark the icon entity as Deleted so the
            // vanilla icon systems stop rendering it.
            for (int i = 0; i < iconBuffer.Length; i++)
            {
                Entity iconEntity = iconBuffer[i].m_Icon;

                if (iconEntity == Entity.Null)
                {
                    continue;
                }

                if (!em.Exists(iconEntity))
                {
                    continue;
                }

                // Avoid double-tagging.
                if (!em.HasComponent<Deleted>(iconEntity) &&
                    !em.HasComponent<Temp>(iconEntity))
                {
                    em.AddComponent<Deleted>(iconEntity);
                }
            }

            // Finally, clear the buffer on the building itself.
            iconBuffer.Clear();
        }

        /// <summary>
        /// Optionally “nudge” a building’s transform slightly to force re-batching /
        /// visual refresh. Used sparingly.
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
        /// Convenience helper: scrub icons + clear flags, optionally nudge transforms.
        /// Used by the “full restore” / rescue paths.
        /// </summary>
        public static void FullRestore(EntityManager em, Entity building, bool nudgeTransforms)
        {
            ScrubIconElements(em, building);
            ClearProblemFlags(em, building);

            if (nudgeTransforms)
            {
                NudgeBuildingTransform(em, building);
                NudgeAttachedLotObject(em, building);
            }
        }
    }
}
