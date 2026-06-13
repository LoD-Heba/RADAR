import sys
import os

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add lastKickTime
    if 'DateTime lastKickTime' not in content:
        content = content.replace(
            'private HashSet<ulong> alertedStealthGuids = new HashSet<ulong>();',
            'private HashSet<ulong> alertedStealthGuids = new HashSet<ulong>();\n        private DateTime lastKickTime = DateTime.MinValue;'
        )

    # 2. Fix offsets to 0xA60 and 0xA6C
    content = content.replace('0xC8C', '0xA60')
    content = content.replace('0xC90', '0xA6C')

    # 3. Remove the manual scan block to keep it clean
    scan_block = """                    // --- BÚSQUEDA AUTOMÁTICA DE CASTEO ---
                    var searchSb = new System.Text.StringBuilder();
                    searchSb.AppendLine("=== RESULTADOS DE BÚSQUEDA DE HECHIZO ===");
                    bool foundAny = false;
                    for (int offset = 0x800; offset < 0x1500; offset += 4)
                    {
                        int val = character.Mem.ReadSafe<int>((IntPtr)(curObj + offset));
                        if (val == 8690)
                        {
                            searchSb.AppendLine($"¡Piedra de Hogar (8690) encontrada en el offset: 0x{offset:X}!");
                            foundAny = true;
                        }
                    }
                    if (foundAny)
                    {
                        System.IO.File.WriteAllText("spell_search.txt", searchSb.ToString());
                    }
                    // -------------------------------------"""
    content = content.replace(scan_block, "")

    # 4. Add shouldKick variable
    if 'bool shouldKick = false;' not in content:
        content = content.replace(
            'bool shouldEquip = false;',
            'bool shouldEquip = false;\n                        bool shouldKick = false;'
        )

    # 5. Add KickBot logic inside foreach
    kick_logic = """
                            // KickBot (Auto-Interrupt)
                            if (p.Guid == activeChar.TargetGuid && p.Distance < 30f && (p.CastingSpellId != 0 || p.ChannelSpellId != 0))
                            {
                                if ((DateTime.Now - lastKickTime).TotalSeconds > 1.5)
                                {
                                    shouldKick = true;
                                    lastKickTime = DateTime.Now;
                                }
                            }"""
    if 'KickBot (Auto-Interrupt)' not in content:
        content = content.replace(
            '// Anti-Rogue (Alerta de sigilo)',
            kick_logic + '\n\n                            // Anti-Rogue (Alerta de sigilo)'
        )

    # 6. Add the VK_F9 keypress after the loop
    if 'shouldKick) SendKeyToWoW' not in content:
        content = content.replace(
            'if (shouldEquip) SendKeyToWoW(VK_F10);',
            'if (shouldEquip) SendKeyToWoW(VK_F10);\n                        if (shouldKick) SendKeyToWoW(VK_F9);'
        )

    # Add VK_F9 definition
    if 'public const int VK_F9 = 0x78;' not in content:
        content = content.replace(
            'public const int VK_F10 = 0x79;',
            'public const int VK_F9 = 0x78;\n        public const int VK_F10 = 0x79;'
        )

    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)


def modify_lua():
    lua_path = r'RadarLinkAddon\RadarLinkAddon.lua'
    with open(lua_path, 'r', encoding='utf-8') as f:
        content = f.read()

    kick_btn = """
    local kickBtn = CreateFrame("Button", "RadarLinkKickBtn", UIParent, "SecureActionButtonTemplate")
    kickBtn:SetAttribute("type", "macro")
    -- Macro para cortar el casteo del objetivo enemigo. Incluye varias clases por defecto.
    local kickMacro = "/cast [target=target,harm,nodead] Kick\\n/cast [target=target,harm,nodead] Counterspell\\n/cast [target=target,harm,nodead] Pummel\\n/cast [target=target,harm,nodead] Mind Freeze\\n/cast [target=target,harm,nodead] Spell Lock"
    kickBtn:SetAttribute("macrotext", kickMacro)
    SetBindingClick("F9", "RadarLinkKickBtn")
"""
    if 'RadarLinkKickBtn' not in content:
        content = content.replace(
            'SetBindingClick("F11", "RadarLinkTargetBtn")',
            kick_btn + '\n    SetBindingClick("F11", "RadarLinkTargetBtn")'
        )

    with open(lua_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
modify_lua()
