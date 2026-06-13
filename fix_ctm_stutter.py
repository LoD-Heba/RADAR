import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add fields for logic
    if "DateTime lastCTMWriteTime = DateTime.MinValue;" not in content:
        content = content.replace("DateTime firstCastDetectedTime = DateTime.MinValue;", "DateTime firstCastDetectedTime = DateTime.MinValue;\n                        DateTime lastCTMWriteTime = DateTime.MinValue;\n                        int castDropFrames = 0;")

    # 2. Update KickBot logic block
    # We will search for the entire KickBot block and replace it.
    old_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk (CTM Behind Target)
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

    new_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk (CTM Behind Target)
                            if (p.CastingSpellId != 0 || p.ChannelSpellId != 0)
                            {
                                if (p.Guid == activeChar.TargetGuid)
                                {
                                    castDropFrames = 0; // Reset drop protection

                                    if (currentCastingGuid != p.Guid)
                                    {
                                        currentCastingGuid = p.Guid;
                                        firstCastDetectedTime = DateTime.Now;
                                    }

                                    if (p.Distance <= 6.0f) // Melee range
                                    {
                                        // Esperamos un margen de 500ms para simular corte tardío (800ms a veces es muy tarde para casteos de 1.5s)
                                        if ((DateTime.Now - firstCastDetectedTime).TotalMilliseconds >= 500)
                                        {
                                            if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                            {
                                                shouldKick = true;
                                                lastKickTime = DateTime.Now;
                                                System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] -> KICK Delayed! Dist={p.Distance:F1}\\n");
                                            }
                                        }
                                    }
                                    else if (p.Distance < 30f) // Si está muy lejos, corremos a él
                                    {
                                        // Auto-posicionamiento: Para evitar stuttering, solo escribimos a CTM cada 300ms
                                        if ((DateTime.Now - lastCTMWriteTime).TotalMilliseconds > 300)
                                        {
                                            // En 3.3.5a, calcular la espalda puede fallar si el enemigo se mueve o rotamos mal.
                                            // Escribir solo X,Y,Z y Action 4 a veces no activa el pathing. 
                                            // Lo combinamos con Interactuar (F8) para que el juego invoque ClickToMove internamente,
                                            // pero sobrescribimos la coordenada CTM para engañar al juego e ir a su espalda.
                                            float backDist = 2.5f; 
                                            float destX = p.X - (float)Math.Cos(p.Facing) * backDist;
                                            float destY = p.Y - (float)Math.Sin(p.Facing) * backDist;
                                            
                                            const uint CTM_BASE = 0x00CA11D8;
                                            activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x8C), destX);
                                            activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x90), destY);
                                            activeChar.Mem.WriteFloat((IntPtr)(CTM_BASE + 0x94), p.Z); 
                                            activeChar.Mem.WriteUInt((IntPtr)(CTM_BASE + 0x1C), 4); // 4 = Move
                                            
                                            // También enviamos F8 como salvavidas híbrido para que el personaje corra nativamente
                                            shouldWalk = true;
                                            lastCTMWriteTime = DateTime.Now;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (p.Guid == currentCastingGuid)
                                {
                                    castDropFrames++;
                                    // Anti-micro cortes: Solo resetear si no lo vimos castear por 10 frames seguidos (aprox 150ms)
                                    if (castDropFrames > 10)
                                    {
                                        currentCastingGuid = 0; 
                                        firstCastDetectedTime = DateTime.MinValue;
                                    }
                                }
                            }"""

    if old_kick in content:
        content = content.replace(old_kick, new_kick)
    else:
        print("Could not find old kick logic")

    # Add back VK_F8 key pressing support
    old_trigger = """                        if (shouldEquip) SendKeyToWoW(VK_F10);
                        if (shouldKick) 
                        {
                            SendKeyToWoW(VK_F9);
                            System.Threading.Thread.Sleep(10);
                            SendKeyToWoW(0x33);
                        }"""
    
    new_trigger = """                        if (shouldEquip) SendKeyToWoW(VK_F10);
                        if (shouldWalk) SendKeyToWoW(VK_F8);
                        if (shouldKick) 
                        {
                            SendKeyToWoW(VK_F9);
                            System.Threading.Thread.Sleep(10);
                            SendKeyToWoW(0x33);
                        }"""

    if old_trigger in content:
        content = content.replace(old_trigger, new_trigger)
    
    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
