using System.Collections;
using UnityEngine;
using EndlessEngine.Economy;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Initializes InventoryService and MergeService after AutoSetupBootstrap.IsReady.
    /// Attach alongside AutoSetupBootstrap (SceneSetupUtility does this automatically).
    /// No configs needed at start — designer adds MergeConfigSO assets to MergeService
    /// via the Inspector or at runtime.
    /// </summary>
    [DefaultExecutionOrder(-489)]
    [AddComponentMenu("Endless Engine/Merge Bootstrap")]
    public class MergeBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var bootstrap = GetComponent<AutoSetupBootstrap>();
            if (bootstrap != null)
                yield return new WaitUntil(() => bootstrap.IsReady);

            var inventory = GetComponentInChildren<InventoryService>(includeInactive: true);
            var merge     = GetComponentInChildren<MergeService>(includeInactive: true);

            if (inventory == null || merge == null)
            {
                Debug.LogWarning("[MergeBootstrap] InventoryService or MergeService not found in children.");
                yield break;
            }

            // Initialize with no items / no merge rules — functional skeleton ready for content
            inventory.Initialize(new EndlessEngine.Config.ItemConfigSO[0]);
            merge.Initialize(new EndlessEngine.Config.MergeConfigSO[0], inventory, bootstrap?.Economy);

            if (bootstrap?.Save != null)
            {
                bootstrap.Save.RegisterStateProvider(inventory);
                // MergeService is not ISaveStateProvider by default; inventory holds item state
            }

            Debug.Log("[MergeBootstrap] MergeService + InventoryService ready.");
        }
    }
}
