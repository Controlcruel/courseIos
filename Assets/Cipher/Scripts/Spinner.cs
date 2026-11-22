using UnityEngine;

namespace Cipher
{
    /// <summary>Просто вращает объект (например, «пакеты» вокруг сетевой улики).</summary>
    public class Spinner : MonoBehaviour
    {
        [Tooltip("Скорость вращения по осям, градусов в секунду")]
        public Vector3 speed = new Vector3(0, 45, 0);
        void Update() => transform.Rotate(speed * Time.deltaTime, Space.Self);
    }
}
