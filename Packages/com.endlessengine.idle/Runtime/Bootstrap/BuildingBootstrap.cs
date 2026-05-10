using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Building;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Initializes BuildingService after AutoSetupBootstrap.IsReady.
    /// Attach alongside AutoSetupBootstrap (SceneSetupUtility does this automatically).
    /// </summary>
    [DefaultExecutionOrder(-489)]
    [AddComponentMenu("Endless Engine/Building Bootstrap")]
    public class BuildingBootstrap : MonoBehaviour
    {
        [SerializeField] private BuildingConfigSO _starterConfig;

        private IEnumerator Start()
        {
            var bootstrap = GetComponent<AutoSetupBootstrap>();
            if (bootstrap != null)
                yield return new WaitUntil(() => bootstrap.IsReady);

            var building = GetComponentInChildren<BuildingService>(includeInactive: true);
            if (building == null)
            {
                Debug.LogWarning("[BuildingBootstrap] No BuildingService found in children.");
                yield break;
            }

            var configs = _starterConfig != null
                ? new BuildingConfigSO[] { _starterConfig }
                : new BuildingConfigSO[0];

            building.Initialize(configs, bootstrap?.Economy);

            if (bootstrap?.Save != null)
                bootstrap.Save.RegisterStateProvider(building);

            Debug.Log("[BuildingBootstrap] BuildingService ready.");
        }
    }
}
