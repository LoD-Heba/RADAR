import sys

def modify_program():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add PostMessage imports to Memory class or Program
    post_message_code = """
        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const int VK_F11 = 0x7A;
        public const int VK_F12 = 0x7B;

        public static void SendKeyToWoW(int vk)
        {
            IntPtr hwnd = FindWindow(null, "World of Warcraft");
            if (hwnd != IntPtr.Zero)
            {
                PostMessage(hwnd, WM_KEYDOWN, vk, 0);
                System.Threading.Thread.Sleep(50);
                PostMessage(hwnd, WM_KEYUP, vk, 0);
            }
        }
"""
    # Insert it inside RadarForm class or Program class. Let's put it at the end of WoWCharacter class
    content = content.replace('class WoWCharacter\n        {', post_message_code + '\n        class WoWCharacter\n        {')

    # 2. Update RadarConfig
    config_old = """public bool TrackNPCs { get; set; } = false;
            public bool FilterZAxis { get; set; } = false;
            public bool PlayAudioAlerts { get; set; } = true;"""
    config_new = """public bool TrackNPCs { get; set; } = false;
            public bool FilterZAxis { get; set; } = false;
            public bool PlayAudioAlerts { get; set; } = true;
            public bool AutoFocusThreat { get; set; } = false;
            public bool AutoEquipPvP { get; set; } = false;"""
    content = content.replace(config_old, config_new)

    # 3. Add cooldown variables to RadarForm
    cooldowns = """
        private DateTime lastAutoFocusTime = DateTime.MinValue;
        private DateTime lastAutoEquipTime = DateTime.MinValue;
"""
    content = content.replace('private System.Windows.Forms.Timer updateTimer;', cooldowns + '\n        private System.Windows.Forms.Timer updateTimer;')

    # 4. Add the bot logic inside MemoryScannerLoop after processing nearbyPlayersRaw
    logic_inject_old = """                    if (activeChar != null)
                    {
                        var rawPlayers = GetNearbyPlayers(activeChar);"""
    
    logic_inject_new = """                    if (activeChar != null)
                    {
                        var rawPlayers = GetNearbyPlayers(activeChar);

                        // Lógica híbrida Activa (Botting seguro)
                        bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
                        bool shouldFocus = false;
                        bool shouldEquip = false;

                        foreach(var p in rawPlayers)
                        {
                            if (p.IsGameObject || p.IsNPC) continue;
                            bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
                            if (isCharAlliance == isPlayerAlliance) continue; // Es aliado

                            // Auto-Equip si un enemigo jugador se acerca a menos de 15 yardas
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
                            }
                        }

                        if (shouldFocus) SendKeyToWoW(VK_F11);
                        if (shouldEquip) SendKeyToWoW(VK_F12);
"""
    content = content.replace(logic_inject_old, logic_inject_new)

    # 5. Add settings checkboxes in SettingsForm
    settings_old = """            CheckBox chkZAxis = CreateCheckBox("Filtro Altura Z-Axis", radar.Config.FilterZAxis, yOffset);
            yOffset += 35;

            CheckBox chkAudio = CreateCheckBox("Alertas Sonoras (Target)", radar.Config.PlayAudioAlerts, yOffset);"""
            
    settings_new = """            CheckBox chkZAxis = CreateCheckBox("Filtro Altura Z-Axis", radar.Config.FilterZAxis, yOffset);
            yOffset += 35;

            CheckBox chkAudio = CreateCheckBox("Alertas Sonoras (Target)", radar.Config.PlayAudioAlerts, yOffset);
            yOffset += 35;
            
            CheckBox chkFocus = CreateCheckBox("[Híbrido] Auto-Target Enemigos", radar.Config.AutoFocusThreat, yOffset);
            yOffset += 25;
            
            CheckBox chkEquip = CreateCheckBox("[Híbrido] Auto-Equipar PvP", radar.Config.AutoEquipPvP, yOffset);"""
    
    content = content.replace(settings_old, settings_new)

    # Update size of Settings form
    content = content.replace('this.Size = new Size(250, 260);', 'this.Size = new Size(250, 320);')

    # Add events for new checkboxes
    events_old = """            chkZAxis.CheckedChanged += (s, e) => { radar.Config.FilterZAxis = chkZAxis.Checked; radar.SaveConfig(); };
            chkAudio.CheckedChanged += (s, e) => { radar.Config.PlayAudioAlerts = chkAudio.Checked; radar.SaveConfig(); };"""
            
    events_new = """            chkZAxis.CheckedChanged += (s, e) => { radar.Config.FilterZAxis = chkZAxis.Checked; radar.SaveConfig(); };
            chkAudio.CheckedChanged += (s, e) => { radar.Config.PlayAudioAlerts = chkAudio.Checked; radar.SaveConfig(); };
            chkFocus.CheckedChanged += (s, e) => { radar.Config.AutoFocusThreat = chkFocus.Checked; radar.SaveConfig(); };
            chkEquip.CheckedChanged += (s, e) => { radar.Config.AutoEquipPvP = chkEquip.Checked; radar.SaveConfig(); };"""
            
    content = content.replace(events_old, events_new)

    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
