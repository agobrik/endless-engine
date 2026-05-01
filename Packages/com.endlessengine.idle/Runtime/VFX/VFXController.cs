using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;

namespace EndlessEngine.VFX
{
    /// <summary>
    /// Subscribes to game events and drives VFX feedback: floating damage numbers
    /// and enemy death particle bursts.
    ///
    /// Pool setup: assign <see cref="FloatingNumberPrefab"/> and <see cref="DeathParticlePrefab"/>
    /// in the Inspector. Pools are pre-warmed in Awake().
    ///
    /// ADR: ADR-0014 — VFX Object Pool
    /// GDD: design/gdd/vfx-feedback-system.md Rules 1–7
    /// Sprint: S4-03
    /// </summary>
    public class VFXController : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [Tooltip("FloatingNumber prefab — must have FloatingNumber + TextMeshPro components.")]
        [SerializeField] private FloatingNumber _floatingNumberPrefab;

        [Tooltip("Death particle prefab — must have ParticleSystem component.")]
        [SerializeField] private ParticleSystem _deathParticlePrefab;

        [Header("Pool Sizes (ADR-0014)")]
        [Tooltip("Pre-warmed floating number pool size. GDD default: 50.")]
        [SerializeField] private int _floatingNumberPoolSize = 50;

        [Tooltip("Pre-warmed death particle pool size. GDD default: 30.")]
        [SerializeField] private int _deathParticlePoolSize = 30;

        [Header("Timing")]
        [Tooltip("How long each floating number remains visible before returning to pool.")]
        [SerializeField] private float _floatDuration = 0.8f;

        // ── Pools ─────────────────────────────────────────────────────────────────

        private readonly List<FloatingNumber> _numberPool  = new List<FloatingNumber>(50);
        private readonly List<ParticleSystem> _particlePool = new List<ParticleSystem>(30);

        private int _numberPoolHead;    // round-robin index for oldest-recycle policy
        private int _particlePoolHead;

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            WarmNumberPool();
            WarmParticlePool();
        }

        private void OnEnable()
        {
            DamageSystem.OnDamageResolved += OnDamageResolved;
            EnemyManager.OnEnemyKilled   += OnEnemyKilled;
        }

        private void OnDisable()
        {
            DamageSystem.OnDamageResolved -= OnDamageResolved;
            EnemyManager.OnEnemyKilled   -= OnEnemyKilled;
        }

        // ── Pool Warm-up ──────────────────────────────────────────────────────────

        private void WarmNumberPool()
        {
            if (_floatingNumberPrefab == null) return;

            for (int i = 0; i < _floatingNumberPoolSize; i++)
            {
                var instance = Instantiate(_floatingNumberPrefab, transform);
                instance.gameObject.SetActive(false);
                _numberPool.Add(instance);
            }
        }

        private void WarmParticlePool()
        {
            if (_deathParticlePrefab == null) return;

            for (int i = 0; i < _deathParticlePoolSize; i++)
            {
                var instance = Instantiate(_deathParticlePrefab, transform);
                instance.gameObject.SetActive(false);
                _particlePool.Add(instance);
            }
        }

        // ── Event Handlers ────────────────────────────────────────────────────────

        private void OnDamageResolved(DamageHit hit)
        {
            // Only spawn numbers for player-to-enemy hits (AttackerType.Player)
            // Contact damage on player is handled separately (not in S4-03 scope)
            if (hit.AttackerType != AttackerType.Player) return;

            SpawnFloatingNumber(hit.FinalDamage, hit.IsCrit, hit.HitPosition);
        }

        private void OnEnemyKilled(EnemyAgent agent)
        {
            SpawnDeathParticle(agent.Position);
        }

        // ── Spawn Helpers ─────────────────────────────────────────────────────────

        private void SpawnFloatingNumber(long damage, bool isCrit, Vector2 worldPos)
        {
            if (_numberPool.Count == 0) return;

            // Find an inactive slot; fallback to oldest-recycle (round-robin)
            FloatingNumber slot = FindInactiveOrRecycle(_numberPool, ref _numberPoolHead);
            slot.Spawn(damage, isCrit, worldPos, _floatDuration);
        }

        /// <summary>
        /// Spawns a floating yield number at a harvest node's world position.
        /// Called by HarvestLoopService on each tick that awards yield.
        /// </summary>
        public void SpawnHarvestNumber(long amount, Vector2 worldPos)
            => SpawnFloatingNumber(amount, isCrit: false, worldPos);

        /// <summary>
        /// Spawns a floating yield number at a click target's world position.
        /// Critical clicks use the crit style (gold, larger).
        /// Called by ClickLoopService on each click that awards yield.
        /// </summary>
        public void SpawnClickNumber(long amount, bool isCrit, Vector2 worldPos)
            => SpawnFloatingNumber(amount, isCrit, worldPos);

        private void SpawnDeathParticle(Vector2 worldPos)
        {
            if (_particlePool.Count == 0) return;

            ParticleSystem slot = FindInactiveOrRecycle(_particlePool, ref _particlePoolHead);
            slot.transform.position = worldPos;
            slot.gameObject.SetActive(true);
            slot.Stop(withChildren: true, stopBehavior: ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Play(withChildren: true);

            // Auto-return to pool via coroutine (ADR-0014 recycling pattern)
            StartCoroutine(ReturnParticleAfterDuration(slot));
        }

        private System.Collections.IEnumerator ReturnParticleAfterDuration(ParticleSystem ps)
        {
            yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);
            ps.gameObject.SetActive(false);
        }

        // ── Pool Utility ──────────────────────────────────────────────────────────

        private static T FindInactiveOrRecycle<T>(List<T> pool, ref int head) where T : MonoBehaviour
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                    return pool[i];
            }
            T recycled = pool[head];
            head = (head + 1) % pool.Count;
            return recycled;
        }

        private static ParticleSystem FindInactiveOrRecycle(List<ParticleSystem> pool, ref int head)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                    return pool[i];
            }
            ParticleSystem recycled = pool[head];
            head = (head + 1) % pool.Count;
            return recycled;
        }
    }
}
