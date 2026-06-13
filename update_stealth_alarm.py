import sys

def modify_program():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Replace lastStealthAlertTime with HashSet
    old_var = """private DateTime lastStealthAlertTime = DateTime.MinValue;"""
    new_var = """private HashSet<ulong> alertedStealthGuids = new HashSet<ulong>();"""
    content = content.replace(old_var, new_var)

    # 2. Add currentStealthGuids before foreach
    old_pre_loop = """                        bool shouldFocus = false;
                        bool shouldEquip = false;

                        foreach(var p in rawPlayers)"""
    
    new_pre_loop = """                        bool shouldFocus = false;
                        bool shouldEquip = false;
                        
                        HashSet<ulong> currentStealthGuids = new HashSet<ulong>();

                        foreach(var p in rawPlayers)"""
    content = content.replace(old_pre_loop, new_pre_loop)

    # 3. Modify inside foreach loop
    old_logic = """                            // Anti-Rogue (Alerta de sigilo)
                            if (Config.AntiRogueAlerts && p.IsStealthed && p.Distance < 60f)
                            {
                                if ((DateTime.Now - lastStealthAlertTime).TotalSeconds > 10.0) // Alarma cada 10 segs
                                {
                                    System.Media.SystemSounds.Hand.Play(); // Sonido de error crítico de Windows
                                    lastStealthAlertTime = DateTime.Now;
                                }
                            }"""
                            
    new_logic = """                            // Anti-Rogue (Alerta de sigilo)
                            if (Config.AntiRogueAlerts && p.IsStealthed && p.Distance < 60f)
                            {
                                currentStealthGuids.Add(p.Guid);
                                if (!alertedStealthGuids.Contains(p.Guid))
                                {
                                    System.Media.SystemSounds.Hand.Play(); // Sonido de error crítico de Windows
                                    alertedStealthGuids.Add(p.Guid);
                                }
                            }"""
    content = content.replace(old_logic, new_logic)

    # 4. Add IntersectWith after foreach loop
    old_post_loop = """                        if (shouldFocus) SendKeyToWoW(VK_F11);
                        if (shouldEquip) SendKeyToWoW(VK_F10);"""
                        
    new_post_loop = """                        alertedStealthGuids.IntersectWith(currentStealthGuids); // Limpia pícaros que salieron de rango o quitaron sigilo

                        if (shouldFocus) SendKeyToWoW(VK_F11);
                        if (shouldEquip) SendKeyToWoW(VK_F10);"""
    content = content.replace(old_post_loop, new_post_loop)

    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
