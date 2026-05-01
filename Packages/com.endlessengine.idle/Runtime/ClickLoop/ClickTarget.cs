using System;
using UnityEngine;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// MonoBehaviour on every clickable world target (coin bag, crystal, monster, etc.).
    /// Registers with ClickTargetRegistry on Awake; deregisters on destroy.
    ///
    /// ClickLoopService calls ApplyDamage() on each detected click.
    /// This class owns HP state, destruction VFX, and respawn timer.
    ///
    /// Create inactive → inject _config via reflection or Inspector → SetActive(true).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ClickTarget : MonoBehaviour, IClickTarget
    {
        [SerializeField] private ClickTargetConfigSO _config;

        public event Action<ClickTarget> OnDestroyed;
        public event Action<ClickTarget> OnRespawned;

        // ── IClickTarget ──────────────────────────────────────────────────────────

        public string              TargetId      => _config != null ? _config.TargetId : string.Empty;
        public ClickTargetConfigSO Config        => _config;
        public bool                IsAlive       => _currentHP > 0f;
        public float               CurrentHP     => _currentHP;
        public Vector2             WorldPosition => (Vector2)transform.position;

        // ── Save-accessible state ─────────────────────────────────────────────────

        public bool  IsRespawning            => _isRespawning;
        public float RespawnSecondsRemaining => _respawnTimer;

        // ── Private state ─────────────────────────────────────────────────────────

        private float      _currentHP;
        private float      _respawnTimer;
        private bool       _isRespawning;
        private GameObject _visual;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[ClickTarget] Config is null on '{name}'.", this);
                return;
            }
            _currentHP = _config.MaxHP;
            _visual    = transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject;
            ClickTargetRegistry.Register(this);
        }

        private void OnDestroy() => ClickTargetRegistry.Unregister(this);

        private void Update()
        {
            if (!_isRespawning) return;
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f) Respawn();
        }

        // ── IClickTarget ──────────────────────────────────────────────────────────

        public float ApplyDamage(float amount)
        {
            if (!IsAlive) return 0f;
            float actual = Mathf.Min(amount, _currentHP);
            _currentHP -= actual;
            if (_currentHP <= 0f) Deplete();
            return actual;
        }

        // ── Save/Load ─────────────────────────────────────────────────────────────

        public void RestoreFromSave(ClickTargetSaveEntry entry)
        {
            if (entry == null) return;
            if (entry.IsRespawning && entry.RespawnSecondsRemaining > 0f)
            {
                _currentHP    = 0f;
                _isRespawning = true;
                _respawnTimer = entry.RespawnSecondsRemaining;
                SetVisible(false);
            }
            else
            {
                _currentHP    = _config != null ? _config.MaxHP : 0f;
                _isRespawning = false;
                _respawnTimer = 0f;
                SetVisible(true);
            }
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void Deplete()
        {
            _currentHP = 0f;
            if (_config.DestructionVFXPrefab != null)
                Instantiate(_config.DestructionVFXPrefab, transform.position, Quaternion.identity);
            SetVisible(false);
            _isRespawning = true;
            _respawnTimer = _config.RespawnSeconds;
            OnDestroyed?.Invoke(this);
        }

        private void Respawn()
        {
            _isRespawning = false;
            _currentHP    = _config.MaxHP;
            SetVisible(true);
            OnRespawned?.Invoke(this);
        }

        private void SetVisible(bool visible)
        {
            if (_visual != null) _visual.SetActive(visible);
            var col = GetComponent<Collider2D>();
            if (col != null) col.enabled = visible;
        }
    }
}
