import sys

def modify_program():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Update WoWCharacter
    content = content.replace(
        'public byte RaceId { get; set; } // ID de raza de WoW\n        }',
        'public byte RaceId { get; set; } // ID de raza de WoW\n            public ulong TargetGuid { get; set; } // NEW: Objetivo del jugador\n        }'
    )

    # 2. Update UpdateCharData to read TargetGuid
    content = content.replace(
        'character.FactionId = character.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xD4));',
        'character.FactionId = character.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xD4));\n                        character.TargetGuid = character.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x48));'
    )

    # 3. Update RadarConfig
    content = content.replace(
        'public bool TrackGameObjects { get; set; } = true;\n            public bool PlayAudioAlerts { get; set; } = true;',
        'public bool TrackGameObjects { get; set; } = true;\n            public bool TrackNPCs { get; set; } = false;\n            public bool FilterZAxis { get; set; } = false;\n            public bool PlayAudioAlerts { get; set; } = true;'
    )

    # 4. Update PlayerInfo
    content = content.replace(
        'public bool IsGameObject { get; set; } // ¿Es un nodo/cofre?',
        'public bool IsGameObject { get; set; } // ¿Es un nodo/cofre?\n            public bool IsNPC { get; set; } // ¿Es un monstruo/NPC?'
    )

    # 5. GetNearbyPlayers Types & dead filter
    content = content.replace(
        'if (type == 4 || type == 5) // Player o GameObject',
        'if (type == 3 || type == 4 || type == 5) // Unit, Player o GameObject'
    )
    
    content = content.replace(
        'var player = new PlayerInfo { Guid = guid, IsGameObject = (type == 5) };\n                        player.Name = player.IsGameObject ? "Objeto" : ResolvePlayerName(inst, guid);',
        'var player = new PlayerInfo { Guid = guid, IsGameObject = (type == 5), IsNPC = (type == 3) };\n                        player.Name = player.IsGameObject ? "Objeto" : (player.IsNPC ? "NPC/Mob" : ResolvePlayerName(inst, guid));'
    )

    content = content.replace(
        'if (descPtr > 0x10000 && !player.IsGameObject)\n                        {\n                            player.Level = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));',
        'if (descPtr > 0x10000 && !player.IsGameObject)\n                        {\n                            player.Level = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));\n                            player.Hp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));\n                            if (player.Hp <= 0) { curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT)); continue; } // Skip dead'
    )
    
    # We already read HP in my replacement above, so remove the duplicate read below
    content = content.replace(
        'player.Level = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));\n                            player.Hp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));\n                            if (player.Hp <= 0) { curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT)); continue; } // Skip dead\n                            player.Hp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));\n                            player.MaxHp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_MAX_HEALTH));',
        'player.Level = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));\n                            player.Hp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));\n                            if (player.Hp <= 0) { curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT)); continue; } // Skip dead\n                            player.MaxHp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_MAX_HEALTH));'
    )

    # Filter GameObjects & NPCs inside the display loop (OnPaint)
    content = content.replace(
        'if (p.IsGameObject && !Config.TrackGameObjects) continue;\n                        if (!p.IsGameObject && !Config.TrackPlayers) continue;',
        'if (p.IsGameObject && !Config.TrackGameObjects) continue;\n                        if (p.IsNPC && !Config.TrackNPCs) continue;\n                        if (!p.IsGameObject && !p.IsNPC && !Config.TrackPlayers) continue;\n                        \n                        if (Config.FilterZAxis && Math.Abs(p.Z - activeChar.Z) > 30f) continue; // Filter by Z-Axis'
    )

    # Highlight Target
    content = content.replace(
        '// Si el jugador me está haciendo target -> Dibujar un círculo pulsante/aviso de peligro a su alrededor',
        '// Si es mi Target, resaltarlo con un círculo blanco\n                        if (activeChar.TargetGuid == p.Guid)\n                        {\n                            using (Pen targetPen = new Pen(Color.White, 2f))\n                            {\n                                g.DrawEllipse(targetPen, otherScreenX - 12, otherScreenY - 12, 24, 24);\n                            }\n                        }\n\n                        // Si el jugador me está haciendo target -> Dibujar un círculo pulsante/aviso de peligro a su alrededor'
    )
    
    # 6. Background Thread Logic
    # Add scanner variables to RadarForm
    content = content.replace(
        'private System.Windows.Forms.Timer updateTimer;',
        'private object dataLock = new object();\n        private List<PlayerInfo> scannedPlayers = new List<PlayerInfo>();\n        private WoWCharacter safeActiveChar = null;\n        private System.Threading.Thread scannerThread;\n\n        private System.Windows.Forms.Timer updateTimer;'
    )

    # Start thread in constructor
    content = content.replace(
        'updateTimer = new System.Windows.Forms.Timer();\n            updateTimer.Interval = 16; \n            updateTimer.Tick += UpdateTick;\n            updateTimer.Start();',
        'updateTimer = new System.Windows.Forms.Timer();\n            updateTimer.Interval = 16; \n            updateTimer.Tick += UpdateTick;\n            updateTimer.Start();\n\n            scannerThread = new System.Threading.Thread(MemoryScannerLoop);\n            scannerThread.IsBackground = true;\n            scannerThread.Start();'
    )

    # Add scanner method before UpdateTick
    scanner_method = """
        private void MemoryScannerLoop()
        {
            while (true)
            {
                try
                {
                    ReloadDynamicConnections();

                    if (char1.Connected) UpdateCharData(char1);
                    if (char2.Connected) UpdateCharData(char2);

                    WoWCharacter activeChar = char1.Connected ? char1 : (char2.Connected ? char2 : null);
                    
                    if (activeChar != null)
                    {
                        var rawPlayers = GetNearbyPlayers(activeChar);
                        lock (dataLock)
                        {
                            scannedPlayers = rawPlayers;
                            safeActiveChar = new WoWCharacter {
                                X = activeChar.X, Y = activeChar.Y, Z = activeChar.Z, Facing = activeChar.Facing, Name = activeChar.Name,
                                Pid = activeChar.Pid, FactionId = activeChar.FactionId, RaceId = activeChar.RaceId, TargetGuid = activeChar.TargetGuid,
                                Connected = true
                            };
                        }
                    }
                    else
                    {
                        lock (dataLock)
                        {
                            scannedPlayers = new List<PlayerInfo>();
                            safeActiveChar = null;
                        }
                    }
                }
                catch { }
                System.Threading.Thread.Sleep(33); // ~30 TPS para el escaneo de memoria
            }
        }

"""
    content = content.replace('private void UpdateTick(object? sender, EventArgs e)', scanner_method + '        private void UpdateTick(object? sender, EventArgs e)')

    # Modify UpdateTick to use safe data
    update_tick_old = """        private void UpdateTick(object? sender, EventArgs e)
        {
            try
            {
                // Asegurar de forma agresiva que las ventanas sigan flotando sobre el juego en cada tick
                this.TopMost = true;
                if (detailsForm != null && !detailsForm.IsDisposed)
                {
                    detailsForm.TopMost = true;
                }

                // 1. Recargar dinámicamente las conexiones en cada tick
                // Esto garantiza que sobreviva a teletransportes, portales, pantallas de carga y logouts.
                ReloadDynamicConnections();

                if (char1.Connected) UpdateCharData(char1);
                if (char2.Connected) UpdateCharData(char2);

                // Escanear jugadores alrededor del personaje de control principal (char1 si está activo, sino char2)
                WoWCharacter? activeChar = char1.Connected ? char1 : (char2.Connected ? char2 : null);
                if (activeChar != null)
                {
                    var rawPlayers = GetNearbyPlayers(activeChar);
                    nearbyPlayers = SmoothPlayerMovement(rawPlayers, activeChar);"""
                    
    update_tick_new = """        private void UpdateTick(object? sender, EventArgs e)
        {
            try
            {
                // Asegurar de forma agresiva que las ventanas sigan flotando sobre el juego en cada tick
                this.TopMost = true;
                if (detailsForm != null && !detailsForm.IsDisposed)
                {
                    detailsForm.TopMost = true;
                }

                WoWCharacter activeChar = null;
                List<PlayerInfo> currentRawPlayers = new List<PlayerInfo>();

                lock (dataLock)
                {
                    activeChar = safeActiveChar;
                    if (scannedPlayers != null)
                        currentRawPlayers = new List<PlayerInfo>(scannedPlayers);
                }

                if (activeChar != null)
                {
                    nearbyPlayers = SmoothPlayerMovement(currentRawPlayers, activeChar);"""
                    
    content = content.replace(update_tick_old, update_tick_new)
    
    # Check if we should fake char1 and char2 for statusMessage
    status_update_old = """                // Mensaje de estado dinámico
                if (char1.Connected && char2.Connected)"""
    status_update_new = """                // Mensaje de estado dinámico
                bool isConnected = activeChar != null;
                if (char1.Connected && char2.Connected)"""
    content = content.replace(status_update_old, status_update_new)

    # 7. Update SettingsForm Checkboxes
    settings_old = """            CheckBox chkNodes = CreateCheckBox("Rastrear Minas y Hierbas", radar.Config.TrackGameObjects, yOffset);
            yOffset += 35;

            CheckBox chkAudio = CreateCheckBox("Alertas Sonoras (Target)", radar.Config.PlayAudioAlerts, yOffset);"""
            
    settings_new = """            CheckBox chkNodes = CreateCheckBox("Rastrear Minas y Hierbas", radar.Config.TrackGameObjects, yOffset);
            yOffset += 25;

            CheckBox chkNPCs = CreateCheckBox("Rastrear NPCs/Monstruos", radar.Config.TrackNPCs, yOffset);
            yOffset += 25;

            CheckBox chkZAxis = CreateCheckBox("Filtro Altura Z-Axis", radar.Config.FilterZAxis, yOffset);
            yOffset += 35;

            CheckBox chkAudio = CreateCheckBox("Alertas Sonoras (Target)", radar.Config.PlayAudioAlerts, yOffset);"""
    
    content = content.replace(settings_old, settings_new)
    
    settings_evt_old = """            chkNodes.CheckedChanged += (s, e) => { radar.Config.TrackGameObjects = chkNodes.Checked; radar.SaveConfig(); };
            chkAudio.CheckedChanged += (s, e) => { radar.Config.PlayAudioAlerts = chkAudio.Checked; radar.SaveConfig(); };"""
    settings_evt_new = """            chkNodes.CheckedChanged += (s, e) => { radar.Config.TrackGameObjects = chkNodes.Checked; radar.SaveConfig(); };
            chkNPCs.CheckedChanged += (s, e) => { radar.Config.TrackNPCs = chkNPCs.Checked; radar.SaveConfig(); };
            chkZAxis.CheckedChanged += (s, e) => { radar.Config.FilterZAxis = chkZAxis.Checked; radar.SaveConfig(); };
            chkAudio.CheckedChanged += (s, e) => { radar.Config.PlayAudioAlerts = chkAudio.Checked; radar.SaveConfig(); };"""
            
    content = content.replace(settings_evt_old, settings_evt_new)
    
    # Save the updated content
    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
