import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Add targetPidOverride
    if 'private int targetPidOverride = 0;' not in content:
        content = content.replace(
            'private string statusMessage = "Conectando a WoW...";',
            'private string statusMessage = "Conectando a WoW...";\n        private int targetPidOverride = 0;'
        )

    # Rewrite SwitchCharacters
    old_switch = """        public void SwitchCharacters()
        {
            lock (dataLock)
            {
                var temp = char1;
                char1 = char2;
                char2 = temp;
            }
        }"""
    
    new_switch = """        public void SwitchCharacters()
        {
            System.Diagnostics.Process[] wowProcs = System.Diagnostics.Process.GetProcessesByName("Wow");
            if (wowProcs.Length == 0) wowProcs = System.Diagnostics.Process.GetProcessesByName("WoW");
            if (wowProcs.Length == 0) return;
            
            int idx = -1;
            for(int i=0; i<wowProcs.Length; i++) {
                if (wowProcs[i].Id == targetPidOverride) { idx = i; break; }
            }
            
            idx++;
            if (idx >= wowProcs.Length) idx = 0;
            
            targetPidOverride = wowProcs[idx].Id;
            char1.Connected = false;
        }"""
    
    content = content.replace(old_switch, new_switch)

    # Rewrite ReloadDynamicConnections completely
    old_reload = """        private void ReloadDynamicConnections()
        {
            Process[] wowProcs = Process.GetProcessesByName("Wow");
            if (wowProcs.Length == 0) wowProcs = Process.GetProcessesByName("WoW");

            if (wowProcs.Length == 0)
            {
                char1.Connected = false;
                char2.Connected = false;
                return;
            }

            // Validar procesos existentes
            ValidateProcess(char1);
            ValidateProcess(char2);

            foreach (var proc in wowProcs)
            {
                if (char1.Connected && char1.Pid == proc.Id) continue;
                if (char2.Connected && char2.Pid == proc.Id) continue;

                IntPtr handle = Memory.OpenProcess(
                    Memory.ProcessAccessFlags.VirtualMemoryRead |
                    Memory.ProcessAccessFlags.QueryInformation,
                    false, (uint)proc.Id);

                if (handle == IntPtr.Zero) continue;

                var mem = new Memory(handle);
                ulong localGuid = mem.ReadSafe<ulong>((IntPtr)STATIC_LOCAL_PLAYER_GUID);
                uint clientConn = mem.ReadSafe<uint>((IntPtr)ADDR_CLIENT_CONNECTION);
                uint objMgr = mem.ReadSafe<uint>((IntPtr)(clientConn + OFFSET_OBJ_MGR_FROM_CC));

                if (objMgr == 0 || localGuid == 0) continue;

                // Asignar dinámicamente a la primera ranura libre
                if (!char1.Connected)
                {
                    char1.Pid = proc.Id;
                    char1.Mem = mem;
                    char1.Guid = localGuid;
                    char1.ObjMgr = objMgr;
                    char1.Name = ReadString(mem, 0x00C79D18, 30);
                    char1.Connected = true;
                }
                else if (!char2.Connected && proc.Id != char1.Pid)
                {
                    char2.Pid = proc.Id;
                    char2.Mem = mem;
                    char2.Guid = localGuid;
                    char2.ObjMgr = objMgr;
                    char2.Name = ReadString(mem, 0x00C79D18, 30);
                    char2.Connected = true;
                }
            }
        }"""

    new_reload = """        private void ReloadDynamicConnections()
        {
            Process[] wowProcs = Process.GetProcessesByName("Wow");
            if (wowProcs.Length == 0) wowProcs = Process.GetProcessesByName("WoW");

            if (wowProcs.Length == 0)
            {
                char1.Connected = false;
                char2.Connected = false;
                return;
            }

            if (targetPidOverride == 0)
            {
                targetPidOverride = wowProcs[0].Id;
            }

            ValidateProcess(char1);

            foreach (var proc in wowProcs)
            {
                if (proc.Id == targetPidOverride)
                {
                    if (!char1.Connected || char1.Pid != proc.Id)
                    {
                        IntPtr handle = Memory.OpenProcess(
                            Memory.ProcessAccessFlags.VirtualMemoryRead |
                            Memory.ProcessAccessFlags.QueryInformation,
                            false, (uint)proc.Id);

                        if (handle != IntPtr.Zero)
                        {
                            var mem = new Memory(handle);
                            ulong localGuid = mem.ReadSafe<ulong>((IntPtr)STATIC_LOCAL_PLAYER_GUID);
                            uint clientConn = mem.ReadSafe<uint>((IntPtr)ADDR_CLIENT_CONNECTION);
                            uint objMgr = mem.ReadSafe<uint>((IntPtr)(clientConn + OFFSET_OBJ_MGR_FROM_CC));

                            if (objMgr != 0 && localGuid != 0)
                            {
                                char1.Pid = proc.Id;
                                char1.Mem = mem;
                                char1.Guid = localGuid;
                                char1.ObjMgr = objMgr;
                                char1.Name = ReadString(mem, 0x00C79D18, 30);
                                char1.Connected = true;
                            }
                        }
                    }
                }
            }
            char2.Connected = false; // Disable char2 entirely for simplicity
        }"""

    content = content.replace(old_reload, new_reload)

    # Make sure status message is simpler
    old_status = """                if (char1.Connected && char2.Connected)
                {
                    float dx = char2.X - char1.X;
                    float dy = char2.Y - char1.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    if (selectedTetherGuid != 0)
                        statusMessage = $"Objetivo fijado: {selectedTetherName}. Haz clic en su tarjeta para deseleccionar.";
                    else
                        statusMessage = $"✓ Ambas cuentas activas (distancia: {dist:F0} yds). Radar estático activado.";
                }
                else if (char1.Connected || char2.Connected)
                {
                    var active = char1.Connected ? char1 : char2;
                    if (selectedTetherGuid != 0)
                        statusMessage = $"Conectado a {active.Name} (PID: {active.Pid}). Objetivo: {selectedTetherName}";
                    else
                        statusMessage = $"Conectado a {active.Name} (PID: {active.Pid}). Haz clic en la barra derecha para fijar un objetivo.";
                }"""

    new_status = """                if (char1.Connected)
                {
                    if (selectedTetherGuid != 0)
                        statusMessage = $"Conectado a {char1.Name} (PID: {char1.Pid}). Objetivo: {selectedTetherName}";
                    else
                        statusMessage = $"Conectado a {char1.Name} (PID: {char1.Pid}). Cambia de Pj en Ajustes.";
                }"""
    
    content = content.replace(old_status, new_status)

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
