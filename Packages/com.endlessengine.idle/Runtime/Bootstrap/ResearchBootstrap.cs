using System.Collections;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Flow;
using EndlessEngine.Research;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Initializes ResearchService and hooks it into TickEngine after AutoSetupBootstrap.IsReady.
    /// Attach alongside AutoSetupBootstrap (SceneSetupUtility does this automatically).
    /// </summary>
    [DefaultExecutionOrder(-489)]
    [AddComponentMenu("Endless Engine/Research Bootstrap")]
    public class ResearchBootstrap : MonoBehaviour
    {
        [SerializeField] private ResearchTreeConfigSO _researchTree;

        private ResearchService _research;

        private IEnumerator Start()
        {
            var bootstrap = GetComponent<AutoSetupBootstrap>();
            if (bootstrap != null)
                yield return new WaitUntil(() => bootstrap.IsReady);

            _research = GetComponentInChildren<ResearchService>(includeInactive: true);
            if (_research == null)
            {
                Debug.LogWarning("[ResearchBootstrap] No ResearchService found in children.");
                yield break;
            }

            var trees = _researchTree != null
                ? new ResearchTreeConfigSO[] { _researchTree }
                : new ResearchTreeConfigSO[0];

            _research.Initialize(trees, bootstrap?.Economy, currencyService: null);

            TickEngine.OnTick += _research.OnTick;

            if (bootstrap?.Save != null)
                bootstrap.Save.RegisterStateProvider(_research);

            Debug.Log("[ResearchBootstrap] ResearchService ready.");
        }

        private void OnDestroy()
        {
            if (_research != null)
                TickEngine.OnTick -= _research.OnTick;
        }
    }
}
