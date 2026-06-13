import sys
import re

def fix():
    with open('Program.cs', 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Fix RadarForm constructor
    content = content.replace('''        public RadarForm()
        {
            InitializeComponent();''', '''        public RadarForm()
        {
            InitializeComponent();
        }

        public RadarForm(int targetPid)
        {
            this.targetPid = targetPid;
            InitializeComponent();''')

    # 2. Fix targetPidOverride in SwitchCharacters -> just remove SwitchCharacters entirely
    switch_regex = re.compile(r'public void SwitchCharacters\(\)\s*\{.*?char1\.Connected = false;\s*\}', re.DOTALL)
    content = switch_regex.sub('', content)

    # 3. Rewrite ReloadDynamicConnections
    reload_regex = re.compile(r'private void ReloadDynamicConnections\(\)\s*\{.*?char2\.Connected = false;\s*// Disable char2 entirely for simplicity\s*\}', re.DOTALL)
    
    new_reload = """private void ReloadDynamicConnections()
        {
            if (targetPid == 0) return;

            ValidateProcess(char1);

            if (!char1.Connected || char1.Pid != targetPid)
            {
                IntPtr handle = Memory.OpenProcess(
                    Memory.ProcessAccessFlags.VirtualMemoryRead |
                    Memory.ProcessAccessFlags.QueryInformation,
                    false, (uint)targetPid);

                if (handle != IntPtr.Zero)
                {
                    var mem = new Memory(handle);
                    ulong localGuid = mem.ReadSafe<ulong>((IntPtr)STATIC_LOCAL_PLAYER_GUID);
                    uint clientConn = mem.ReadSafe<uint>((IntPtr)ADDR_CLIENT_CONNECTION);
                    uint objMgr = mem.ReadSafe<uint>((IntPtr)(clientConn + OFFSET_OBJ_MGR_FROM_CC));

                    if (objMgr != 0 && localGuid != 0)
                    {
                        char1.Pid = targetPid;
                        char1.Mem = mem;
                        char1.Guid = localGuid;
                        char1.ObjMgr = objMgr;
                        char1.Name = ReadString(mem, 0x00C79D18, 30);
                        char1.Connected = true;
                    }
                }
            }
        }"""
    content = reload_regex.sub(new_reload, content)

    # 4. Fix sender nullability
    content = content.replace('private void BtnConnect_Click(object sender, EventArgs e)', 'private void BtnConnect_Click(object? sender, EventArgs e)')

    with open('Program.cs', 'w', encoding='utf-8') as f:
        f.write(content)

fix()
