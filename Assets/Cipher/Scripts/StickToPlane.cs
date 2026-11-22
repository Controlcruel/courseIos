using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Cipher
{
    /// <summary>Держит улику «приклеенной» к плоскости, пока iPhone уточняет её положение.</summary>
    public class StickToPlane : MonoBehaviour
    {
        ARPlane plane;
        Vector3 localPos;
        Quaternion localRot;

        public void Init(ARPlane p)
        {
            plane = p;
            localPos = p.transform.InverseTransformPoint(transform.position);
            localRot = Quaternion.Inverse(p.transform.rotation) * transform.rotation;
        }

        void LateUpdate()
        {
            if (!plane) { Destroy(this); return; }
            if (plane.subsumedBy) { Init(plane.subsumedBy); return; }
            transform.SetPositionAndRotation(plane.transform.TransformPoint(localPos), plane.transform.rotation * localRot);
        }
    }
}
