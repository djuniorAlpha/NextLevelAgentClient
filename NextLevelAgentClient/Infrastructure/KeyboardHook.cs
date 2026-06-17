using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace NextLevelAgentClient.Infrastructure
{
    public static class KeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        public static event Action OnDeveloperExit;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static nint _hookID = nint.Zero;

        //Delegado necessario para o callback do Hook
        private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

        /// <summary>
        /// Ativa o bloqueio global do teclado para atalhos do sistema.
        /// </summary>
        public static void Start() {
            if(_hookID == nint.Zero)
            {
                Stop();
                _hookID = SetHook(_proc);
            }
        }

        /// <summary>
        /// Libera o teclado de volta para o comportamento padrão do Windows.
        /// </summary>
        public static void Stop() {
            if(_hookID != nint.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = nint.Zero;
            }
        }

        private static nint SetHook(LowLevelKeyboardProc proc)
        {
            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule curModule = curProcess.MainModule;
            return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        private static nint HookCallback(int nCode, nint wParam, nint lParam)
        {
            if(nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (Control.ModifierKeys == Keys.Shift && key == Keys.F12)
                {
                    OnDeveloperExit?.Invoke();
                    return 1;
                }

                if (key == Keys.LWin || key == Keys.RWin)
                {
                    return 1;
                }

                if (Control.ModifierKeys == Keys.Alt)
                {
                    if (key == Keys.Tab || key == Keys.F4 || key == Keys.Escape)
                    {
                        return 1;
                    }
                }

                if (Control.ModifierKeys == Keys.Control && key == Keys.Escape)
                {
                    return 1;
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint GetModuleHandle(string lpModuleName);
    }
}
