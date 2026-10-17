using UnityEngine;

namespace WinjayXR
{
    /// <summary>Keeps labels and panels facing the main camera.</summary>
    public sealed class Billboard : MonoBehaviour
    {
        /// <summary>Yaw-only mode: the object stays upright, turning around world up.</summary>
        public bool yawOnly;
        /// <summary>Extra back-tilt in degrees, for consoles that lean toward a viewer above them.</summary>
        public float tiltDegrees;

        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                var main = Camera.main;
                if (main != null) _cam = main.transform;
                else return;
            }

            var toObj = transform.position - _cam.position;
            if (!yawOnly)
            {
                transform.rotation = Quaternion.LookRotation(toObj, Vector3.up);
                return;
            }

            toObj.y = 0f;
            if (toObj.sqrMagnitude < 0.0001f) return;
            var rot = Quaternion.LookRotation(toObj.normalized, Vector3.up);
            if (tiltDegrees != 0f) rot *= Quaternion.Euler(tiltDegrees, 0f, 0f);
            transform.rotation = rot;
        }
    }
}
