using UnityEngine;
using EndlessEngine.Economy;
using EndlessEngine.Modules;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Attach to any GameObject with a Collider2D to make it a gold-earning click target.
    ///
    /// If a ClickYieldService is in the scene this forwards clicks to it via
    /// SimulateClickForTesting (available in Editor and Development builds).
    /// As a fallback it awards gold directly from AutoSetupBootstrap.Economy
    /// so the clicker demo always works even without a configured ClickSourceConfigSO.
    /// </summary>
    [AddComponentMenu("Endless Engine/Click Target Handler")]
    public class ClickTargetHandler : MonoBehaviour
    {
        private AutoSetupBootstrap _bootstrap;
        private ClickYieldService  _clickService;
        private Vector3            _baseScale;
        private float              _scaleTimer;

        private void Start()
        {
            _bootstrap    = FindFirstObjectByType<AutoSetupBootstrap>();
            _clickService = FindFirstObjectByType<ClickYieldService>();
            _baseScale    = transform.localScale;
        }

        private void OnMouseDown()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_clickService != null)
            {
                _clickService.SimulateClickForTesting();
                AnimatePunch();
                return;
            }
#endif
            // Fallback: award 1 gold directly (works when ClickYieldService is absent)
            if (_bootstrap?.Economy != null)
                _bootstrap.Economy.AddResources(1);

            AnimatePunch();
        }

        private void Update()
        {
            if (_scaleTimer <= 0f) return;
            _scaleTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(_scaleTimer / 0.12f);
            transform.localScale = Vector3.LerpUnclamped(_baseScale * 0.88f, _baseScale, t);
        }

        private void AnimatePunch()
        {
            transform.localScale = _baseScale * 0.88f;
            _scaleTimer = 0.12f;
        }
    }
}
