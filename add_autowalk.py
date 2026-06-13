import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add VK_F8
    if "public const int VK_F8" not in content:
        content = content.replace("public const int VK_F9 = 0x78;", "public const int VK_F8 = 0x77;\n        public const int VK_F9 = 0x78;")

    # 2. Add bool shouldWalk
    if "bool shouldWalk = false;" not in content:
        content = content.replace("bool shouldKick = false;", "bool shouldKick = false;\n                        bool shouldWalk = false;")

    # 3. Update KickBot logic
    old_kick = """                            // KickBot (Auto-Interrupt) + Logging
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

    new_kick = """                            // KickBot (Auto-Interrupt) + Auto-Walk
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
    
    if old_kick in content:
        content = content.replace(old_kick, new_kick)
    else:
        print("Could not find old kick logic!")

    # 4. Trigger send key
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
    else:
        print("Could not find old trigger logic!")

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
