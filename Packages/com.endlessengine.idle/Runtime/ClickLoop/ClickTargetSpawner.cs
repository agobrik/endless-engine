using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Spawns ClickTarget instances at defined world positions.
    /// Respawn is handled by ClickTarget itself.
    /// </summary>
    public class ClickTargetSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnPoint
        {
            public ClickTargetConfigSO Config;
            public Vector2             WorldPosition;
        }

        [SerializeField] private List<SpawnPoint> _spawnPoints = new();

        private readonly List<ClickTarget> _spawned = new();

        private void Start()  => SpawnAll();
        private void OnDestroy() => DespawnAll();

        public void SpawnAll()
        {
            foreach (var sp in _spawnPoints)
            {
                if (sp.Config?.Prefab == null)
                {
                    Debug.LogWarning("[ClickTargetSpawner] SpawnPoint has null Config or Prefab — skipped.", this);
                    continue;
                }

                var go = Instantiate(sp.Config.Prefab,
                    new Vector3(sp.WorldPosition.x, sp.WorldPosition.y, 0f), Quaternion.identity);
                go.name = $"ClickTarget_{sp.Config.TargetId}_{_spawned.Count}";

                var target = go.GetComponent<ClickTarget>();
                if (target == null)
                {
                    Debug.LogError($"[ClickTargetSpawner] Prefab '{sp.Config.Prefab.name}' has no ClickTarget component.", this);
                    Destroy(go);
                    continue;
                }
                _spawned.Add(target);
            }
        }

        public void DespawnAll()
        {
            foreach (var t in _spawned)
                if (t != null) Destroy(t.gameObject);
            _spawned.Clear();
        }
    }
}
