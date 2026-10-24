using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using WindowsInput;
using WindowsInput.Native;

namespace CrewChiefV4.commands
{
    public class KeyPresser
    {
        // used for free text entry only
        public static InputSimulator InputSim = new InputSimulator();

        // used by VROverlayWindow
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);

        public static bool parseKeycode(String keyString, out Tuple<VirtualKeyCode?, VirtualKeyCode> modifierAndKeyCode)
        {
            VirtualKeyCode virtualKeyCode;
            VirtualKeyCode? modifier = null;
            if (parseNonLetterChars(keyString, out virtualKeyCode))
            {
                modifierAndKeyCode = new Tuple<VirtualKeyCode?, VirtualKeyCode>(modifier, virtualKeyCode);
                return true;
            }
            // special case for action keys where the keyString is just a letter - we parse this to the appropriate VirtualKeyCode
            // but we add the modifier to capitalize it
            bool isSingleUpperCaseChar = keyString.Length == 1 && Regex.IsMatch(keyString, "^[A-Z]$");
            bool addedModifier = false;
            if (keyString.Contains("+"))
            {
                string[] split = keyString.Split('+');
                keyString = split[1];
                if (parseModifier(split[0], out VirtualKeyCode parsedModifier))
                {
                    modifier = parsedModifier;
                    addedModifier = true;
                }
            }
            if (!addedModifier && isSingleUpperCaseChar)
            {
                modifier = VirtualKeyCode.LSHIFT;
            }
            if (parseKeycodeAsVirtualKeyCode(keyString, out virtualKeyCode))
            {
                modifierAndKeyCode = new Tuple<VirtualKeyCode?, VirtualKeyCode>(modifier, virtualKeyCode);
                return true;
            }
            if (parseKeycodeAsKeyCode(keyString, out virtualKeyCode))
            {
                modifierAndKeyCode = new Tuple<VirtualKeyCode?, VirtualKeyCode>(modifier, virtualKeyCode);
                return true;
            }
            modifierAndKeyCode = new Tuple<VirtualKeyCode?, VirtualKeyCode>(modifier, 0);
            return false;
        }

        private static bool parseModifier(String modifierKeyString, out VirtualKeyCode modifierVirtualKeyCode)
        {
            VirtualKeyCode parsedModifierVirtualKeyCode;
            if (Enum.TryParse(modifierKeyString, true, out parsedModifierVirtualKeyCode))
            {
                modifierVirtualKeyCode = parsedModifierVirtualKeyCode;
                return true;
            }
            KeyCode parsedModifierKeyCode;
            if (Enum.TryParse(modifierKeyString, true, out parsedModifierKeyCode))
            {
                modifierVirtualKeyCode = (VirtualKeyCode)parsedModifierKeyCode;
                return true;
            }
            modifierVirtualKeyCode = 0;
            return false;
        }

        private static bool parseNonLetterChars(String keyString, out VirtualKeyCode virtualKeyCode)
        {
            if (keyString == " ")
            {
                virtualKeyCode = VirtualKeyCode.SPACE;
                return true;
            }
            if (keyString == ",")
            {
                virtualKeyCode = VirtualKeyCode.OEM_COMMA;
                return true;
            }
            if (keyString == ".")
            {
                virtualKeyCode = VirtualKeyCode.OEM_PERIOD;
                return true;
            }
            if (keyString == "-")
            {
                virtualKeyCode = VirtualKeyCode.OEM_MINUS;
                return true;
            }
            if (keyString == "+")
            {
                virtualKeyCode = VirtualKeyCode.OEM_PLUS;
                return true;
            }
            virtualKeyCode = 0;
            return false;
        }

