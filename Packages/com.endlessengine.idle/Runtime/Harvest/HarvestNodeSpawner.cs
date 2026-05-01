using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Spawns HarvestNode instances in the world according to a spawn point list.
    /// Each spawn point has a config and a world position; the spawner instantiates
    /// the config's Prefab at that position and keeps a reference for cleanup.
    ///
    /// Respawn is handled by HarvestNode itself (timer + re-enable). This spawner
    /// handles initial placement and scene lifecycle only.
    ///
    /// Attach to a scene-persistent GameObject and populate SpawnPoints in the Inspector.
    /// </summary>
    public class HarvestNodeSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class SpawnPoint
        {
            public HarvestNodeConfigSO Config;
            public Vector2             WorldPosition;
        }

        [SerializeField] private List<SpawnPoint> _spawnPoints = new();

        private readonly List<HarvestNode> _spawnedNodes = new();

        private void Start()
        {
            SpawnAll();
        }

        private void OnDestroy()
        {
            DespawnAll();
        }

        public void SpawnAll()
        {
            foreach (var sp in _spawnPoints)
            {
                if (sp.Config == null || sp.Config.Prefab == null)
                {
                    Debug.LogWarning($"[HarvestNodeSpawner] SpawnPoint has null Config or Prefab — skipped.", this);
                    continue;
                }

                GameObject go = Instantiate(sp.Config.Prefab,
                                            new Vector3(sp.WorldPosition.x, sp.WorldPosition.y, 0f),
                                            Quaternion.identity);
                go.name = $"HarvestNode_{sp.Config.NodeId}_{_spawnedNodes.Count}";

                var node = go.GetComponent<HarvestNode>();
                if (node == null)
                {
                    Debug.LogError($"[HarvestNodeSpawner] Prefab '{sp.Config.Prefab.name}' has no HarvestNode component.", this);
                    Destroy(go);
                    continue;
                }

                _spawnedNodes.Add(node);
            }
        }

        public void DespawnAll()
        {
            foreach (var node in _spawnedNodes)
                if (node != null) Destroy(node.gameObject);
            _spawnedNodes.Clear();
        }
    }
}
