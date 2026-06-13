import sys

def modify_program():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update RadarConfig
    config_old = """public bool AutoEquipPvP { get; set; } = false;"""
    config_new = """public bool AutoEquipPvP { get; set; } = false;
            public bool AntiRogueAlerts { get; set; } = true;"""
    content = content.replace(config_old, config_new)

    # 2. Add lastStealthAlertTime variable
    cooldown_old = """private DateTime lastAutoEquipTime = DateTime.MinValue;"""
    cooldown_new = """private DateTime lastAutoEquipTime = DateTime.MinValue;
        private DateTime lastStealthAlertTime = DateTime.MinValue;"""
    content = content.replace(cooldown_old, cooldown_new)

    # 3. Add Scanner Logic
    logic_old = """// Auto-Focus si un enemigo nos hace target a menos de 40 yardas"""
    logic_new = """// Anti-Rogue (Alerta de sigilo)
                            if (Config.AntiRogueAlerts && p.IsStealthed && p.Distance < 60f)
                            {
                                if ((DateTime.Now - lastStealthAlertTime).TotalSeconds > 4.0) // Alarma cada 4 segs
                                {
                                    System.Media.SystemSounds.Hand.Play(); // Sonido de error crítico de Windows
                                    lastStealthAlertTime = DateTime.Now;
                                }
                            }

                            // Auto-Focus si un enemigo nos hace target a menos de 40 yardas"""
    content = content.replace(logic_old, logic_new)

    # 4. Add OnPaint Logic (Snaplines)
    paint_old = """                        if (p.IsGameObject)"""
    paint_new = """                        // 2. Snapline Anti-Rogue: Dibuja línea roja si está en sigilo
                        if (p.IsStealthed && Config.AntiRogueAlerts)
                        {
                            using (Pen snaplinePen = new Pen(Color.Red, 2f))
                            {
                                snaplinePen.DashStyle = DashStyle.Dash; // Línea punteada roja
                                g.DrawLine(snaplinePen, centerX, centerY, otherScreenX, otherScreenY);
                            }
                        }

                        if (p.IsGameObject)"""
    content = content.replace(paint_old, paint_new)

    # 5. Add SettingsForm Checkbox
    settings_old = """CheckBox chkEquip = CreateCheckBox("[Híbrido] Auto-Equipar PvP", radar.Config.AutoEquipPvP, yOffset);"""
    settings_new = """CheckBox chkEquip = CreateCheckBox("[Híbrido] Auto-Equipar PvP", radar.Config.AutoEquipPvP, yOffset);
            yOffset += 35;
            
            CheckBox chkRogue = CreateCheckBox("[Defensa] Anti-Pícaro (Snapline)", radar.Config.AntiRogueAlerts, yOffset);"""
    content = content.replace(settings_old, settings_new)

    # Expand SettingsForm window size again
    content = content.replace('this.Size = new Size(250, 320);', 'this.Size = new Size(250, 370);')

    # Add Event for checkbox
    event_old = """chkEquip.CheckedChanged += (s, e) => { radar.Config.AutoEquipPvP = chkEquip.Checked; radar.SaveConfig(); };"""
    event_new = """chkEquip.CheckedChanged += (s, e) => { radar.Config.AutoEquipPvP = chkEquip.Checked; radar.SaveConfig(); };
            chkRogue.CheckedChanged += (s, e) => { radar.Config.AntiRogueAlerts = chkRogue.Checked; radar.SaveConfig(); };"""
    content = content.replace(event_old, event_new)

    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
