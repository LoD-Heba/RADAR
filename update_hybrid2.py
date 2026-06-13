import sys

def modify_program():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update SendKeyToWoW
    old_sendkey = """        public static void SendKeyToWoW(int vk)
        {
            IntPtr hwnd = FindWindow(null, "World of Warcraft");
            if (hwnd != IntPtr.Zero)
            {
                PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                System.Threading.Thread.Sleep(50);
                PostMessage(hwnd, WM_KEYUP, vk, 0);
            }
        }"""
        
    new_sendkey = """        public static void SendKeyToWoW(int vk)
        {
            IntPtr hwnd = FindWindow(null, "World of Warcraft");
            if (hwnd != IntPtr.Zero)
            {
                // Delay ultra-reducido para no trabar el hilo del radar. 
                // 15ms es más que suficiente para que DirectInput registre la pulsación.
                PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                System.Threading.Thread.Sleep(15);
                PostMessage(hwnd, WM_KEYUP, vk, 0);
            }
        }"""
    content = content.replace(old_sendkey, new_sendkey)

    # 2. Update Cooldowns in MemoryScannerLoop
    old_logic = """                            // Auto-Equip si un enemigo jugador se acerca a menos de 15 yardas
                            if (Config.AutoEquipPvP && p.Distance < 15f)
                            {
                                if ((DateTime.Now - lastAutoEquipTime).TotalSeconds > 10) // 10s cooldown
                                {
                                    shouldEquip = true;
                                    lastAutoEquipTime = DateTime.Now;
                                }
                            }

                            // Auto-Focus si un enemigo nos hace target a menos de 40 yardas
                            if (Config.AutoFocusThreat && p.IsTargetingMe && p.Distance < 40f)
                            {
                                if ((DateTime.Now - lastAutoFocusTime).TotalSeconds > 3) // 3s cooldown
                                {
                                    shouldFocus = true;
                                    lastAutoFocusTime = DateTime.Now;
                                }
                            }"""

    new_logic = """                            // Auto-Equip si un enemigo jugador se acerca a menos de 15 yardas
                            // (El Addon Lua verificará casteos y redundancias)
                            if (Config.AutoEquipPvP && p.Distance < 15f)
                            {
                                if ((DateTime.Now - lastAutoEquipTime).TotalSeconds > 1.5) // Cooldown hiper-rápido de 1.5s
                                {
                                    shouldEquip = true;
                                    lastAutoEquipTime = DateTime.Now;
                                }
                            }

                            // Auto-Focus si un enemigo nos hace target a menos de 40 yardas
                            // (El Addon Lua usa [noharm] para no perder tu target actual)
                            if (Config.AutoFocusThreat && p.IsTargetingMe && p.Distance < 40f)
                            {
                                if ((DateTime.Now - lastAutoFocusTime).TotalSeconds > 1.0) // Cooldown hiper-rápido de 1s
                                {
                                    shouldFocus = true;
                                    lastAutoFocusTime = DateTime.Now;
                                }
                            }"""
    
    content = content.replace(old_logic, new_logic)

    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
