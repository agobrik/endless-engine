using UnityEngine;
using EndlessEngine.Building;

namespace EndlessEngine.Bootstrap
{
    [AddComponentMenu("Endless Engine/Building Slot Handler")]
    public class BuildingSlotHandler : MonoBehaviour
    {
        [SerializeField] private string _buildingId;
        [SerializeField] private int _gridX;
        [SerializeField] private int _gridY;

        private SpriteRenderer _sr;
        private static readonly Color EmptyColor   = new Color(0.6f, 0.55f, 0.5f);
        private static readonly Color PlacedColor  = new Color(0.3f, 0.7f, 0.35f);

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        public void Configure(string buildingId, int gridX, int gridY)
        {
            _buildingId = buildingId;
            _gridX      = gridX;
            _gridY      = gridY;
        }

        private void OnMouseDown()
        {
            var service = FindFirstObjectByType<BuildingService>();
            if (service == null) return;

            var result = service.TryPlace(_buildingId, _gridX, _gridY);
            if (result.Success)
            {
                if (_sr != null) _sr.color = PlacedColor;
                Debug.Log($"[BuildingSlotHandler] Placed {_buildingId} at ({_gridX},{_gridY})");
            }
            else
            {
                Debug.Log($"[BuildingSlotHandler] Place failed: {result.FailReason}");
            }
        }
    }
}
