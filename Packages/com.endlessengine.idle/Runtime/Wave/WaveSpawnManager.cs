using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Damage;
using EndlessEngine.Enemy;
using EndlessEngine.Health;
using EndlessEngine.SaveAndLoad;
using EndlessEngine.Telemetry;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.Wave
{
    /// <summary>
    /// Orchestrates the enemy lifecycle for each wave:
    ///   - Builds spawn manifest (enemy count formula, archetype weights)
    ///   - Caches wave-scaled stats into EnemyRuntimeData for each archetype
    ///   - Trickle-spawns enemies; pauses when HardCapEnemiesOnScreen is active
    ///   - Detects wave-clear; raises OnWaveComplete, OnUpgradeSelectionTriggered, save milestone
    ///   - Manages enemy pool (pre-allocated at session start)
    ///
    /// ADR: ADR-0011 — Enemy Pool and Wave Scaling
    /// ADR: ADR-0008 — Physics 2D Movement (spawn positions at arena edge)
    /// </summary>
    public class WaveSpawnManager : MonoBehaviour, ISaveStateProvider
    {
        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public int ProviderOrder => SaveConstants.SaveProviderOrder.WaveAndCombat;

        // ── Events ────────────────────────────────────────────────────────────────

        /// <summary>Fires at the start of each wave with the wave number (1-indexed).</summary>
        public static event Action<int> OnWaveStarted;

        /// <summary>Fires when all enemies for a wave are dead.</summary>
        public static event Action<int> OnWaveComplete;

        /// <summary>Fires every UpgradeSelectionWaveInterval waves after clear.</summary>
        public static event Action OnUpgradeSelectionTriggered;

        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private GameObject _enemyPrefab;
        [SerializeField] private Transform  _spawnParent;  // optional: keeps hierarchy tidy

        // ── Dependencies ──────────────────────────────────────────────────────────

        private EnemyManager _enemyManager;
        private IWaveSaveNotifier _saveNotifier;
        private HealthSystem _healthSystem;

        // ── Config (cached at wave start) ─────────────────────────────────────────

        private WaveConfigSO     _waveConfig;
        private EnemyStatConfigSO _enemyConfig;

        // ── Runtime state ──────────────────────────────────────────────────────────

        private int  _currentWaveNumber = 1;
        private int  _aliveEnemyCount;
        private int  _spawnedThisWave;
        private int  _manifestCount;
        private bool _initialized;
        private bool _stopped;

        private WaveState _state = WaveState.Idle;

        // ── Manifest queue ────────────────────────────────────────────────────────

        /// <summary>Enemy archetypes queued for trickle spawning this wave.</summary>
        private readonly Queue<EnemyArchetype> _spawnQueue = new Queue<EnemyArchetype>(200);

        // ── Public accessors ──────────────────────────────────────────────────────

        public int  CurrentWaveNumber => _currentWaveNumber;
        public WaveState State        => _state;

        // ── Initialization ────────────────────────────────────────────────────────

        /// <summary>Inject dependencies. Call before OnSaveLoaded fires.</summary>
        public void Initialize(EnemyManager enemyManager, IWaveSaveNotifier saveNotifier, HealthSystem healthSystem = null)
        {
            _enemyManager = enemyManager;
            _saveNotifier = saveNotifier;
            _healthSystem = healthSystem;
        }

        // ── ISaveStateProvider ────────────────────────────────────────────────────

        public void OnBeforeSave(SaveData saveData)
        {
            saveData.WaveNumber = _currentWaveNumber;
        }

        public void OnAfterLoad(SaveData saveData)
        {
            _waveConfig   = ConfigRegistry.Wave;
            _enemyConfig  = ConfigRegistry.Enemy;
            _currentWaveNumber = Mathf.Max(1, saveData.WaveNumber > 0 ? saveData.WaveNumber : 1);
            _initialized  = true;
        }

        // ── Wave Lifecycle ────────────────────────────────────────────────────────

        /// <summary>
        /// Begin the first (or resumed) wave. Call after OnAfterLoad completes.
        /// </summary>
        public void StartFirstWave()
        {
            if (!_initialized) return;
            _stopped = false;
            StartCoroutine(WaveLifecycleCoroutine());
        }

        /// <summary>
        /// Stops wave spawning. Called by GameFlowStateMachine when run ends.
        /// The current wave coroutine will exit at the next loop iteration.
        /// </summary>
        public void StopWaves()
        {
            _stopped = true;
            _state   = WaveState.Idle;
            Debug.Log($"[WaveSpawnManager] Waves stopped at wave {_currentWaveNumber}.");
        }

        /// <summary>
        /// Resets wave number to 1 and clears state. Call before starting a new run.
        /// </summary>
        public void ResetForNewRun()
        {
            _stopped             = false;
            _currentWaveNumber   = 1;
            _aliveEnemyCount     = 0;
            _spawnedThisWave     = 0;
            _manifestCount       = 0;
            _spawnQueue.Clear();
            _state               = WaveState.Idle;
        }

        private IEnumerator WaveLifecycleCoroutine()
        {
            while (!_stopped)
            {
                yield return StartCoroutine(RunWave(_currentWaveNumber));

                if (_stopped) yield break;

                // Wave clear processing
                _state = WaveState.WaveComplete;
                TelemetryService.Track(TelemetryEvents.WaveCompleted,
                    new System.Collections.Generic.Dictionary<string, object> { { "wave", _currentWaveNumber } });
                OnWaveComplete?.Invoke(_currentWaveNumber);

                // Upgrade selection milestone
                if (_currentWaveNumber % _waveConfig.UpgradeSelectionWaveInterval == 0)
                    OnUpgradeSelectionTriggered?.Invoke();

                // Save milestone
                if (_currentWaveNumber % _waveConfig.WaveSaveMilestoneInterval == 0)
                    _saveNotifier?.NotifyWaveMilestone(_currentWaveNumber);

                _currentWaveNumber++;

                // Infinite mode: TotalWavesPerRun = -1 means run forever
                if (_waveConfig.TotalWavesPerRun > 0 && _currentWaveNumber > _waveConfig.TotalWavesPerRun)
                {
                    _state = WaveState.RunComplete;
                    yield break;
                }

                // Transition delay
                yield return new WaitForSeconds(_waveConfig.WaveTransitionDelaySeconds);
            }
        }

        private IEnumerator RunWave(int waveNumber)
        {
            _state = WaveState.WaveBuilding;

            // Cache fresh config
            _waveConfig  = ConfigRegistry.Wave;
            _enemyConfig = ConfigRegistry.Enemy;

            // Build spawn manifest
            int manifestCount = ComputeEnemyCount(waveNumber, _waveConfig);
            BuildSpawnQueue(manifestCount, waveNumber, _waveConfig, _enemyConfig);
            _manifestCount = manifestCount;
            _spawnedThisWave = 0;
            _aliveEnemyCount = 0;

            OnWaveStarted?.Invoke(waveNumber);
            _state = WaveState.Spawning;

            float waveTimer = 0f;
            float spawnTimer = 0f;

            // Main wave loop: spawn + watch for clear
            while (_aliveEnemyCount > 0 || _spawnQueue.Count > 0)
            {
                if (_stopped) yield break;

                waveTimer += Time.deltaTime;
                if (waveTimer >= _waveConfig.WaveDurationSeconds)
                {
                    Debug.LogWarning($"[WaveSpawnManager] Wave {waveNumber} force-cleared after {_waveConfig.WaveDurationSeconds}s.");
                    break;
                }

                // Trickle spawn
                if (_spawnQueue.Count > 0)
                {
                    int activeCount = _enemyManager != null ? _enemyManager.ActiveEnemies.Count : 0;
                    if (activeCount >= _waveConfig.HardCapEnemiesOnScreen)
                    {
                        _state = WaveState.SpawnPaused;
                    }
                    else
                    {
                        _state = WaveState.Spawning;
                        spawnTimer += Time.deltaTime;
                        if (spawnTimer >= _waveConfig.SpawnIntervalSeconds)
                        {
                            spawnTimer = 0f;
                            SpawnNextEnemy(waveNumber, _spawnQueue.Dequeue());
                        }
                    }
                }
                else
                {
                    _state = WaveState.WaitingForClear;
                }

                yield return null;
            }
        }

        private void SpawnNextEnemy(int waveNumber, EnemyArchetype archetype)
        {
            var agent = CreateAgentForArchetype(archetype, waveNumber);
            if (agent == null) return;

            // Register HealthComponent with HealthSystem for damage routing
            if (_healthSystem != null)
            {
                var health = new HealthComponent();
                health.Initialize(agent.InstanceId, agent.RuntimeData.ScaledHP, "enemy_death");
                _healthSystem.Register(health);
            }

            _spawnedThisWave++;
            _aliveEnemyCount++;
            _enemyManager?.SpawnEnemy(agent);
        }

        /// <summary>Called by EnemyManager.OnEnemyKilled handler — decrements alive count for wave-clear detection.</summary>
        public void OnEnemyDied()
        {
            _aliveEnemyCount = Mathf.Max(0, _aliveEnemyCount - 1);
        }

        // ── Formulas (static — directly unit-testable) ────────────────────────────

        /// <summary>
        /// Computes total enemies for a wave.
        /// Formula: Floor(BaseEnemyCountPerWave × (EnemyCountScalingFactor ^ (WaveNumber - 1)))
        /// Capped at HardCapEnemiesOnScreen × 3.
        /// </summary>
        public static int ComputeEnemyCount(int waveNumber, WaveConfigSO config)
        {
            if (waveNumber <= 0) return config.BaseEnemyCountPerWave;
            float raw = config.BaseEnemyCountPerWave * Mathf.Pow(config.EnemyCountScalingFactor, waveNumber - 1);
            int count = Mathf.FloorToInt(raw);
            int cap   = config.HardCapEnemiesOnScreen * 3;
            return Mathf.Clamp(count, 1, cap);
        }

        /// <summary>
        /// Computes wave-scaled HP/Damage/Contact for a given archetype and wave number.
        /// Delegates to WaveScalingCalculator (already tested in damage-system tests).
        /// </summary>
        public static EnemyRuntimeData ComputeRuntimeData(
            int waveNumber,
            float baseHP,
            float baseDamage,
            float baseContact,
            float scalingExponent,
            int entityId = 0)
        {
            return new EnemyRuntimeData
            {
                EntityID          = entityId,
                ScaledHP          = WaveScalingCalculator.ComputeScaledValue(baseHP,      waveNumber, scalingExponent),
                ScaledDamage      = WaveScalingCalculator.ComputeScaledValue(baseDamage,  waveNumber, scalingExponent),
                ScaledContactDamage = WaveScalingCalculator.ComputeScaledValue(baseContact, waveNumber, scalingExponent),
                ScalingExponent   = scalingExponent,
            };
        }

        // ── Archetype logic ───────────────────────────────────────────────────────

        private void BuildSpawnQueue(int count, int waveNumber, WaveConfigSO waveConfig, EnemyStatConfigSO enemyConfig)
        {
            _spawnQueue.Clear();
            bool isBossWave  = waveConfig.BossWaveInterval > 0 && waveNumber % waveConfig.BossWaveInterval == 0;
            bool isEliteWave = !isBossWave && waveConfig.EliteWaveInterval > 0 && waveNumber % waveConfig.EliteWaveInterval == 0;

            for (int i = 0; i < count; i++)
            {
                // MVP: simple archetype selection — all standard enemies use Runner archetype
                // Post-MVP: weighted selection from enemyConfig.ArchetypeWeights
                EnemyArchetype archetype = EnemyArchetype.Runner;
                if (isBossWave && i == count - 1)
                    archetype = EnemyArchetype.Boss;
                else if (isEliteWave && i == 0)
                    archetype = EnemyArchetype.Elite;

                _spawnQueue.Enqueue(archetype);
            }
        }

        // ── Agent factory ─────────────────────────────────────────────────────────

        /// <summary>Override for testing — replaces the pool-based factory with a test factory.</summary>
        public Func<EnemyArchetype, int, EnemyAgent> AgentFactory;

        private EnemyAgent CreateAgentForArchetype(EnemyArchetype archetype, int waveNumber)
        {
            if (AgentFactory != null)
                return AgentFactory(archetype, waveNumber);

            var runtimeData = ComputeRuntimeData(
                waveNumber,
                baseHP:          _enemyConfig != null ? _enemyConfig.BaseMaxHP : 100f,
                baseDamage:      _enemyConfig != null ? _enemyConfig.BaseAttackDamage : 10f,
                baseContact:     _enemyConfig != null ? _enemyConfig.BaseContactDamage : 5f,
                scalingExponent: _enemyConfig != null ? _enemyConfig.WaveScalingExponent : 1.2f
            );

            // Stat multiplier for elite/boss archetypes
            float statMult = 1f;
            if (archetype == EnemyArchetype.Elite && _waveConfig != null)
                statMult = _waveConfig.EliteStatMultiplier;
            else if (archetype == EnemyArchetype.Boss && _waveConfig != null)
                statMult = _waveConfig.EliteStatMultiplier * 2f;

            runtimeData.ScaledHP     = (long)(runtimeData.ScaledHP     * statMult);
            runtimeData.ScaledDamage = (long)(runtimeData.ScaledDamage * statMult);

            // Spawn position: random point on arena edge
            Rect bounds;
            try { bounds = ConfigRegistry.Realm.ArenaBounds; }
            catch { bounds = new Rect(-10f, -6f, 20f, 12f); }
            Vector2 spawnPos = GetRandomEdgePosition(bounds);

            // Instantiate prefab if available, otherwise create a bare GameObject
            Rigidbody2D rb = null;
            int instanceId = 0;
            if (_enemyPrefab != null)
            {
                Transform parent = _spawnParent != null ? _spawnParent : null;
                GameObject go = UnityEngine.Object.Instantiate(_enemyPrefab, spawnPos, Quaternion.identity, parent);
                rb = go.GetComponent<Rigidbody2D>();
                if (rb == null)
                    rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                instanceId  = go.GetInstanceID();
            }

            float moveSpeed = _enemyConfig != null ? _enemyConfig.MoveSpeed : 3f;
            if (archetype == EnemyArchetype.Boss) moveSpeed *= 0.6f;

            runtimeData.EntityID = instanceId;

            return new EnemyAgent
            {
                InstanceId     = instanceId,
                Rigidbody      = rb,
                RuntimeData    = runtimeData,
                MoveSpeed      = moveSpeed * statMult,
                AttackInterval = _enemyConfig != null ? _enemyConfig.AttackInterval : 2f,
                AttackRange    = _enemyConfig != null ? _enemyConfig.AttackRange    : 0.5f,
                AttackTimer    = _enemyConfig != null ? _enemyConfig.AttackInterval : 2f,
                GoldDropAmount = (long)Mathf.Max(1, waveNumber * (_enemyConfig != null ? _enemyConfig.BaseMaxHP * 0.1f : 5f)),
                State          = EnemyState.Moving,
                Position       = spawnPos,
            };
        }

        private static Vector2 GetRandomEdgePosition(Rect bounds)
        {
            // Spawn on one of 4 edges randomly
            int edge = UnityEngine.Random.Range(0, 4);
            return edge switch
            {
                0 => new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMax), // top
                1 => new Vector2(UnityEngine.Random.Range(bounds.xMin, bounds.xMax), bounds.yMin), // bottom
                2 => new Vector2(bounds.xMin, UnityEngine.Random.Range(bounds.yMin, bounds.yMax)), // left
                _ => new Vector2(bounds.xMax, UnityEngine.Random.Range(bounds.yMin, bounds.yMax)), // right
            };
        }

        // ── Test injection ────────────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Direct state injection for unit tests.</summary>
        public void InjectStateForTesting(int waveNumber, int aliveCount)
        {
            _currentWaveNumber = waveNumber;
            _aliveEnemyCount   = aliveCount;
            _initialized       = true;
        }

        /// <summary>Directly call ComputeEnemyCount without a MonoBehaviour for unit tests.</summary>
        public int ComputeEnemyCountForTesting(int waveNumber)
            => ComputeEnemyCount(waveNumber, _waveConfig ?? ConfigRegistry.Wave);

        /// <summary>Fires OnWaveStarted for testing wave start event plumbing.</summary>
        public void SimulateWaveStartForTesting(int waveNumber)
        {
            OnWaveStarted?.Invoke(waveNumber);
        }

        /// <summary>Fires OnWaveComplete (and OnUpgradeSelectionTriggered if applicable) for testing.</summary>
        public void SimulateWaveClearForTesting(int waveNumber)
        {
            OnWaveComplete?.Invoke(waveNumber);
            if (waveNumber % (_waveConfig?.UpgradeSelectionWaveInterval ?? 3) == 0)
                OnUpgradeSelectionTriggered?.Invoke();
            if (waveNumber % (_waveConfig?.WaveSaveMilestoneInterval ?? 10) == 0)
                _saveNotifier?.NotifyWaveMilestone(waveNumber);
        }

        /// <summary>Clears all static event subscribers. Call in TearDown to prevent test bleed.</summary>
        public static void ClearStaticSubscribersForTesting()
        {
            OnWaveStarted              = null;
            OnWaveComplete             = null;
            OnUpgradeSelectionTriggered = null;
        }
#endif
    }

    // ── Supporting types ──────────────────────────────────────────────────────────

    public enum WaveState
    {
        Idle,
        WaveBuilding,
        Spawning,
        SpawnPaused,
        WaitingForClear,
        WaveComplete,
        RunComplete,
    }

    public enum EnemyArchetype
    {
        Runner,
        Heavy,
        Elite,
        Boss,
    }

}
