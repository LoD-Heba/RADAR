import sys
import re

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update SendKeyToWoW with MapVirtualKey and correct lParam
    old_sendkey = """        public void SendKeyToWoW(int vk)
        {
            if (targetPid == 0) return;
            try
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(targetPid);
                if (p != null)
                {
                    IntPtr hwnd = p.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                        System.Threading.Thread.Sleep(15);
                        PostMessage(hwnd, WM_KEYUP, vk, 0);
                    }
                }
            }
            catch { }
        }"""
        
    new_sendkey = """        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);

        public void SendKeyToWoW(int vk)
        {
            if (targetPid == 0) return;
            try
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(targetPid);
                if (p != null)
                {
                    IntPtr hwnd = p.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                    {
                        uint scanCode = MapVirtualKey((uint)vk, 0);
                        uint lParamDown = (scanCode << 16) | 1;
                        uint lParamUp = (1u << 31) | (1u << 30) | (scanCode << 16) | 1;

                        PostMessage(hwnd, WM_KEYDOWN, vk, (int)lParamDown);
                        System.Threading.Thread.Sleep(15);
                        PostMessage(hwnd, WM_KEYUP, vk, (int)lParamUp);
                    }
                }
            }
            catch { }
        }"""
    
    if "MapVirtualKey" not in content:
        content = content.replace(old_sendkey, new_sendkey)

    # 2. Update Kick logic in GetNearbyPlayers
    old_kick = """                            // KickBot (Auto-Interrupt)
                            if (p.Guid == activeChar.TargetGuid && p.Distance < 30f && (p.CastingSpellId != 0 || p.ChannelSpellId != 0))
                            {
                                if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                {
                                    shouldKick = true;
                                    lastKickTime = DateTime.Now;
                                }
                            }"""

    new_kick = """                            // KickBot (Auto-Interrupt) + Logging
                            if (p.Distance < 30f && (p.CastingSpellId != 0 || p.ChannelSpellId != 0))
                            {
                                string debugMsg = $"[{DateTime.Now:HH:mm:ss.fff}] CAST DETECTED! Enemy: {p.Name}, EnemyGuid: {p.Guid}, MyTarget: {activeChar.TargetGuid}, Match: {p.Guid == activeChar.TargetGuid}\\n";
                                System.IO.File.AppendAllText("kick_debug.txt", debugMsg);

                                if (p.Guid == activeChar.TargetGuid)
                                {
                                    if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                    {
                                        shouldKick = true;
                                        lastKickTime = DateTime.Now;
                                        System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] -> KICK TRIGGERED! Sending F9 and '3'!\\n");
                                    }
                                }
                            }"""
    
    content = content.replace(old_kick, new_kick)

    # 3. Update MemoryScannerLoop to send F9 AND 3
    old_trigger = """                    if (shouldKick && activeChar != null && activeChar.Pid != 0)
                    {
                        SendKeyToWoW(VK_F9);
                        shouldKick = false; // Prevents multiple presses if already pressed (handled by lastKickTime)
                    }"""
                    
    new_trigger = """                    if (shouldKick && activeChar != null && activeChar.Pid != 0)
                    {
                        SendKeyToWoW(VK_F9);
                        System.Threading.Thread.Sleep(10);
                        SendKeyToWoW(0x33); // Tecla '3' para cortar casteo directamente en la casilla 3
                        shouldKick = false; // Prevents multiple presses if already pressed (handled by lastKickTime)
                    }"""
                    
    content = content.replace(old_trigger, new_trigger)

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
