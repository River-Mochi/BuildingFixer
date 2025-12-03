// Systems/BuildingFixerSystem.Steps.cs
// Step_* methods for remove/disable/nudge + orphan cleanup.

namespace BuildingFixer
{
    using Game.Buildings;      // Building, Abandoned, Condemned, Destroyed, UnderConstruction
    using Game.Common;         // Deleted, Temp, Updated, Owner
    using Game.Notifications;  // IconElement, Icon
    using Game.Objects;
    using Game.Tools;
    using Unity.Entities;

    public sealed partial class BuildingFixerSystem
    {
        // --------------------------------------------------------------------
        // Step helpers (SystemAPI query based)
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

                BuildingFixerHelpers.ScrubIconElements(em, entity);
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

                BuildingFixerHelpers.ScrubIconElements(em, entity);
                count++;
            }

            return count;
        }

        private int Step_RemoveCondemned(EntityManager em)
        {
            var count = 0;

            // 1) Buildings that have Condemned.
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                if (!em.HasComponent<Deleted>(entity))
                {
                    em.AddComponent<Deleted>(entity);
                }

                BuildingFixerHelpers.ScrubIconElements(em, entity);
                count++;
            }

            // 2) Buildings that only have a Condemned icon (IconElement) but no Condemned tag.
            if (TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                                  .WithNone<Deleted, Temp, Condemned>()
                                  .WithEntityAccess())
                {
                    if (!HasCondemnedIcon(em, iconBuffer, condemnedNotificationPrefab))
                    {
                        continue;
                    }

                    if (!em.HasComponent<Deleted>(entity))
                    {
                        em.AddComponent<Deleted>(entity);
                    }

                    BuildingFixerHelpers.ScrubIconElements(em, entity);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Lightweight future-focused disable: reacts to fresh Abandoned+Updated buildings
        /// (and icon-only cases with Updated) instead of sweeping the whole city every tick.
        /// </summary>
        private int Step_DisableAbandoned(EntityManager em)
        {
            var count = 0;

            // 1) Buildings that have Abandoned and were recently Updated.
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned, Updated>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            // 2) Icon-only Abandoned, gated on Updated to keep it cheap.
            if (TryGetAbandonedNotificationPrefabs(
                    out Entity abandonedNotificationPrefab,
                    out Entity abandonedCollapsedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                            .WithAll<Updated>()
                            .WithNone<Deleted>()
                            .WithNone<Temp>()
                            .WithNone<Abandoned>()
                            .WithNone<UnderConstruction>()
                            .WithEntityAccess())
                {
                    if (!HasAbandonedIcon(
                            em, iconBuffer,
                            abandonedNotificationPrefab,
                            abandonedCollapsedNotificationPrefab))
                    {
                        continue;
                    }

                    BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Heavy sweep: restore ALL existing Abandoned buildings and Abandoned icon-only cases.
        /// Triggered only by the "Restore existing Abandoned now" button.
        /// </summary>
        private int Step_DisableAbandonedAll(EntityManager em)
        {
            var count = 0;

            // 1) All Abandoned buildings (non-deleted, non-temp, not under construction).
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Abandoned>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            // 2) Icon-only Abandoned (no Abandoned tag, but abandoned icons present).
            if (TryGetAbandonedNotificationPrefabs(
                    out Entity abandonedNotificationPrefab,
                    out Entity abandonedCollapsedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI
                            .Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                            .WithNone<Deleted>()
                            .WithNone<Temp>()
                            .WithNone<Abandoned>()
                            .WithNone<UnderConstruction>()
                            .WithEntityAccess())
                {
                    if (!HasAbandonedIcon(
                            em,
                            iconBuffer,
                            abandonedNotificationPrefab,
                            abandonedCollapsedNotificationPrefab))
                    {
                        continue;
                    }

                    BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                    count++;
                }
            }

            return count;
        }

        private int Step_DisableCollapsed(EntityManager em)
        {
            int count = 0;

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

        private int Step_DisableCondemned(EntityManager em)
        {
            int count = 0;

            // 1) Buildings that have Condemned.
            foreach ((RefRO<Building> _, Entity entity) in
                     SystemAPI.Query<RefRO<Building>>()
                              .WithAll<Condemned>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                count++;
            }

            // 2) Buildings that only have a Condemned icon but no Condemned tag.
            if (TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab))
            {
                foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                         SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                                  .WithNone<Deleted, Temp, Condemned>()
                                  .WithEntityAccess())
                {
                    if (!HasCondemnedIcon(em, iconBuffer, condemnedNotificationPrefab))
                    {
                        continue;
                    }

                    BuildingFixerHelpers.FullRestore(em, entity, nudgeTransforms: true);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// One-shot global icon clear + road/lot nudge.
        /// Clears all IconElement entries on buildings, nudges transforms so service and
        /// access state can be recomputed, and cleans up orphan icons with no owner.
        /// </summary>
        private int Step_GlobalNudgeAndClearIcons(EntityManager em)
        {
            var count = 0;

            // 1) Building-tied icons and transforms.
            foreach ((RefRO<Building> _, DynamicBuffer<IconElement> iconBuffer, Entity entity) in
                     SystemAPI.Query<RefRO<Building>, DynamicBuffer<IconElement>>()
                              .WithNone<Deleted, Temp, UnderConstruction>()
                              .WithEntityAccess())
            {
                BuildingFixerHelpers.ScrubIconElements(em, entity);
                BuildingFixerHelpers.NudgeBuildingTransform(em, entity);
                BuildingFixerHelpers.NudgeAttachedLotObject(em, entity);
                BuildingFixerHelpers.MarkRestoreUpdated(em, entity);
                count++;
            }

            // 2) Orphaned icon entities that no longer have a live owner.
            count += Step_CleanupOrphanIcons(em);

            return count;
        }

        /// <summary>
        /// Cleans up icon entities whose owner is null, missing, or already Deleted.
        /// Handles ghost utility/problem icons that float in space after their building is gone.
        /// </summary>
        private int Step_CleanupOrphanIcons(EntityManager em)
        {
            var count = 0;

            foreach ((RefRO<Icon> _, RefRO<Owner> owner, Entity iconEntity) in
                     SystemAPI.Query<RefRO<Icon>, RefRO<Owner>>()
                              .WithNone<Deleted, Temp>()
                              .WithEntityAccess())
            {
                Entity ownerEntity = owner.ValueRO.m_Owner;

                if (ownerEntity == Entity.Null ||
                    !em.Exists(ownerEntity) ||
                    em.HasComponent<Deleted>(ownerEntity))
                {
                    if (!em.HasComponent<Deleted>(iconEntity))
                    {
                        em.AddComponent<Deleted>(iconEntity);
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
