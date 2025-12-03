// BuildingFixerSystem.IconHelpers.cs
// Helpers for Condemned / Abandoned notification icon prefabs and checks.

namespace BuildingFixer
{
    using Game.Notifications;  // IconElement
    using Game.Prefabs;        // BuildingConfigurationData, PrefabRef
    using Unity.Entities;

    public sealed partial class BuildingFixerSystem
    {
        // --------------------------------------------------------------------
        // Condemned / Abandoned icon helpers
        // --------------------------------------------------------------------

        /// <summary>
        /// Tries to get the NotificationIconPrefab entity used for Condemned
        /// from the BuildingConfigurationData singleton.
        /// </summary>
        private bool TryGetCondemnedNotificationPrefab(out Entity condemnedNotificationPrefab)
        {
            condemnedNotificationPrefab = Entity.Null;

            if (!SystemAPI.TryGetSingleton<BuildingConfigurationData>(out BuildingConfigurationData config))
            {
                return false;
            }

            if (config.m_CondemnedNotification == Entity.Null)
            {
                return false;
            }

            condemnedNotificationPrefab = config.m_CondemnedNotification;
            return true;
        }

        /// <summary>
        /// Tries to fetch the Abandoned / Abandoned-Collapsed notification prefabs
        /// from BuildingConfigurationData.
        /// </summary>
        private bool TryGetAbandonedNotificationPrefabs(
            out Entity abandonedNotificationPrefab,
            out Entity abandonedCollapsedNotificationPrefab)
        {
            abandonedNotificationPrefab = Entity.Null;
            abandonedCollapsedNotificationPrefab = Entity.Null;

            if (!SystemAPI.TryGetSingleton<BuildingConfigurationData>(out BuildingConfigurationData config))
            {
                return false;
            }

            bool any = false;

            if (config.m_AbandonedNotification != Entity.Null)
            {
                abandonedNotificationPrefab = config.m_AbandonedNotification;
                any = true;
            }

            if (config.m_AbandonedCollapsedNotification != Entity.Null)
            {
                abandonedCollapsedNotificationPrefab = config.m_AbandonedCollapsedNotification;
                any = true;
            }

            return any;
        }

        /// <summary>
        /// Checks whether a building's IconElement buffer contains an icon whose
        /// PrefabRef matches the Condemned notification prefab.
        /// </summary>
        private static bool HasCondemnedIcon(
            EntityManager em,
            DynamicBuffer<IconElement> iconBuffer,
            Entity condemnedNotificationPrefab)
        {
            if (condemnedNotificationPrefab == Entity.Null)
            {
                return false;
            }

            for (var i = 0; i < iconBuffer.Length; i++)
            {
                Entity iconEntity = iconBuffer[i].m_Icon;

                if (iconEntity == Entity.Null || !em.Exists(iconEntity))
                {
                    continue;
                }

                if (!em.HasComponent<PrefabRef>(iconEntity))
                {
                    continue;
                }

                PrefabRef prefabRef = em.GetComponentData<PrefabRef>(iconEntity);
                if (prefabRef.m_Prefab == condemnedNotificationPrefab)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a building's IconElement buffer contains an Abandoned or
        /// Abandoned-Collapsed notification icon prefab.
        /// </summary>
        private static bool HasAbandonedIcon(
            EntityManager em,
            DynamicBuffer<IconElement> iconBuffer,
            Entity abandonedNotificationPrefab,
            Entity abandonedCollapsedNotificationPrefab)
        {
            bool hasAbandoned =
                abandonedNotificationPrefab != Entity.Null ||
                abandonedCollapsedNotificationPrefab != Entity.Null;

            if (!hasAbandoned)
            {
                return false;
            }

            for (var i = 0; i < iconBuffer.Length; i++)
            {
                Entity iconEntity = iconBuffer[i].m_Icon;

                if (iconEntity == Entity.Null || !em.Exists(iconEntity))
                {
                    continue;
                }

                if (!em.HasComponent<PrefabRef>(iconEntity))
                {
                    continue;
                }

                PrefabRef prefabRef = em.GetComponentData<PrefabRef>(iconEntity);

                if ((abandonedNotificationPrefab != Entity.Null &&
                     prefabRef.m_Prefab == abandonedNotificationPrefab) ||
                    (abandonedCollapsedNotificationPrefab != Entity.Null &&
                     prefabRef.m_Prefab == abandonedCollapsedNotificationPrefab))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
