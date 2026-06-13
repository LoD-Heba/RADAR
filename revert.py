import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Search and replace the KickBot block
    old_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk (CTM Behind Target)
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

    new_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk (Nativo)
                            if (p.CastingSpellId != 0 || p.ChannelSpellId != 0)
                            {
                                if (p.Guid == activeChar.TargetGuid)
                                {
                                    if (p.Distance <= 6.0f) // Melee range
                                    {
                                        if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                        {
                                            // Humanizer Delay: Dormimos el hilo del escáner temporalmente 
                                            // para retrasar la patada sin complicar la memoria
                                            System.Threading.Thread.Sleep(350); 
                                            
                                            shouldKick = true;
                                            lastKickTime = DateTime.Now;
                                            System.IO.File.AppendAllText("kick_debug.txt", $"[{DateTime.Now:HH:mm:ss.fff}] -> KICK Delayed! Dist={p.Distance:F1}\\n");
                                        }
                                    }
                                    else if (p.Distance <= 15f) // Reducido a 15 yardas como pidió el usuario
                                    {
                                        // Usa F8 (Interactuar) para correr nativamente
                                        shouldWalk = true;
                                    }
                                }
                            }"""

    if old_kick in content:
        content = content.replace(old_kick, new_kick)
    else:
        print("Could not find Kick logic")

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
