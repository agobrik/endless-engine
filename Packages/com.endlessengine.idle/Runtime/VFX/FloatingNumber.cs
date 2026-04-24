using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;

namespace EndlessEngine.VFX
{
    /// <summary>
    /// Single pooled floating damage number.
    /// Spawned by <see cref="VFXController"/> — returns to pool after <see cref="FloatDuration"/>.
    ///
    /// Uses TextMeshPro for world-space text rendering.
    /// ADR: ADR-0014 — VFX Object Pool
    /// GDD: design/gdd/vfx-feedback-system.md Rules 1–5
    /// </summary>
    [RequireComponent(typeof(TextMeshPro))]
    public class FloatingNumber : MonoBehaviour
    {
        [Tooltip("Upward travel distance in world units over the full float duration.")]
        [SerializeField] private float _floatDistance = 1.2f;

        [Tooltip("Animation curve for the float (Y offset 0→1 over lifetime).")]
        [SerializeField] private AnimationCurve _floatCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private TextMeshPro  _tmp;
        private float        _lifetime;
        private Vector3      _startPos;
        private float        _elapsed;
        private Coroutine    _floatRoutine;

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
        }

        /// <summary>
        /// Activates this number with the given parameters.
        /// Called by the pool on checkout.
        /// </summary>
        public void Spawn(long damage, bool isCrit, Vector2 worldPos, float duration)
        {
            _lifetime = duration;
            _startPos = worldPos;
            _elapsed  = 0f;

            transform.position = worldPos;

            // Style per GDD Rule 2
            if (isCrit)
            {
                _tmp.text      = damage.ToString();
                _tmp.color     = new Color(1f, 0.85f, 0.1f); // gold/yellow
                _tmp.fontSize  = 6f;
                transform.localScale = Vector3.one * 1.5f;
            }
            else
            {
                _tmp.text      = damage.ToString();
                _tmp.color     = Color.white;
                _tmp.fontSize  = 4f;
                transform.localScale = Vector3.one;
            }

            gameObject.SetActive(true);

            if (_floatRoutine != null) StopCoroutine(_floatRoutine);
            _floatRoutine = StartCoroutine(FloatAndReturn());
        }

        private IEnumerator FloatAndReturn()
        {
            float elapsed = 0f;

            while (elapsed < _lifetime)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _lifetime);

                float yOffset = _floatCurve.Evaluate(t) * _floatDistance;
                transform.position = _startPos + new Vector3(0f, yOffset, 0f);

                // Fade out in last 25% of lifetime
                float alpha = t < 0.75f ? 1f : Mathf.InverseLerp(1f, 0.75f, t);
                var col = _tmp.color;
                col.a = alpha;
                _tmp.color = col;

                yield return null;
            }

            gameObject.SetActive(false);
            _floatRoutine = null;
        }
    }
}
