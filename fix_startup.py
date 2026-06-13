import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # 1. Add ProcessSelectorForm at the end
    if 'public class ProcessSelectorForm' not in content:
        selector_form = """
    public class ProcessSelectorForm : Form
    {
        public int SelectedPid { get; private set; }
        private ListBox listBox;

        public ProcessSelectorForm()
        {
            this.Text = "Seleccionar Ventana de WoW";
            this.Size = new Size(300, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Label lbl = new Label();
            lbl.Text = "Ventanas de World of Warcraft detectadas:";
            lbl.Location = new Point(10, 10);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);

            listBox = new ListBox();
            listBox.Location = new Point(10, 30);
            listBox.Size = new Size(260, 120);
            this.Controls.Add(listBox);

            Button btnRefresh = new Button();
            btnRefresh.Text = "Actualizar";
            btnRefresh.Location = new Point(10, 160);
            btnRefresh.Size = new Size(120, 30);
            btnRefresh.Click += (s, e) => LoadProcesses();
            this.Controls.Add(btnRefresh);

            Button btnConnect = new Button();
            btnConnect.Text = "Conectar";
            btnConnect.Location = new Point(150, 160);
            btnConnect.Size = new Size(120, 30);
            btnConnect.Click += BtnConnect_Click;
            this.Controls.Add(btnConnect);

            LoadProcesses();
        }

        private void LoadProcesses()
        {
            listBox.Items.Clear();
            var procs = System.Diagnostics.Process.GetProcessesByName("Wow");
            if (procs.Length == 0) procs = System.Diagnostics.Process.GetProcessesByName("WoW");

            foreach (var p in procs)
            {
                // Intentar leer el nombre de la ventana y el PID
                listBox.Items.Add($"WoW (PID: {p.Id}) - {p.MainWindowTitle}");
            }
            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;
            else
                listBox.Items.Add("No se encontró ningún proceso Wow.exe");
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (listBox.SelectedItem != null && listBox.SelectedItem.ToString().Contains("PID"))
            {
                string text = listBox.SelectedItem.ToString();
                int start = text.IndexOf("PID: ") + 5;
                int end = text.IndexOf(")", start);
                if (int.TryParse(text.Substring(start, end - start), out int pid))
                {
                    SelectedPid = pid;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            else
            {
                MessageBox.Show("Por favor, selecciona un proceso válido.");
            }
        }
    }
"""
        content = content.replace('\n}', selector_form + '\n}')

    # 2. Modify Main
    old_main = """        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RadarForm());
        }"""
    
    new_main = """        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            int selectedPid = 0;
            using (var selector = new ProcessSelectorForm())
            {
                if (selector.ShowDialog() == DialogResult.OK)
                {
                    selectedPid = selector.SelectedPid;
                }
                else
                {
                    return; // Salir si el usuario cancela
                }
            }

            Application.Run(new RadarForm(selectedPid));
        }"""
    
    content = content.replace(old_main, new_main)

    # 3. Add constructor to RadarForm and member targetPid
    content = content.replace('private int targetPidOverride = 0;', 'private int targetPid = 0;')
    content = content.replace('targetPidOverride = wowProcs[0].Id;', '') # cleanup
    content = content.replace('targetPidOverride = wowProcs[idx].Id;', '') # cleanup
    
    old_ctor = """        public RadarForm()
        {
            InitializeComponent();"""
    
    new_ctor = """        public RadarForm(int targetPid)
        {
            this.targetPid = targetPid;
            InitializeComponent();"""
    
    content = content.replace(old_ctor, new_ctor)
    
    # Also update RadarForm default constructor if it exists for designer
    # We replaced the only one. But if we need a parameterless for Designer, we add it.
    new_ctor_with_def = """        public RadarForm()
        {
            InitializeComponent();
        }
        
        public RadarForm(int targetPid)
        {
            this.targetPid = targetPid;
            InitializeComponent();"""
    content = content.replace(new_ctor, new_ctor_with_def)

    # 4. Modify ReloadDynamicConnections
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
    
    new_reload = """        private void ReloadDynamicConnections()
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

    content = content.replace(old_reload, new_reload)

    # 5. Remove Switch button from SettingsForm
    start_str = "Button btnSwitch = new Button();"
    end_str = "this.Controls.Add(btnSwitch);"
    if start_str in content and end_str in content:
        s_idx = content.find(start_str)
        e_idx = content.find(end_str) + len(end_str)
        # also remove yOffset += 75;
        snippet = content[s_idx:e_idx]
        content = content.replace(snippet, "")
        content = content.replace("yOffset += 75;", "")
    
    with open(cs_path, 'w', encoding='utf-8') as f:
        f.write(content)

modify_program()
