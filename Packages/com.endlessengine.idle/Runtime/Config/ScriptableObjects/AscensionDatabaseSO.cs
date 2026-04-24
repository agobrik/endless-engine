using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Container for all prestige layer configs in ascending order.
    /// Index 0 = standard prestige. Index 1+ = ascension tiers.
    ///
    /// Assign to AscensionStateManager in Inspector.
    /// Create via: Tools → Endless Engine → Create Ascension Database
    /// </summary>
    [CreateAssetMenu(
        menuName = "Endless Engine/Prestige/Ascension Database",
        fileName = "AscensionDatabase")]
    public class AscensionDatabaseSO : ScriptableObject
    {
        [Tooltip("Prestige layers in ascending order. Index 0 = standard prestige.")]
        public PrestigeLayerConfigSO[] Layers = new PrestigeLayerConfigSO[0];

        /// <summary>Returns the layer config at the given index, or null.</summary>
        public PrestigeLayerConfigSO GetLayer(int layerIndex)
        {
            if (Layers == null || layerIndex < 0 || layerIndex >= Layers.Length) return null;
            return Layers[layerIndex];
        }

        /// <summary>Number of layers defined.</summary>
        public int LayerCount => Layers?.Length ?? 0;
    }
}
