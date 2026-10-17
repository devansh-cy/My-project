using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DroneTrainer.Core
{
    /// <summary>
    /// Dual-compatibility input helper supporting both Unity's New Input System
    /// and legacy Input Manager seamlessly without throwing InvalidOperationException.
    /// </summary>
    public static class InputHelper
    {
        public static bool WasKeyPressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                switch (key)
                {
                    case KeyCode.Tab:
                        return kb.tabKey.wasPressedThisFrame;
                    case KeyCode.F1:
                        return kb.f1Key.wasPressedThisFrame;
                    case KeyCode.F2:
                        return kb.f2Key.wasPressedThisFrame;
                    case KeyCode.Escape:
                        return kb.escapeKey.wasPressedThisFrame;
                    case KeyCode.Space:
                        return kb.spaceKey.wasPressedThisFrame;
                    case KeyCode.Return:
                        return kb.enterKey.wasPressedThisFrame;
                    default:
                        break;
                }
            }
#endif

#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            try
            {
                return Input.GetKeyDown(key);
            }
            catch
            {
                return false;
            }
#else
            return false;
#endif
        }
    }
}
