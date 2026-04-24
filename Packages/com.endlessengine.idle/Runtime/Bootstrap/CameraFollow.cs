using UnityEngine;

namespace EndlessEngine.Bootstrap
{
    /// <summary>
    /// Simple camera follow for Vertical Slice. Attach to Main Camera.
    /// Not for production use — VS only.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private void LateUpdate()
        {
            if (_target == null) return;
            Vector3 desired = _target.position + _offset;
            transform.position = Vector3.Lerp(transform.position, desired, _smoothSpeed * Time.deltaTime);
        }
    }
}
