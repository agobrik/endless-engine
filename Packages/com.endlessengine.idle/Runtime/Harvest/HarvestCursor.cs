using System.Collections.Generic;
using UnityEngine;
using EndlessEngine.Config;
using EndlessEngine.Core;
using EndlessEngine.Input;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Tracks the mouse/touch world position and maintains the set of HarvestNodes
    /// currently within the harvest radius.
    ///
    /// Radius is driven by HarvestAreaConfigSO.BaseRadius scaled by the
    /// HarvestRadius stat from UpgradeApplicationSystem.
    ///
    /// HarvestLoopService reads OverlappingNodes each tick — it does NOT poll Physics2D.
    /// This class is the only place Physics2D.OverlapCircleNonAlloc is called.
    ///
    /// Attach to a persistent manager GameObject alongside HarvestLoopService.
    /// Inject IInputProvider from the bootstrapper.
    /// </summary>
    public class HarvestCursor : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────────

        [SerializeField] private HarvestAreaConfigSO _config;
        [SerializeField] private LayerMask           _harvestLayer;

        // ── Public state ──────────────────────────────────────────────────────────

        public Vector2 WorldPosition { get; private set; }

        /// <summary>
        /// Live set of alive nodes whose collider overlaps the current cursor radius.
        /// Updated every frame in Update(). HarvestLoopService reads this directly.
        /// Virtual so test subclasses can override without Physics2D.
        /// </summary>
        public virtual IReadOnlyList<IHarvestNode> OverlappingNodes => _overlapping;

        public virtual float EffectiveRadius { get; protected set; }

        // ── Private ───────────────────────────────────────────────────────────────

        private IInputProvider               _input;
        private readonly List<IHarvestNode>  _overlapping   = new();
        private readonly Collider2D[]        _hitBuffer     = new Collider2D[32];

        // ── Injection ─────────────────────────────────────────────────────────────

        /// <summary>Call from bootstrapper before first Update.</summary>
        public void Inject(IInputProvider input, HarvestAreaConfigSO config = null, LayerMask? harvestLayer = null)
        {
            _input = input;
            if (config != null) _config = config;
            if (harvestLayer.HasValue) _harvestLayer = harvestLayer.Value;
        }

        // ── Unity Lifecycle ───────────────────────────────────────────────────────

        private void Update()
        {
            if (_config == null || _input == null) return;

            WorldPosition   = _input.GetMouseWorldPosition();
            EffectiveRadius = ComputeRadius();

            RefreshOverlapping();
        }

        // ── Private ───────────────────────────────────────────────────────────────

        private float ComputeRadius()
        {
            float radiusMult = UpgradeApplicationSystem.GetEffectiveStat(StatType.HarvestRadius);
            // Base radius × (1 + multiplier bonus) — baseline multiplier = 0 means no change
            return _config.BaseRadius * (1f + radiusMult);
        }

        private void RefreshOverlapping()
        {
            _overlapping.Clear();

            int count = Physics2D.OverlapCircle(WorldPosition, EffectiveRadius,
                new UnityEngine.ContactFilter2D { layerMask = _harvestLayer, useLayerMask = true, useTriggers = true },
                _hitBuffer);
            for (int i = 0; i < count; i++)
            {
                var node = _hitBuffer[i].GetComponent<HarvestNode>();
                if (node != null && node.IsAlive)
                    _overlapping.Add(node);
            }
        }

        // ── Gizmos ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_config == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
            Gizmos.DrawSphere(WorldPosition, EffectiveRadius > 0f ? EffectiveRadius : _config.BaseRadius);
        }
#endif
    }
}
