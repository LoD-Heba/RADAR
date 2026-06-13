import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update OpenProcess flags
    if "VirtualMemoryWrite = 0x00000020" not in content:
        content = content.replace("VirtualMemoryOperation = 0x00000008,", "VirtualMemoryOperation = 0x00000008,\n            VirtualMemoryWrite = 0x00000020,")

    old_open = """                IntPtr handle = Memory.OpenProcess(
                    Memory.ProcessAccessFlags.VirtualMemoryRead |
                    Memory.ProcessAccessFlags.QueryInformation,
                    false, (uint)targetPid);"""
    
    new_open = """                IntPtr handle = Memory.OpenProcess(
                    Memory.ProcessAccessFlags.VirtualMemoryRead |
                    Memory.ProcessAccessFlags.VirtualMemoryWrite |
                    Memory.ProcessAccessFlags.VirtualMemoryOperation |
                    Memory.ProcessAccessFlags.QueryInformation,
                    false, (uint)targetPid);"""
    content = content.replace(old_open, new_open)

    # 2. Add WriteProcessMemory and WriteFloat/WriteUInt to Memory class
    if "WriteProcessMemory" not in content:
        old_read_decl = """        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr h, IntPtr a, [Out] byte[] b, int s, out IntPtr r);"""
        
        new_read_decl = """        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr h, IntPtr a, [Out] byte[] b, int s, out IntPtr r);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, int s, out IntPtr r);

        public void WriteFloat(IntPtr address, float value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }

        public void WriteUInt(IntPtr address, uint value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }

        public void WriteUInt64(IntPtr address, ulong value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }"""
        content = content.replace(old_read_decl, new_read_decl)

    # 3. Add firstCastDetectedTime logic and CTM math
    if "DateTime firstCastDetectedTime = DateTime.MinValue;" not in content:
        content = content.replace("bool shouldKick = false;", "bool shouldKick = false;\n                        DateTime firstCastDetectedTime = DateTime.MinValue;\n                        ulong currentCastingGuid = 0;")

    # We need to replace the entire KickBot block. 
    # Let's search for "// KickBot (Auto-Interrupt) + Auto-Walk" block.
    # We will use Regex or simple string replacement.
    old_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk
                            if (p.Distance < 30f && (p.CastingSpellId != 0 || p.ChannelSpellId != 0))
                            {
                                if (p.Guid == activeChar.TargetGuid)
                                {
                                    if (p.Distance <= 6.5f) // Rango de melee expandido ligeramente para latencia
                                    {
                                        if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                        {
                                            shouldKick = true;
                                            lastKickTime = DateTime.Now;
                                            System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] -> KICK (Melee Range)! Sending F9 and '3'\\n");
                                        }
                                    }
                                    else
                                    {
                                        // Está casteando pero está lejos, corremos hacia él
                                        shouldWalk = true;
                                    }
                                }
                            }"""

    new_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk (CTM Behind Target)
                            if (p.CastingSpellId != 0 || p.ChannelSpellId != 0)
                            {
                                if (p.Guid == activeChar.TargetGuid)
                                {
                                    if (currentCastingGuid != p.Guid)
                                    {
                                        currentCastingGuid = p.Guid;
                                        firstCastDetectedTime = DateTime.Now;
                                    }

                                    if (p.Distance <= 6.5f) // Melee range
                                    {
                                        // Esperamos un margen de 800ms para simular corte tardío
                                        if ((DateTime.Now - firstCastDetectedTime).TotalMilliseconds >= 800)
                                        {
                                            if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                            {
                                                shouldKick = true;
                                                lastKickTime = DateTime.Now;
                                                System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] -> KICK Delayed! Dist={p.Distance:F1}\\n");
                                            }
                                        }
                                    }
                                    else if (p.Distance < 30f) // Si está muy lejos, ni lo intentamos
                                    {
                                        // Auto-posicionamiento por la espalda (Click-To-Move Inyección)
                                        float backDist = 2.0f; // 2 yardas a la espalda
                                        float destX = p.X - (float)Math.Cos(p.Facing) * backDist;
                                        float destY = p.Y - (float)Math.Sin(p.Facing) * backDist;
                                        
                                        const uint CTM_BASE = 0x00CA11D8;
                                        activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x8C), destX);
                                        activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x90), destY);
                                        activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x94), p.Z); // Ground Z
                                        activeChar.Mem.WriteUInt64((IntPtr)(CTM_BASE + 0x20), p.Guid);
                                        activeChar.Mem.WriteUInt((IntPtr)(CTM_BASE + 0x1C), 4); // 4 = Move
                                        
                                        // No necesitamos presionar F8 porque escribimos en memoria
                                    }
                                }
                            }
                            else
                            {
                                if (p.Guid == currentCastingGuid)
                                {
                                    currentCastingGuid = 0; // Se detuvo el casteo
                                    firstCastDetectedTime = DateTime.MinValue;
                                }
                            }"""

    if old_kick in content:
        content = content.replace(old_kick, new_kick)
    else:
        print("Could not find KickBot block!")

    # Remove shouldWalk from the end of the loop since we use Memory CTM now
    old_trigger = """                        if (shouldEquip) SendKeyToWoW(VK_F10);
                        if (shouldWalk) SendKeyToWoW(VK_F8);
                        if (shouldKick) 
                        {
                            SendKeyToWoW(VK_F9);
                            System.Threading.Thread.Sleep(10);
                            SendKeyToWoW(0x33);
                        }"""

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
