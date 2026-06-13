import sys
import re

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Revert Scan Codes in SendKeyToWoW but keep GetWoWWindow and logging
    old_sendkey = """        public void SendKeyToWoW(int vk)
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

                    System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] Sending key 0x{vk:X} to HWND {hwnd} for PID {targetPid}\\n");

                    PostMessage(hwnd, WM_KEYDOWN, vk, (int)lParamDown);
                    System.Threading.Thread.Sleep(25);
                    PostMessage(hwnd, WM_KEYUP, vk, (int)lParamUp);
                }
                else
                {
                    System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] Could not find HWND for PID {targetPid}\\n");
                }
            }
            catch { }
        }"""

    new_sendkey = """        public void SendKeyToWoW(int vk)
        {
            if (targetPid == 0) return;
            try
            {
                IntPtr hwnd = GetWoWWindow(targetPid);
                if (hwnd != IntPtr.Zero)
                {
                    System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] Sending key 0x{vk:X} to HWND {hwnd} for PID {targetPid}\\n");
                    PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                    System.Threading.Thread.Sleep(20);
                    PostMessage(hwnd, WM_KEYUP, vk, 0);
                }
                else
                {
                    System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] [DEBUG] Could not find HWND for PID {targetPid}\\n");
                }
            }
            catch { }
        }"""
    
    content = content.replace(old_sendkey, new_sendkey)

    # 2. Add SendKeyToWoW(0x33) inside the update logic
    # Look for: if (shouldKick) SendKeyToWoW(VK_F9);
    # This was at line ~510 in my previous search!
    old_trigger = """                        if (shouldEquip) SendKeyToWoW(VK_F10);
                        if (shouldKick) SendKeyToWoW(VK_F9);"""
                        
    new_trigger = """                        if (shouldEquip) SendKeyToWoW(VK_F10);
                        if (shouldKick) 
                        {
                            SendKeyToWoW(VK_F9);
                            System.Threading.Thread.Sleep(10);
                            SendKeyToWoW(0x33);
                        }"""
    
    content = content.replace(old_trigger, new_trigger)

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
