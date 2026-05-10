using System.Collections;
using UnityEngine;
using EndlessEngine.Prestige;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Wires PrestigeStateManager's SaveService reference and registers it as ISaveStateProvider
    /// after AutoSetupBootstrap.IsReady.
    /// Attach alongside AutoSetupBootstrap (SceneSetupUtility does this automatically for
    /// PrestigeHeavy and any game type with HasPrestige = true).
    /// </summary>
    [DefaultExecutionOrder(-489)]
    [AddComponentMenu("Endless Engine/Prestige Bootstrap")]
    public class PrestigeBootstrap : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var bootstrap = GetComponent<AutoSetupBootstrap>();
            if (bootstrap != null)
                yield return new WaitUntil(() => bootstrap.IsReady);

            var pm = GetComponent<PrestigeStateManager>();
            if (pm == null) pm = FindFirstObjectByType<PrestigeStateManager>();
            if (pm == null)
            {
                Debug.LogWarning("[PrestigeBootstrap] No PrestigeStateManager found in scene.");
                yield break;
            }

            // Inject economy so CanPrestige can check MinGoldToPrestige
            if (bootstrap?.Economy != null)
                pm.InjectEconomy(bootstrap.Economy);

            // Wire SaveService via reflection (it is a [SerializeField] private field)
            if (bootstrap?.Save != null)
            {
                var field = typeof(PrestigeStateManager)
                    .GetField("_saveService",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(pm, bootstrap.Save);

                bootstrap.Save.RegisterStateProvider(pm);
            }

            Debug.Log("[PrestigeBootstrap] PrestigeStateManager wired.");
        }
    }
}
