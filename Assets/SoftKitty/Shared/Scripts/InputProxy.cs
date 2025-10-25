using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace SoftKitty
{
    /// Please comment out the whole region of the input system you're not using.
    public class InputProxy : MonoBehaviour
    {
#if ENABLE_LEGACY_INPUT_MANAGER

            public static Vector3 mousePosition
            {
                get
                {
                    return Input.mousePosition;
                }
            }

            public static bool GetMouseButton(int _button)
            {
                return Input.GetMouseButton(_button);
            }

            public static bool GetMouseButtonDown(int _button)
            {
                return Input.GetMouseButtonDown(_button);
            }
            public static bool GetMouseButtonUp(int _button)
            {
                return Input.GetMouseButtonUp(_button);
            }

            public static bool GetKey(KeyCode _key)
            {
                return Input.GetKey(_key);
            }

            public static bool GetKeyUp(KeyCode _key)
            {
                return Input.GetKeyUp(_key);
            }

            public static bool GetKeyDown(KeyCode _key)
            {
                return Input.GetKeyDown(_key);
            }

#else
        private static InputAction[] mouseButtonActions = new InputAction[3]{
            new InputAction(binding: "<Mouse>/leftButton"),
            new InputAction(binding: "<Mouse>/rightButton"),
            new InputAction(binding: "<Mouse>/middleButton")
        };
        private static Dictionary<KeyCode, Key> lookup;

        public static Vector3 mousePosition
        {
            get
            {
                return Mouse.current.position.ReadValue();
            }
        }

        public static bool GetMouseButton(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].IsPressed();
        }

        public static bool GetMouseButtonDown(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].WasPressedThisFrame();
        }
        public static bool GetMouseButtonUp(int _button)
        {
            if (!mouseButtonActions[_button].enabled) mouseButtonActions[_button].Enable();
            return mouseButtonActions[_button].WasReleasedThisFrame();
        }

        public static bool GetKey(KeyCode _key)
        {

            return Keyboard.current[GetKeyByKeyCode(_key)].IsPressed();
        }

        public static bool GetKeyUp(KeyCode _key)
        {
            return Keyboard.current[GetKeyByKeyCode(_key)].wasReleasedThisFrame;
        }

        public static bool GetKeyDown(KeyCode _key)
        {
            return Keyboard.current[GetKeyByKeyCode(_key)].wasPressedThisFrame;
        }

        private static Key GetKeyByKeyCode(KeyCode _key)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<KeyCode, Key>();
                foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    var textVersion = keyCode.ToString().Replace("Alpha", "Digit").Replace("Keypad", "Numpad");
                    if (System.Enum.TryParse<Key>(textVersion, true, out var value))
                        lookup[keyCode] = value;
                }
                lookup[KeyCode.Return] = Key.Enter;
                lookup[KeyCode.KeypadEnter] = Key.NumpadEnter;
            }
            if (lookup.ContainsKey(_key))
                return lookup[_key];
            else
                return Key.None;
        }

#endif
    }
}