        private static bool parseKeycodeAsVirtualKeyCode(String keyString, out VirtualKeyCode virtualKeyCode)
        {
            if (keyString == " ")
            {
                virtualKeyCode = VirtualKeyCode.SPACE;
                return true;
            }
            if (Enum.TryParse(keyString, true, out virtualKeyCode))
            {
                return true;
            }
            if (Enum.TryParse("VK_" + keyString, true, out virtualKeyCode))
            {
                return true;
            }
            if (keyString.StartsWith("VK_") && keyString.Length > 2)
            {
                string keyStringWithoutLeadingText = keyString.Substring(3);
                if (Enum.TryParse(keyStringWithoutLeadingText, true, out virtualKeyCode))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool parseKeycodeAsKeyCode(String keyString, out VirtualKeyCode virtualKeyCode)
        {
            if (keyString == " ")
            {
                virtualKeyCode = VirtualKeyCode.SPACE;
                return true;
            }
            KeyCode parsedKeyCode;
            if (Enum.TryParse(keyString, true, out parsedKeyCode))
            {
                virtualKeyCode = (VirtualKeyCode)parsedKeyCode;
                return true;
            }
            if (Enum.TryParse("KEY_" + keyString, true, out parsedKeyCode))
            {
                virtualKeyCode = (VirtualKeyCode)parsedKeyCode;
                return true;
            }
            if (keyString.StartsWith("KEY_") && keyString.Length > 3)
            {
                string keyStringWithoutLeadingText = keyString.Substring(4);
                if (Enum.TryParse(keyStringWithoutLeadingText, true, out parsedKeyCode))
                {
                    virtualKeyCode = (VirtualKeyCode)parsedKeyCode;
                    return true;
                }
            }
            virtualKeyCode = 0;
            return false;
        }

        // this should be used if the action item may have mulitple key presses (e.g. { MULTIPLE_100, KEY_A } - this is the
        // case for all standard actionItem instances constructed from the macros json
        public static void SendKeyPresses(Tuple<VirtualKeyCode?, VirtualKeyCode>[] keyCodes, int? keyPressTime, int waitBetweenKeys)
        {
            for (int keyIndex = 0; keyIndex < keyCodes.Length; keyIndex++)
            {
                KeyPresser.SendKeyPress(keyCodes[keyIndex], keyPressTime);
                int waitTime = waitBetweenKeys <= 0 ? 20 : waitBetweenKeys;
                Thread.Sleep(waitTime);
            }
        }

        // this should only be used if we've constructed our own key press in code (e.g. the VR recentre key and ACC fall back pit commands)
        // where we know there can't be a multiple key press directive
        public static void SendKeyPress(Tuple<VirtualKeyCode?, VirtualKeyCode> modifierAndKeyCode, int? keyPressTime = null)
        {
            KeyCode? modifierKeyCode = null;
            if (modifierAndKeyCode.Item1 != null)
            {
                modifierKeyCode = (KeyCode)modifierAndKeyCode.Item1;
            }
            int defaultKeyHoldTime = Game.ACC ? 80 : 50;
            SendScanCodeKeyPress(new Tuple<KeyCode?, KeyCode>(modifierKeyCode, (KeyCode)modifierAndKeyCode.Item2),
                keyPressTime == null || keyPressTime <= 0 ? defaultKeyHoldTime : keyPressTime.Value);
        }

        const int INPUT_MOUSE = 0;
        const int INPUT_KEYBOARD = 1;
        const int INPUT_HARDWARE = 2;
        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint KEYEVENTF_UNICODE = 0x0004;
        const uint KEYEVENTF_SCANCODE = 0x0008;

        static Tuple<ushort, Boolean> keyBeingPressed = null;
        static Tuple<ushort, Boolean> modifierKeyBeingPressed = null;

        struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT
        {
            /*Virtual Key code.  Must be from 1-254.  If the dwFlags member specifies KEYEVENTF_UNICODE, wVk must be 0.*/
            public ushort wVk;
            /*A hardware scan code for the key. If dwFlags specifies KEYEVENTF_UNICODE, wScan specifies a Unicode character which is to be sent to the foreground application.*/
            public ushort wScan;
            /*Specifies various aspects of a keystroke.  See the KEYEVENTF_ constants for more information.*/
            public uint dwFlags;
            /*The time stamp for the event, in milliseconds. If this parameter is zero, the system will provide its own time stamp.*/
            public uint time;
            /*An additional value associated with the keystroke. Use the GetMessageExtraInfo function to obtain this information.*/
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private static KeyCode[] extendedKeys = { KeyCode.UP, KeyCode.DOWN, KeyCode.LEFT, KeyCode.RIGHT,
                                           KeyCode.INSERT, KeyCode.HOME, KeyCode.PAGE_UP, KeyCode.PAGEDOWN, KeyCode.DELETE, KeyCode.END };

        public static void releasePressedKey()
        {
            if (modifierKeyBeingPressed != null)
            {
                try
                {
                    release(keyBeingPressed.Item1, keyBeingPressed.Item2, true);
                }
                catch (Exception) { /*swallow*/ }
            }
            if (keyBeingPressed != null)
            {
                try
                {
                    release(keyBeingPressed.Item1, keyBeingPressed.Item2, false);
                }
                catch (Exception) { /*swallow*/ }
            }
        }

        private static void SendScanCodeKeyPress(Tuple<KeyPresser.KeyCode?, KeyPresser.KeyCode> modifierAndKeyCode, int holdTimeMillis)
        {
            bool sendModifier = modifierAndKeyCode.Item1 != null;

            ushort scanCode = (ushort)MapVirtualKey((ushort)modifierAndKeyCode.Item2, 0);
            Boolean extended = extendedKeys.Contains(modifierAndKeyCode.Item2);
            ushort modifierScanCode = 0;
            Boolean modifierExtended = false;
            if (sendModifier)
            {
                modifierScanCode = (ushort)MapVirtualKey((ushort)modifierAndKeyCode.Item1.Value, 0);
                modifierExtended = extendedKeys.Contains(modifierAndKeyCode.Item1.Value);
                press(modifierScanCode, modifierExtended, true);
                Thread.Sleep(20);
            }
            press(scanCode, extended, false);
            Thread.Sleep(holdTimeMillis);
            release(scanCode, extended, false);
            if (sendModifier)
            {
                Thread.Sleep(20);
                release(modifierScanCode, modifierExtended, true);
            }
        }
        private static void press(ushort scanCode, Boolean extended, Boolean isModifier)
        {
            uint eventScanCode = extended ? KEYEVENTF_SCANCODE | KEYEVENTF_EXTENDEDKEY : KEYEVENTF_SCANCODE;
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = scanCode,
                            dwFlags = eventScanCode,
                            dwExtraInfo = GetMessageExtraInfo(),
                        }
                    }
                }
            };
            if (isModifier)
            {
                modifierKeyBeingPressed = new Tuple<ushort, bool>(scanCode, extended);
            }
            else
            {
                keyBeingPressed = new Tuple<ushort, bool>(scanCode, extended);
            }
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static void release(ushort scanCode, Boolean extended, Boolean isModifier)
        {
            uint eventScanCode = extended ? KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY : KEYEVENTF_SCANCODE | KEYEVENTF_KEYUP;
            INPUT[] inputs = new INPUT[]
            {
                new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = scanCode,
                            dwFlags = eventScanCode,
                            dwExtraInfo = GetMessageExtraInfo(),
                        }
                    }
                }
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (isModifier)
            {
                modifierKeyBeingPressed = null;
            }
            else
            {
                keyBeingPressed = null;
            }
        }


        public enum KeyCode : ushort
        {
            #region Media

            /// <summary>
            /// Next track if a song is playing
            /// </summary>
            MEDIA_NEXT_TRACK = 0xb0,

            /// <summary>
            /// Play pause
            /// </summary>
            MEDIA_PLAY_PAUSE = 0xb3,

            /// <summary>
            /// Previous track
            /// </summary>
            MEDIA_PREV_TRACK = 0xb1,

            /// <summary>
            /// Stop
            /// </summary>
            MEDIA_STOP = 0xb2,

            #endregion

            #region math

            /// <summary>Key "+"</summary>
            ADD = 0x6b,
            /// <summary>
            /// "*" key
            /// </summary>
            MULTIPLY = 0x6a,

            /// <summary>
            /// "/" key
            /// </summary>
            DIVIDE = 0x6f,

            /// <summary>
            /// Subtract key "-"
            /// </summary>
            SUBTRACT = 0x6d,

            #endregion

            #region Browser
            /// <summary>
            /// Go Back
            /// </summary>
            BROWSER_BACK = 0xa6,
            /// <summary>
            /// Favorites
            /// </summary>
            BROWSER_FAVORITES = 0xab,
            /// <summary>
            /// Forward
            /// </summary>
            BROWSER_FORWARD = 0xa7,
            /// <summary>
            /// Home
            /// </summary>
            BROWSER_HOME = 0xac,
            /// <summary>
            /// Refresh
            /// </summary>
            BROWSER_REFRESH = 0xa8,
            /// <summary>
            /// browser search
            /// </summary>
            BROWSER_SEARCH = 170,
            /// <summary>
            /// Stop
            /// </summary>
            BROWSER_STOP = 0xa9,
            #endregion

            #region Numpad numbers
            /// <summary>
            /// 
            /// </summary>
            NUMPAD0 = 0x60,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD1 = 0x61,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD2 = 0x62,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD3 = 0x63,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD4 = 100,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD5 = 0x65,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD6 = 0x66,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD7 = 0x67,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD8 = 0x68,
            /// <summary>
            /// 
            /// </summary>
            NUMPAD9 = 0x69,

            #endregion

            #region Fkeys
            /// <summary>
            /// F1
            /// </summary>
            F1 = 0x70,
            /// <summary>
            /// F10
            /// </summary>
            F10 = 0x79,
            /// <summary>
            /// 
            /// </summary>
            F11 = 0x7a,
            /// <summary>
            /// 
            /// </summary>
            F12 = 0x7b,
            /// <summary>
            /// 
            /// </summary>
            F13 = 0x7c,
            /// <summary>
            /// 
            /// </summary>
            F14 = 0x7d,
            /// <summary>
            /// 
            /// </summary>
            F15 = 0x7e,
            /// <summary>
            /// 
            /// </summary>
            F16 = 0x7f,
            /// <summary>
            /// 
            /// </summary>
            F17 = 0x80,
            /// <summary>
            /// 
            /// </summary>
            F18 = 0x81,
            /// <summary>
            /// 
            /// </summary>
            F19 = 130,
            /// <summary>
            /// 
            /// </summary>
            F2 = 0x71,
            /// <summary>
            /// 
            /// </summary>
            F20 = 0x83,
            /// <summary>
            /// 
            /// </summary>
            F21 = 0x84,
            /// <summary>
            /// 
            /// </summary>
            F22 = 0x85,
            /// <summary>
            /// 
            /// </summary>
            F23 = 0x86,
            /// <summary>
            /// 
            /// </summary>
            F24 = 0x87,
            /// <summary>
            /// 
            /// </summary>
            F3 = 0x72,
            /// <summary>
            /// 
            /// </summary>
            F4 = 0x73,
            /// <summary>
            /// 
            /// </summary>
            F5 = 0x74,
            /// <summary>
            /// 
            /// </summary>
            F6 = 0x75,
            /// <summary>
            /// 
            /// </summary>
            F7 = 0x76,
            /// <summary>
            /// 
            /// </summary>
            F8 = 0x77,
            /// <summary>
            /// 
            /// </summary>
            F9 = 120,

            #endregion

            #region Other
            /// <summary>
            /// 
            /// </summary>
            OEM_1 = 0xba,
            /// <summary>
            /// 
            /// </summary>
            OEM_102 = 0xe2,
            /// <summary>
            /// 
            /// </summary>
            OEM_2 = 0xbf,
            /// <summary>
            /// 
            /// </summary>
            OEM_3 = 0xc0,
            /// <summary>
            /// 
            /// </summary>
            OEM_4 = 0xdb,
            /// <summary>
            /// 
            /// </summary>
            OEM_5 = 220,
            /// <summary>
            /// 
            /// </summary>
            OEM_6 = 0xdd,
            /// <summary>
            /// 
            /// </summary>
            OEM_7 = 0xde,
            /// <summary>
            /// 
            /// </summary>
            OEM_8 = 0xdf,
            /// <summary>
            /// 
            /// </summary>
            OEM_CLEAR = 0xfe,
            /// <summary>
            /// 
            /// </summary>
            OEM_COMMA = 0xbc,
            /// <summary>
            /// 
            /// </summary>
            OEM_MINUS = 0xbd,
            /// <summary>
            /// 
            /// </summary>
            OEM_PERIOD = 190,
            /// <summary>
            /// 
            /// </summary>
            OEM_PLUS = 0xbb,

            #endregion

            #region KEYS

            /// <summary>
            /// 
            /// </summary>
            KEY_0 = 0x30,
            /// <summary>
            /// 
            /// </summary>
            KEY_1 = 0x31,
            /// <summary>
            /// 
            /// </summary>
            KEY_2 = 50,
            /// <summary>
            /// 
            /// </summary>
            KEY_3 = 0x33,
            /// <summary>
            /// 
            /// </summary>
            KEY_4 = 0x34,
            /// <summary>
            /// 
            /// </summary>
            KEY_5 = 0x35,
            /// <summary>
            /// 
            /// </summary>
            KEY_6 = 0x36,
            /// <summary>
            /// 
            /// </summary>
            KEY_7 = 0x37,
            /// <summary>
            /// 
            /// </summary>
            KEY_8 = 0x38,
            /// <summary>
            /// 
            /// </summary>
            KEY_9 = 0x39,
            /// <summary>
            /// 
            /// </summary>
            KEY_A = 0x41,
            /// <summary>
            /// 
            /// </summary>
            KEY_B = 0x42,
            /// <summary>
            /// 
            /// </summary>
            KEY_C = 0x43,
            /// <summary>
            /// 
            /// </summary>
            KEY_D = 0x44,
            /// <summary>
            /// 
            /// </summary>
            KEY_E = 0x45,
            /// <summary>
            /// 
            /// </summary>
            KEY_F = 70,
            /// <summary>
            /// 
            /// </summary>
            KEY_G = 0x47,
            /// <summary>
            /// 
            /// </summary>
            KEY_H = 0x48,
            /// <summary>
            /// 
            /// </summary>
            KEY_I = 0x49,
            /// <summary>
            /// 
            /// </summary>
            KEY_J = 0x4a,
            /// <summary>
            /// 
            /// </summary>
            KEY_K = 0x4b,
            /// <summary>
            /// 
            /// </summary>
            KEY_L = 0x4c,
            /// <summary>
            /// 
            /// </summary>
            KEY_M = 0x4d,
            /// <summary>
            /// 
            /// </summary>
            KEY_N = 0x4e,
            /// <summary>
            /// 
            /// </summary>
            KEY_O = 0x4f,
            /// <summary>
            /// 
            /// </summary>
            KEY_P = 80,
            /// <summary>
            /// 
            /// </summary>
            KEY_Q = 0x51,
            /// <summary>
            /// 
            /// </summary>
            KEY_R = 0x52,
            /// <summary>
            /// 
            /// </summary>
            KEY_S = 0x53,
            /// <summary>
            /// 
            /// </summary>
            KEY_T = 0x54,
            /// <summary>
            /// 
            /// </summary>
            KEY_U = 0x55,
            /// <summary>
            /// 
            /// </summary>
            KEY_V = 0x56,
            /// <summary>
            /// 
            /// </summary>
            KEY_W = 0x57,
            /// <summary>
            /// 
            /// </summary>
            KEY_X = 0x58,
            /// <summary>
            /// 
            /// </summary>
            KEY_Y = 0x59,
            /// <summary>
            /// 
            /// </summary>
            KEY_Z = 90,

            #endregion

            #region volume
            /// <summary>
            /// Decrese volume
            /// </summary>
            VOLUME_DOWN = 0xae,

            /// <summary>
            /// Mute volume
            /// </summary>
            VOLUME_MUTE = 0xad,

            /// <summary>
            /// Increase volue
            /// </summary>
            VOLUME_UP = 0xaf,

            #endregion


            /// <summary>
            /// Take snapshot of the screen and place it on the clipboard
            /// </summary>
            SNAPSHOT = 0x2c,

            /// <summary>Send right click from keyboard "key that is 2 keys to the right of space bar"</summary>
            RightClick = 0x5d,

            /// <summary>
            /// Go Back or delete
            /// </summary>
            BACKSPACE = 8,

            /// <summary>
            /// Control + Break "When debuging if you step into an infinite loop this will stop debug"
            /// </summary>
            CANCEL = 3,
            /// <summary>
            /// Caps lock key to send cappital letters
            /// </summary>
            CAPS_LOCK = 20,
            /// <summary>
            /// Ctlr key
            /// </summary>
            CONTROL = 0x11,

            /// <summary>
            /// Alt key
            /// </summary>
            ALT = 18,

            /// <summary>
            /// "." key
            /// </summary>
            DECIMAL = 110,

            /// <summary>
            /// Delete Key
            /// </summary>
            DELETE = 0x2e,


            /// <summary>
            /// Arrow down key
            /// </summary>
            DOWN = 40,

            /// <summary>
            /// End key
            /// </summary>
            END = 0x23,

            /// <summary>
            /// Escape key
            /// </summary>
            ESC = 0x1b,

            /// <summary>
            /// Home key
            /// </summary>
            HOME = 0x24,

            /// <summary>
            /// Insert key
            /// </summary>
            INSERT = 0x2d,

            /// <summary>
            /// Open my computer
            /// </summary>
            LAUNCH_APP1 = 0xb6,
            /// <summary>
            /// Open calculator
            /// </summary>
            LAUNCH_APP2 = 0xb7,

            /// <summary>
            /// Open default email in my case outlook
            /// </summary>
            LAUNCH_MAIL = 180,

            /// <summary>
            /// Opend default media player (itunes, winmediaplayer, etc)
            /// </summary>
            LAUNCH_MEDIA_SELECT = 0xb5,

            /// <summary>
            /// Left control
            /// </summary>
            LCONTROL = 0xa2,

            /// <summary>
            /// Left arrow
            /// </summary>
            LEFT = 0x25,

            /// <summary>
            /// Left shift
            /// </summary>
            LSHIFT = 160,

            /// <summary>
            /// left windows key
            /// </summary>
            LWIN = 0x5b,


            /// <summary>
            /// Next "page down"
            /// </summary>
            PAGEDOWN = 0x22,

            /// <summary>
            /// Num lock to enable typing numbers
            /// </summary>
            NUMLOCK = 0x90,

            /// <summary>
            /// Page up key
            /// </summary>
            PAGE_UP = 0x21,

            /// <summary>
            /// Right control
            /// </summary>
            RCONTROL = 0xa3,

            /// <summary>
            /// Return key
            /// </summary>
            ENTER = 13,

            /// <summary>
            /// Right arrow key
            /// </summary>
            RIGHT = 0x27,

            /// <summary>
            /// Right shift
            /// </summary>
            RSHIFT = 0xa1,

            /// <summary>
            /// Right windows key
            /// </summary>
            RWIN = 0x5c,

            /// <summary>
            /// Shift key
            /// </summary>
            SHIFT = 0x10,

            /// <summary>
            /// Space back key
            /// </summary>
            SPACE_BAR = 0x20,

            /// <summary>
            /// Tab key
            /// </summary>
            TAB = 9,

            /// <summary>
            /// Up arrow key
            /// </summary>
            UP = 0x26,
        }
    }
}
