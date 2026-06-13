import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    robust_window_finder = """
        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        public static IntPtr GetWoWWindow(int pid)
        {
            IntPtr foundWindow = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out int windowPid);
                if (windowPid == pid && IsWindowVisible(hWnd))
                {
                    // Optionally check window class or title if needed
                    foundWindow = hWnd;
                    return false;
                }
                return true;
            }, 0);
            return foundWindow;
        }

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public void SendKeyToWoW(int vk)
        {
            if (targetPid == 0) return;
            try
            {
                IntPtr hwnd = GetWoWWindow(targetPid);
                if (hwnd != IntPtr.Zero)
                {
                    uint scanCode = MapVirtualKey((uint)vk, 0);
                    uint lParamDown = (scanCode << 16) | 1;
                    uint lParamUp = (1u << 31) | (1u << 30) | (scanCode << 16) | 1;

                    System.IO.File.AppendAllText("kick_debug.txt", $"[DEBUG] Sending key {vk} to HWND {hwnd} for PID {targetPid}\\n");

                    PostMessage(hwnd, WM_KEYDOWN, vk, (int)lParamDown);
                    System.Threading.Thread.Sleep(20); // Aumentado ligeramente
                    PostMessage(hwnd, WM_KEYUP, vk, (int)lParamUp);
                }
                else
                {
                    System.IO.File.AppendAllText("kick_debug.txt", $"[DEBUG] Could not find HWND for PID {targetPid}\\n");
                }
            }
            catch { }
        }
"""
    
    # We will replace the existing MapVirtualKey and SendKeyToWoW
    import re
    sendkey_pattern = re.compile(r'\[DllImport\("user32\.dll"\)\]\s*public static extern uint MapVirtualKey.*?catch \{ \}\s*\}', re.DOTALL)
    
    if sendkey_pattern.search(content):
        content = sendkey_pattern.sub(robust_window_finder.strip(), content)
    else:
        print("Could not find SendKeyToWoW block to replace")
        return

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
