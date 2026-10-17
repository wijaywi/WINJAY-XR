using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace WinjayXR
{
    /// <summary>
    /// Desktop fallback camera control (right-drag orbit around the world).
    /// Disables itself when an XR device provides head tracking.
    /// </summary>
    public sealed class EditorCameraOrbit : MonoBehaviour
    {
        public Vector3 target = new Vector3(0, 2, 0);
        public float distance = 12f;

        private float _yaw;
        private float _pitch = 25f;
        private float _dist = -1f;

        private void LateUpdate()
        {
            if (WinjayPrimitives.XrDisplayActive()) return;
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (_dist < 0f) _dist = distance;

            if (mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * 0.15f;
                _pitch = Mathf.Clamp(_pitch - d.y * 0.15f, 8f, 75f);
            }
            _dist = Mathf.Clamp(_dist - mouse.scroll.ReadValue().y * 0.1f, 4f, 25f);

            var rot = Quaternion.Euler(_pitch, _yaw, 0);
            var offset = rot * new Vector3(0, 0, -_dist);
            transform.position = Vector3.Lerp(transform.position, target + offset, 0.2f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(target - transform.position, Vector3.up), 0.2f);
        }
    }
}
