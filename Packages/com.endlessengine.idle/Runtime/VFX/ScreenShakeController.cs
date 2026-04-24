using System.Collections;
using UnityEngine;
using EndlessEngine.Health;

namespace EndlessEngine.VFX
{
    /// <summary>
    /// Applies a brief camera shake when the player takes damage.
    ///
    /// Attach to the Main Camera GameObject. Records the camera's base position
    /// and applies a random offset each frame for <see cref="_shakeDuration"/> seconds.
    ///
    /// GDD: design/gdd/vfx-feedback-system.md Rule 10
    /// Sprint: S4-10
    /// </summary>
    public class ScreenShakeController : MonoBehaviour
    {
        [Tooltip("Shake duration in seconds. GDD default: 0.2s.")]
        [SerializeField] private float _shakeDuration  = 0.2f;

        [Tooltip("Maximum pixel offset during shake. GDD default: 0.15 world units.")]
        [SerializeField] private float _shakeMagnitude = 0.15f;

        private Vector3   _originalPosition;
        private Coroutine _shakeRoutine;
        private bool      _shaking;

        private void Awake()
        {
            _originalPosition = transform.localPosition;
        }

        private void OnEnable()
        {
            PlayerHealthComponent.OnPlayerHPChanged += OnPlayerHPChanged;
        }

        private void OnDisable()
        {
            PlayerHealthComponent.OnPlayerHPChanged -= OnPlayerHPChanged;
        }

        private void OnPlayerHPChanged(float currentHP, float maxHP)
        {
            // Only shake on damage (HP decreased), not on heal
            if (currentHP < maxHP)
                TriggerShake();
        }

        private void TriggerShake()
        {
            if (_shakeRoutine != null)
                StopCoroutine(_shakeRoutine);

            _shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            _shaking = true;
            float elapsed = 0f;

            while (elapsed < _shakeDuration)
            {
                float t = elapsed / _shakeDuration;
                // Magnitude damped linearly toward 0
                float magnitude = _shakeMagnitude * (1f - t);

                transform.localPosition = _originalPosition + (Vector3)Random.insideUnitCircle * magnitude;

                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.localPosition = _originalPosition;
            _shaking      = false;
            _shakeRoutine = null;
        }

        private void OnDestroy()
        {
            // Restore position if destroyed mid-shake
            transform.localPosition = _originalPosition;
        }
    }
}
