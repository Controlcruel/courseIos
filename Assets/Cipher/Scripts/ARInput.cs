using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cipher
{
    /// <summary>
    /// Ввод для телефона. Прицел всегда в центре экрана: игрок наводит телефон на предмет.
    /// Касание экрана — действие, удержание пальца — сканирование.
    /// В редакторе то же самое делает левая кнопка мыши (правая + WASD двигают камеру XR Simulation).
    /// Касания по кнопкам интерфейса в игровой мир не передаются.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class ARInput : MonoBehaviour
    {
        public Ray Ray { get; private set; }
        public bool TapDown { get; private set; }
        public bool Held { get; private set; }

        bool startedOverUI;

        void Update()
        {
            var cam = Camera.main;
            if (cam) Ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            bool down = false, held = false;
            int pointerId = -1;
#if ENABLE_INPUT_SYSTEM
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                held = true;
                down = ts.primaryTouch.press.wasPressedThisFrame;
                pointerId = ts.primaryTouch.touchId.ReadValue();
            }
            else if (Mouse.current != null)
            {
                held = Mouse.current.leftButton.isPressed;
                down = Mouse.current.leftButton.wasPressedThisFrame;
            }
#else
            held = Input.GetMouseButton(0);
            down = Input.GetMouseButtonDown(0);
#endif
            if (down) startedOverUI = IsOverUI(pointerId);
            if (!held) startedOverUI = false;

            TapDown = down && !startedOverUI;
            Held = held && !startedOverUI;
        }

        static bool IsOverUI(int pointerId)
        {
            var es = EventSystem.current;
            if (!es) return false;
            return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
        }

        /// <summary>Клавиши для проверки в редакторе (на телефоне не используются).</summary>
        public static bool EditorKey(char c)
        {
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (char.ToLowerInvariant(c))
            {
                case 'c': return kb.cKey.wasPressedThisFrame;
                case 'u': return kb.uKey.wasPressedThisFrame;
                case 'n': return kb.nKey.wasPressedThisFrame;
                case 'm': return kb.mKey.wasPressedThisFrame;
            }
#endif
            return false;
        }
    }
}
