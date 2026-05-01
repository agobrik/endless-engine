using System;
using UnityEngine;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// MonoBehaviour placed on every harvestable world object (tree, mineral, etc.).
    /// Registers itself with HarvestNodeRegistry on Awake; deregisters on destroy.
    ///
    /// Requires a Collider2D (trigger) so HarvestCursor can detect overlap via Physics2D.
    /// HarvestLoopService calls ApplyDamage(); this class owns the HP state and respawn timer.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HarvestNode : MonoBehaviour, IHarvestNode
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private HarvestNodeConfigSO _config;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fired when this node is fully depleted. Payload: this node.</summary>
        public event Action<HarvestNode> OnDepleted;

        /// <summary>Fired when this node respawns. Payload: this node.</summary>
        public event Action<HarvestNode> OnRespawned;

        // ── IHarvestNode ──────────────────────────────────────────────────────────

        public string               NodeId      => _config != null ? _config.NodeId : string.Empty;
        public HarvestNodeConfigSO  Config      => _config;
        public bool                 IsAlive     => _currentHP > 0f;
        public float                CurrentHP   => _currentHP;
        public Vector2              WorldPosition => (Vector2)transform.position;

        // ── Save/load state (read by HarvestLoopService.OnBeforeSave) ────────────

        public bool  IsRespawning            => _isRespawning;
        public float RespawnSecondsRemaining => _respawnTimer;

        // ── Private state ─────────────────────────────────────────────────────────

        private float _currentHP;
        private float _respawnTimer;
        private bool  _isRespawning;

        // Visual root — toggled on/off during respawn (child objects stay put)
        private GameObject _visual;

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"[HarvestNode] Config is null on '{name}'. Assign a HarvestNodeConfigSO.", this);
                return;
            }

            _currentHP = _config.MaxHP;
            _visual    = transform.childCount > 0 ? transform.GetChild(0).gameObject : gameObject;

            HarvestNodeRegistry.Register(this);
        }

        private void OnDestroy()
        {
            HarvestNodeRegistry.Unregister(this);
        }

        private void Update()
        {
            if (!_isRespawning) return;

            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
                Respawn();
        }

        // ── IHarvestNode ──────────────────────────────────────────────────────────

        public float ApplyDamage(float amount)
        {
            if (!IsAlive) return 0f;

            float actual = Mathf.Min(amount, _currentHP);
            _currentHP -= actual;

            if (_currentHP <= 0f)
                Deplete();

            return actual;
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private void Deplete()
        {
            _currentHP = 0f;

            if (_config.DepletionVFXPrefab != null)
                Instantiate(_config.DepletionVFXPrefab, transform.position, Quaternion.identity);

            SetVisible(false);

            _isRespawning = true;
            _respawnTimer = _config.RespawnSeconds;

            OnDepleted?.Invoke(this);
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
            if (_visual != null)
                _visual.SetActive(visible);

            // Disable collider while respawning so cursor stops detecting it
            var col = GetComponent<Collider2D>();
            if (col != null)
                col.enabled = visible;
        }

        /// <summary>
        /// Restores respawn state after a save load.
        /// Called by HarvestLoopService.OnAfterLoad for each node in HarvestNodeRegistry.
        /// </summary>
        public void RestoreFromSave(HarvestNodeSaveEntry entry)
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
    }
}
