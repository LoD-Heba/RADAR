using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.IO;
using KeyAuth;
using System.Windows.Forms;
using System.Media;
using System.Threading.Tasks;
namespace WoWTest
{
    internal class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        public static extern bool DestroyIcon(IntPtr handle);

        public static Icon? LoadPngAsIcon(string pngPath)
        {
            try
            {
                if (System.IO.File.Exists(pngPath))
                {
                    using (Bitmap bmp = new Bitmap(pngPath))
                    {
                        IntPtr hIcon = bmp.GetHicon();
                        Icon tempIcon = Icon.FromHandle(hIcon);
                        Icon icon = (Icon)tempIcon.Clone();
                        DestroyIcon(hIcon);
                        return icon;
                    }
                }
            }
            catch { }
            return null;
        }

        public static Icon? GetApplicationIcon()
        {
            try
            {
                // Buscar recursos incrustados que terminen con LoD.ico
                foreach (string name in typeof(Program).Assembly.GetManifestResourceNames())
                {
                    if (name.EndsWith("LoD.ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var stream = typeof(Program).Assembly.GetManifestResourceStream(name))
                        {
                            if (stream != null)
                            {
                                return new Icon(stream);
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                // Fallback 1: Buscar archivo local LoD.ico en el directorio base
                string icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LoD.ico");
                if (System.IO.File.Exists(icoPath))
                {
                    return new Icon(icoPath);
                }
            }
            catch { }

            try
            {
                // Fallback 2: Buscar archivo local LoD.png en el directorio base
                string pngPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LoD.png");
                return LoadPngAsIcon(pngPath);
            }
            catch { }

            return null;
        }

        [STAThread]
        static async Task Main() // Cambiado a 'async Task' para soportar los procesos en segundo plano de KeyAuth
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1. Inicializar KeyAuth con tus datos del panel web de KeyAuth
            api keyAuthApp = new api(
                name: "RadarWow",
                ownerid: "JY66e6PUAH",
                secret: "4278f356aa6fdaee7b468a353330ec44bb11b4ed302bebf6c837272471e79568", // Reemplaza aquí con el 'Secret' de tu app en KeyAuth
                version: "1.0"
            );

            await keyAuthApp.init();

            // Ruta del archivo local donde guardaremos la clave de licencia
            string rutaLicenciaLocal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licencia.txt");
            string licenciaUsuario = "";

            // INTENTO 1: Comprobar si el archivo de licencia ya existe localmente
            if (File.Exists(rutaLicenciaLocal))
            {
                licenciaUsuario = File.ReadAllText(rutaLicenciaLocal).Trim();
            }

            // Si no hay archivo guardado o estaba vacío, procedemos a pedir la clave mediante la interfaz
            if (string.IsNullOrEmpty(licenciaUsuario))
            {
                using (Form loginForm = new Form { Width = 300, Height = 150, Text = "Activación de Licencia", StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false })
                {
                    Label lbl = new Label { Left = 10, Top = 20, Text = "Introduce tu clave de producto:", Width = 250 };
                    TextBox txt = new TextBox { Left = 10, Top = 45, Width = 260 };
                    Button btn = new Button { Text = "Activar", Left = 190, Top = 80, DialogResult = DialogResult.OK };
                    loginForm.Controls.Add(lbl); loginForm.Controls.Add(txt); loginForm.Controls.Add(btn);
                    loginForm.AcceptButton = btn;

                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        licenciaUsuario = txt.Text.Trim();
                    }
                    else
                    {
                        return; // El usuario canceló la operación cerrando la ventana
                    }
                }
            }

            // 2. Validar la clave de forma asíncrona (sea leída del archivo o ingresada a mano)
            await keyAuthApp.license(licenciaUsuario);

            // Verificar si la respuesta fue exitosa
            if (!keyAuthApp.response.success)
            {
                MessageBox.Show($"Error de activación: {keyAuthApp.response.message}", "Licencia Inválida", MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Si la clave guardada en el archivo local falló (por ejemplo, porque expiró), 
                // borramos el archivo para que la próxima vez le vuelva a pedir una nueva al usuario.
                if (File.Exists(rutaLicenciaLocal))
                {
                    File.Delete(rutaLicenciaLocal);
                }
                return;
            }

            // SÍ FUE EXITOSA: Si la clave es válida y todavía no teníamos el archivo guardado en el disco, lo creamos
            if (!File.Exists(rutaLicenciaLocal))
            {
                File.WriteAllText(rutaLicenciaLocal, licenciaUsuario);
                MessageBox.Show("¡Licencia verificada y guardada correctamente en este equipo!", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            // 3. Continuar con la carga normal de tu Radar si la licencia es válida
            int selectedPid = 0;
            using (var selector = new ProcessSelectorForm())
            {
                if (selector.ShowDialog() == DialogResult.OK)
                {
                    selectedPid = selector.SelectedPid;
                }
                else
                {
                    return;
                }
            }

            Application.Run(new RadarForm(selectedPid));
        }
    }

    public class RadarForm : Form
    {
        // ======================================================
        // OFFSETS DE WoW 3.3.5a (12340)
        // ======================================================
        const uint STATIC_MAP_ID = 0x00AB63BC;
        const uint STATIC_LOCAL_PLAYER_GUID = 0x00CA1238;
        const uint STATIC_FOCUS_GUID = 0x00BD07D0;
        const uint ADDR_CLIENT_CONNECTION = 0x00C79CE0;
        const uint OFFSET_OBJ_MGR_FROM_CC = 0x2ED0;
        const uint OFFSET_FIRST_OBJECT = 0x00AC;
        const uint OFFSET_NEXT_OBJECT = 0x003C;
        const uint OFFSET_OBJ_GUID = 0x30;
        const uint OFFSET_OBJ_TYPE = 0x14;
        const uint OFFSET_OBJ_DESCRIPTOR = 0x08;

        const uint DESC_HEALTH = 0x0060;
        const uint DESC_MAX_HEALTH = 0x0080;
        const uint DESC_LEVEL = 0x00D8;
        const uint DESC_BYTES_0 = 0x005C;

        const uint ADDR_PLAYER_NAME_CACHE = 0x00C5D938;


        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const int VK_F11 = 0x7A;
        public const int VK_F8 = 0x77;
        public const int VK_F9 = 0x78;
        public const int VK_F10 = 0x79; // Cambiado de F12 a F10 por si Windows intercepta F12

        public delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        public static IntPtr GetWoWWindow(int pid)
        {
            IntPtr foundWindow = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                GetWindowThreadProcessId(hWnd, out int windowPid);
                if (windowPid == pid && IsWindowVisible(hWnd))
                {
                    foundWindow = hWnd;
                    return false;
                }
                return true;
            }, 0);
            return foundWindow;
        }


        public class WoWCharacter
        {
            public int Pid { get; set; }
            public Memory Mem { get; set; } = null!;
            public ulong Guid { get; set; }
            public string Name { get; set; } = "Desconocido";
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Facing { get; set; } // Dirección de mirada
            public int Hp { get; set; }
            public int MaxHp { get; set; }
            public int Level { get; set; }
            public string ClassName { get; set; } = "Desconocida";
            public string RaceName { get; set; } = "Desconocida";
            public uint ObjMgr { get; set; }
            public bool Connected { get; set; }
            public bool IsPvPActive { get; set; }
            public uint FactionId { get; set; } // ID de facción de WoW
            public byte RaceId { get; set; } // ID de raza de WoW
            public ulong TargetGuid { get; set; } // NEW: Objetivo del jugador
            public ulong FocusGuid { get; set; } // Focus del jugador
            public int CastingSpellId { get; set; } // Hechizo casteando (0xA60)
            public int ChannelSpellId { get; set; } // Hechizo canalizando (0xA6C)
            public int CastingSpellId2 { get; set; } // Hechizo casteando alt (0xCB0)
        }

        public class PlayerInfo
        {
            public ulong Guid { get; set; }
            public string Name { get; set; } = "Desconocido";
            public int Level { get; set; }
            public int Hp { get; set; }
            public int MaxHp { get; set; }
            public string ClassName { get; set; } = "Desconocida";
            public string RaceName { get; set; } = "Desconocida";
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Distance { get; set; }
            public Rectangle HitBox { get; set; } // Para clicks
            public bool IsPvPActive { get; set; }
            public Color FactionColor { get; set; } = Color.DarkGray;
            public float Facing { get; set; } // Orientación de mirada del otro player
            public int Power { get; set; } // Recurso (Mana, Ira, Energía)
            public int MaxPower { get; set; } = 100; // Recurso Máximo
            public string PowerType { get; set; } = "Maná"; // Tipo de recurso
            public bool IsTargetingMe { get; set; } // ¿Me tiene en target?
            public uint FactionId { get; set; } // ID de facción de WoW
            public byte RaceId { get; set; } // ID de raza de WoW
            public bool IsStealthed { get; set; } // Detección de sigilo
            public bool IsGameObject { get; set; } // ¿Es un nodo/cofre?
            public bool IsNPC { get; set; } // ¿Es un monstruo/NPC?
            public uint AuraState { get; set; } // Depuración (0xF0)
            public uint Flags { get; set; } // Depuración (0xE8)
            public uint Bytes1 { get; set; } // Depuración (0x110)
            public int CastingSpellId { get; set; } // Hechizo casteando
            public int ChannelSpellId { get; set; } // Hechizo canalizando
            public int CastingSpellId2 { get; set; } // Hechizo casteando alt (0xCB0)

            public DateTime LastSeenTime { get; set; } = DateTime.Now;
            public bool IsGhost { get; set; } = false;
            public string GuildName { get; set; } = "";
        }

        private WoWCharacter char1 = new WoWCharacter();
        private WoWCharacter char2 = new WoWCharacter();
        private List<PlayerInfo> nearbyPlayers = new List<PlayerInfo>();
        private ulong selectedTetherGuid = 0; // Jugador seleccionado para enlazar
        private string selectedTetherName = "";
        private uint lastMapId = 99999;

        private class SmoothedPlayerState
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Vx { get; set; }
            public float Vy { get; set; }
            public bool IsInitialized { get; set; }
        }
        private Dictionary<ulong, SmoothedPlayerState> playerStates = new Dictionary<ulong, SmoothedPlayerState>();

        private Point dragStartPoint;
        private bool dragging = false;

        private object dataLock = new object();
        private List<PlayerInfo> scannedPlayers = new List<PlayerInfo>();
        private WoWCharacter safeActiveChar = null;
        private System.Threading.Thread scannerThread;

        private static readonly Font zFont = new Font("Arial", 6f);
        private static readonly Font xFont = new Font("Arial", 16f, FontStyle.Bold);
        private static readonly Font gearFont = new Font("Segoe UI Emoji", 14f);
        private static readonly Font compassFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        private static readonly Font zoomFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        private static readonly Font italicFont = new Font("Segoe UI", 8.5f, FontStyle.Italic);

        private HashSet<ulong> alertedStealthGuids = new HashSet<ulong>();

        private System.Windows.Forms.Timer updateTimer;
        private Dictionary<ulong, PlayerInfo> recentGhosts = new Dictionary<ulong, PlayerInfo>();

        public class DisplayedEnemy
        {
            public PlayerInfo Player { get; set; }
            public DateTime LastSeen { get; set; }
        }
        private Dictionary<ulong, DisplayedEnemy> displayedEnemies = new Dictionary<ulong, DisplayedEnemy>();
        private HashSet<ulong> activeEnemiesOnRadar = new HashSet<ulong>();

        private string statusMessage = "Conectando a WoW...";
        private int targetPid = 0;
        public EnemyDetailsForm detailsForm = null!;
        private float mapScale = 1.1f; // Escala del mapa interactiva (1.1f = ~100 yds)
        private HashSet<ulong> targetingMeHistory = new HashSet<ulong>(); // Historial de alertas de audio

        public class RadarConfig
        {
            public int X { get; set; }
            public int Y { get; set; }
            public float Zoom { get; set; }
            public bool TrackPlayers { get; set; } = true;
            public bool TrackAllies { get; set; } = true;
            public bool TrackEnemies { get; set; } = true;
            public bool TrackGameObjects { get; set; } = true;
            public bool TrackNPCs { get; set; } = false;
            public bool FilterZAxis { get; set; } = false;
            public bool PlayAudioAlerts { get; set; } = true;
            public bool AntiRogueAlerts { get; set; } = true;
            public int InfoDisplayTime { get; set; } = 7;
            public string CustomSoundPath { get; set; } = "";
            public int MaxDetailTargets { get; set; } = 5;
            public bool ColorEnemiesByClass { get; set; } = false;
            public bool ColorAlliesByClass { get; set; } = false;
            public int AudioAlertFaction { get; set; } = 0; // 0 = Solo Enemigos, 1 = Solo Aliados, 2 = Ambos
            public bool MinimizeWithGame { get; set; } = false;
            public int MaxAllies { get; set; } = 30;
            public int MaxEnemies { get; set; } = 30;
        }

        public RadarConfig Config = new RadarConfig();
        private SettingsForm settingsForm = null!;
        private System.Media.SoundPlayer? customPlayer = null;

        private void PlayDetectionSound()
        {
            try
            {
                if (!string.IsNullOrEmpty(Config.CustomSoundPath) && System.IO.File.Exists(Config.CustomSoundPath))
                {
                    if (customPlayer == null || customPlayer.SoundLocation != Config.CustomSoundPath)
                    {
                        customPlayer = new System.Media.SoundPlayer(Config.CustomSoundPath);
                        customPlayer.Load();
                    }
                    customPlayer.Play();
                }
                else
                {
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
                System.Media.SystemSounds.Asterisk.Play();
            }
        }

        public RadarForm()
        {
        }

        public WoWCharacter? GetActiveChar()
        {
            return char1.Connected ? char1 : (char2.Connected ? char2 : null);
        }

        public RadarForm(int targetPid)
        {
            this.targetPid = targetPid;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(260, 260);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.Fuchsia; // Usado para clave de transparencia
            this.TransparencyKey = Color.Fuchsia; // Transparencia total
            this.DoubleBuffered = true;
            this.TopMost = true;

            Icon? customIcon = Program.GetApplicationIcon();
            if (customIcon != null)
            {
                this.Icon = customIcon;
            }

            // Cargar configuración guardada
            try
            {
                if (File.Exists("radar_config.json"))
                {
                    string json = File.ReadAllText("radar_config.json");
                    var cfg = JsonSerializer.Deserialize<RadarConfig>(json);
                    if (cfg != null)
                    {
                        this.Config = cfg;
                        this.StartPosition = FormStartPosition.Manual;
                        this.Location = new Point(cfg.X, cfg.Y);
                        this.mapScale = cfg.Zoom;
                    }
                }
            }
            catch { }

            // Inicializar formulario de detalles del enemigo (segunda ventana)
            detailsForm = new EnemyDetailsForm(this);
            detailsForm.Show(this); // Su dueño es este RadarForm para que minimicen/restauren juntos

            this.Load += (s, e) =>
            {
                // Posicionar a la derecha del radar con 6px de espacio
                detailsForm.Location = new Point(this.Location.X + this.Width + 6, this.Location.Y);
            };

            // Mantener alineadas y escaladas ambas ventanas al redimensionar
            this.Resize += (s, e) =>
            {
                if (this.Width != this.Height)
                {
                    this.Width = this.Height; // Forzar proporción cuadrada para el círculo
                }
                if (detailsForm != null && !detailsForm.IsDisposed)
                {
                    detailsForm.Location = new Point(this.Location.X + this.Width + 6, this.Location.Y);
                    detailsForm.Height = 4 + this.Config.MaxDetailTargets * 56;
                }
            };

            // Temporizador (60 FPS)
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateTick;
            updateTimer.Start();

            scannerThread = new System.Threading.Thread(MemoryScannerLoop);
            scannerThread.IsBackground = true;
            scannerThread.Start();

            this.MouseDown += OnFormMouseDown;
            this.MouseMove += OnFormMouseMove;
            this.MouseUp += OnFormMouseUp;
            this.MouseWheel += OnFormMouseWheel; // Registrar evento de zoom interactivo
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST)
            {
                Point pos = this.PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                if (pos.X >= this.ClientSize.Width - 16 && pos.Y >= this.ClientSize.Height - 16)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                    return;
                }
            }
            base.WndProc(ref m);
        }




        public void SaveConfig()
        {
            try
            {
                Config.X = this.Location.X;
                Config.Y = this.Location.Y;
                Config.Zoom = this.mapScale;
                File.WriteAllText("radar_config.json", JsonSerializer.Serialize(Config));
            }
            catch { }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (Evita que la ventana robe foco o desaparezca al hacer clic en el juego)
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (Oculta del Alt-Tab y barra de tareas)
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST (Forza que se dibuje por encima de todo)
                return cp;
            }
        }

        private void OnFormMouseWheel(object? sender, MouseEventArgs e)
        {
            // Zoom in/out con rueda del mouse
            if (e.Delta > 0)
            {
                mapScale = Math.Min(mapScale + 0.1f, 3.5f); // Límite zoom in
            }
            else
            {
                mapScale = Math.Max(mapScale - 0.1f, 0.4f); // Límite zoom out
            }
            SaveConfig();
        }

        private void OnFormMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragStartPoint = new Point(e.X, e.Y);
            }
        }

        private void OnFormMouseMove(object? sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point diff = new Point(e.X - dragStartPoint.X, e.Y - dragStartPoint.Y);
                this.Location = new Point(this.Location.X + diff.X, this.Location.Y + diff.Y);
                if (detailsForm != null && !detailsForm.IsDisposed)
                {
                    detailsForm.Location = new Point(this.Location.X + this.Width + 6, this.Location.Y);
                }
            }
        }

        private void OnFormMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // Check if click was inside 'X' (top right)
                if (e.X >= this.Width - 30 && e.Y <= 30)
                {
                    Environment.Exit(0);
                }

                // Check if click was inside gear icon bounding box (bottom right)
                if (e.X >= this.Width - 55 && e.X <= this.Width - 15 && e.Y >= this.Height - 55 && e.Y <= this.Height - 15)
                {
                    if (settingsForm == null || settingsForm.IsDisposed)
                    {
                        settingsForm = new SettingsForm(this);
                        settingsForm.Show();
                    }
                    else
                    {
                        settingsForm.BringToFront();
                    }
                }

                dragging = false;
                SaveConfig();
            }
        }


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
                        uint currentMapId = activeChar.Mem.ReadSafe<uint>((IntPtr)STATIC_MAP_ID);
                        if (currentMapId != lastMapId)
                        {
                            lastMapId = currentMapId;
                            playerDetailsCache.Clear();
                            playerNameCache.Clear();
                            guildNameCache.Clear();
                        }

                        var rawPlayers = GetNearbyPlayers(activeChar);



                        lock (dataLock)
                        {
                            scannedPlayers = rawPlayers;
                            safeActiveChar = new WoWCharacter
                            {
                                X = activeChar.X,
                                Y = activeChar.Y,
                                Z = activeChar.Z,
                                Facing = activeChar.Facing,
                                Name = activeChar.Name,
                                Pid = activeChar.Pid,
                                FactionId = activeChar.FactionId,
                                RaceId = activeChar.RaceId,
                                TargetGuid = activeChar.TargetGuid,
                                FocusGuid = activeChar.FocusGuid,
                                Hp = activeChar.Hp,
                                MaxHp = activeChar.MaxHp,
                                ClassName = activeChar.ClassName,
                                RaceName = activeChar.RaceName,
                                CastingSpellId = activeChar.CastingSpellId,
                                ChannelSpellId = activeChar.ChannelSpellId,
                                CastingSpellId2 = activeChar.CastingSpellId2,
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

        private void UpdateTick(object? sender, EventArgs e)
        {
            try
            {
                // 1. Verificar si el juego se ha cerrado
                if (targetPid != 0)
                {
                    try
                    {
                        var proc = System.Diagnostics.Process.GetProcessById(targetPid);
                        if (proc == null || proc.HasExited)
                        {
                            Environment.Exit(0);
                        }
                    }
                    catch
                    {
                        // Proceso no encontrado, el juego se cerró
                        Environment.Exit(0);
                    }

                    // 2. Ocultar o mostrar radar si el juego está minimizado o si se cambia de ventana
                    IntPtr wowHWnd = GetWoWWindow(targetPid);
                    if (wowHWnd != IntPtr.Zero)
                    {
                        bool isWowMinimized = IsIconic(wowHWnd);
                        bool shouldHide = false;

                        if (Config.MinimizeWithGame)
                        {
                            if (isWowMinimized)
                            {
                                shouldHide = true;
                            }
                            else
                            {
                                IntPtr activeWindow = GetForegroundWindow();
                                // Si la ventana activa no es WoW, ni el radar, ni los detalles, ni los ajustes -> ocultar
                                if (activeWindow != wowHWnd &&
                                    activeWindow != this.Handle &&
                                    activeWindow != (detailsForm != null ? detailsForm.Handle : IntPtr.Zero) &&
                                    activeWindow != (settingsForm != null ? settingsForm.Handle : IntPtr.Zero))
                                {
                                    shouldHide = true;
                                }
                            }
                        }

                        if (shouldHide)
                        {
                            if (this.Visible) this.Visible = false;
                            if (detailsForm != null && detailsForm.Visible) detailsForm.Visible = false;
                            if (settingsForm != null && settingsForm.Visible) settingsForm.Visible = false;
                        }
                        else
                        {
                            if (!this.Visible) this.Visible = true;
                            if (detailsForm != null && !detailsForm.Visible && detailsForm.Opacity > 0.35)
                            {
                                detailsForm.Visible = true;
                            }
                        }
                    }
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
                    var smoothed = SmoothPlayerMovement(currentRawPlayers, activeChar);

                    // Filtrar por cantidad máxima de aliados y enemigos
                    var filtered = new List<PlayerInfo>();
                    int alliesCount = 0;
                    int enemiesCount = 0;
                    foreach (var p in smoothed)
                    {
                        if (p.IsGameObject || p.IsNPC)
                        {
                            filtered.Add(p);
                            continue;
                        }

                        bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
                        bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
                        bool isSameFaction = (isCharAlliance == isPlayerAlliance);

                        if (isSameFaction)
                        {
                            if (alliesCount < Config.MaxAllies)
                            {
                                filtered.Add(p);
                                alliesCount++;
                            }
                        }
                        else
                        {
                            if (enemiesCount < Config.MaxEnemies)
                            {
                                filtered.Add(p);
                                enemiesCount++;
                            }
                        }
                    }
                    nearbyPlayers = filtered;
                }
                else
                {
                    nearbyPlayers.Clear();
                }

                // Encontrar los enemigos y procesar alertas de audio y panel de detalles
                var currentTargetingMe = new HashSet<ulong>();
                var nextActiveEnemies = new HashSet<ulong>();

                if (activeChar != null)
                {
                    foreach (var p in nearbyPlayers)
                    {
                        if (p.IsGameObject || p.IsNPC) continue;

                        bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
                        bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
                        bool isSameFaction = (isCharAlliance == isPlayerAlliance);

                        // 1. Alerta de nuevo enemigo real en el mapa y actualización del panel de detalles (sin filtro de santuario)
                        if (!isSameFaction)
                        {
                            nextActiveEnemies.Add(p.Guid);

                            if (Config.PlayAudioAlerts && !activeEnemiesOnRadar.Contains(p.Guid))
                            {
                                PlayDetectionSound();
                            }

                            // Registrar/Actualizar en displayedEnemies (lista de enemigos a la derecha)
                            displayedEnemies[p.Guid] = new DisplayedEnemy { Player = p, LastSeen = DateTime.Now };
                        }

                        // 2. Alerta de jugador (aliado o enemigo) apuntándonos (IsTargetingMe) (sin filtro de santuario)
                        if (p.IsTargetingMe)
                        {
                            currentTargetingMe.Add(p.Guid);

                            bool playAlert = false;

                            if (Config.AudioAlertFaction == 0 && !isSameFaction) // Solo Enemigos
                                playAlert = true;
                            else if (Config.AudioAlertFaction == 1 && isSameFaction) // Solo Aliados
                                playAlert = true;
                            else if (Config.AudioAlertFaction == 2) // Ambos
                                playAlert = true;

                            if (Config.PlayAudioAlerts && playAlert && !targetingMeHistory.Contains(p.Guid))
                            {
                                SystemSounds.Exclamation.Play();
                            }
                        }
                    }
                }
                activeEnemiesOnRadar = nextActiveEnemies;
                targetingMeHistory = currentTargetingMe;

                // Limpiar enemigos mostrados que hayan expirado
                var expiredGuids = new List<ulong>();
                foreach (var kvp in displayedEnemies)
                {
                    if ((DateTime.Now - kvp.Value.LastSeen).TotalSeconds > Config.InfoDisplayTime)
                    {
                        expiredGuids.Add(kvp.Key);
                    }
                }
                foreach (var guid in expiredGuids)
                {
                    displayedEnemies.Remove(guid);
                }

                // Ordenar y seleccionar los principales
                var sortedList = new List<DisplayedEnemy>(displayedEnemies.Values);
                sortedList.Sort((a, b) =>
                {
                    bool aActive = activeEnemiesOnRadar.Contains(a.Player.Guid);
                    bool bActive = activeEnemiesOnRadar.Contains(b.Player.Guid);
                    if (aActive && !bActive) return -1;
                    if (!aActive && bActive) return 1;
                    return a.Player.Distance.CompareTo(b.Player.Distance);
                });

                var targetEnemies = new List<PlayerInfo>();
                for (int i = 0; i < Math.Min(Config.MaxDetailTargets, sortedList.Count); i++)
                {
                    targetEnemies.Add(sortedList[i].Player);
                }

                // Actualizar los datos en la ventana secundaria de detalles con el tiempo de visualización configurable
                if (detailsForm != null && !detailsForm.IsDisposed)
                {
                    detailsForm.UpdateTargets(targetEnemies, Config.InfoDisplayTime);
                }

                // Mensaje de estado dinámico
                bool isConnected = activeChar != null;
                // Mensaje de estado dinámico (Simplificado y limpio sin Tether)
                if (char1.Connected)
                {
                    statusMessage = $"Conectado a {char1.Name} (PID: {char1.Pid}). Cambia de Pj en Ajustes.";
                }
                else
                {
                    statusMessage = "Buscando procesos de WoW activos en segundo plano...";
                }
            }
            catch (Exception ex)
            {
                statusMessage = $"Error: {ex.Message}";
            }

            this.Invalidate();
        }

        private void ReloadDynamicConnections()
        {
            if (targetPid == 0) return;

            ValidateProcess(char1);

            if (!char1.Connected || char1.Pid != targetPid)
            {
                IntPtr handle = Memory.OpenProcess(
                    Memory.ProcessAccessFlags.VirtualMemoryRead |
                    Memory.ProcessAccessFlags.VirtualMemoryWrite |
                    Memory.ProcessAccessFlags.VirtualMemoryOperation |
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
        }

        private void ValidateProcess(WoWCharacter character)
        {
            if (!character.Connected) return;
            try
            {
                // Leer dinámicamente el Object Manager en cada tick para sobrevivir a pantallas de carga
                uint clientConn = character.Mem.ReadSafe<uint>((IntPtr)ADDR_CLIENT_CONNECTION);
                uint objMgr = character.Mem.ReadSafe<uint>((IntPtr)(clientConn + OFFSET_OBJ_MGR_FROM_CC));
                if (objMgr > 0x10000)
                {
                    character.ObjMgr = objMgr;
                }
                else
                {
                    character.Connected = false;
                }
            }
            catch
            {
                character.Connected = false;
            }
        }

        private struct Coords { public float X; public float Y; public float Z; public float Facing; }

        private Coords? GetAbsoluteCoordinates(Memory mem, uint objMgr, ulong targetGuid)
        {
            if (targetGuid == 0) return null;
            uint curObj = mem.ReadSafe<uint>((IntPtr)(objMgr + OFFSET_FIRST_OBJECT));
            HashSet<uint> visited = new HashSet<uint>();

            while (curObj != 0 && curObj > 0x10000 && !visited.Contains(curObj))
            {
                visited.Add(curObj);
                ulong guid = mem.ReadSafe<ulong>((IntPtr)(curObj + OFFSET_OBJ_GUID));

                if (guid == targetGuid)
                {
                    int type = mem.ReadSafe<int>((IntPtr)(curObj + OFFSET_OBJ_TYPE));
                    float x, y, z, facing = 0f;
                    if (type == 5) // GameObject
                    {
                        x = mem.ReadSafe<float>((IntPtr)(curObj + 0xE8));
                        y = mem.ReadSafe<float>((IntPtr)(curObj + 0xEC));
                        z = mem.ReadSafe<float>((IntPtr)(curObj + 0xF0));
                        facing = mem.ReadSafe<float>((IntPtr)(curObj + 0xF4));
                    }
                    else // Unit/Player
                    {
                        x = mem.ReadSafe<float>((IntPtr)(curObj + 0x798));
                        y = mem.ReadSafe<float>((IntPtr)(curObj + 0x79C));
                        z = mem.ReadSafe<float>((IntPtr)(curObj + 0x7A0));
                        facing = mem.ReadSafe<float>((IntPtr)(curObj + 0x7A8));
                    }
                    return new Coords { X = x, Y = y, Z = z, Facing = facing };
                }

                curObj = mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
            }
            return null;
        }

        private void UpdateCharData(WoWCharacter character)
        {
            character.FocusGuid = character.Mem.ReadSafe<ulong>((IntPtr)STATIC_FOCUS_GUID);
            uint curObj = character.Mem.ReadSafe<uint>((IntPtr)(character.ObjMgr + OFFSET_FIRST_OBJECT));
            HashSet<uint> visited = new HashSet<uint>();

            while (curObj != 0 && curObj > 0x10000 && !visited.Contains(curObj))
            {
                visited.Add(curObj);
                ulong guid = character.Mem.ReadSafe<ulong>((IntPtr)(curObj + OFFSET_OBJ_GUID));

                if (guid == character.Guid)
                {
                    character.Name = ReadString(character.Mem, 0x00C79D18, 30);

                    uint descPtr = character.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_OBJ_DESCRIPTOR));
                    if (descPtr > 0x10000)
                    {
                        character.Level = character.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));
                        character.Hp = character.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));
                        character.MaxHp = character.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_MAX_HEALTH));

                        character.TargetGuid = character.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x48));

                        uint bytes0 = character.Mem.ReadSafe<uint>((IntPtr)(descPtr + DESC_BYTES_0));
                        character.RaceId = (byte)(bytes0 & 0xFF);
                        character.RaceName = GetRaceName(character.RaceId);
                        character.ClassName = GetClassName((byte)((bytes0 >> 8) & 0xFF));

                        uint bytes2 = character.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0x78));
                        character.IsPvPActive = ((bytes2 & 0xFF) & 0x01) != 0;

                        character.FactionId = character.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xD4));
                    }

                    character.X = character.Mem.ReadSafe<float>((IntPtr)(curObj + 0x798));
                    character.Y = character.Mem.ReadSafe<float>((IntPtr)(curObj + 0x79C));
                    character.Z = character.Mem.ReadSafe<float>((IntPtr)(curObj + 0x7A0));
                    character.Facing = character.Mem.ReadSafe<float>((IntPtr)(curObj + 0x7A8));

                    ulong transportGuid = character.Mem.ReadSafe<ulong>((IntPtr)(curObj + 0x830));
                    if (transportGuid == 0 && descPtr > 0x10000)
                    {
                        ulong charmedBy = character.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x10)); // UNIT_FIELD_CHARMEDBY
                        if (charmedBy != 0)
                        {
                            transportGuid = charmedBy;
                        }
                        else
                        {
                            ulong charm = character.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x00)); // UNIT_FIELD_CHARM
                            if (charm != 0)
                            {
                                transportGuid = charm;
                            }
                        }
                    }

                    if (transportGuid != 0)
                    {
                        var transportCoords = GetAbsoluteCoordinates(character.Mem, character.ObjMgr, transportGuid);
                        if (transportCoords != null)
                        {
                            character.X = transportCoords.Value.X;
                            character.Y = transportCoords.Value.Y;
                            character.Z = transportCoords.Value.Z;
                            character.Facing = transportCoords.Value.Facing;
                        }
                    }

                    character.CastingSpellId = character.Mem.ReadSafe<int>((IntPtr)(curObj + 0xA60));
                    character.ChannelSpellId = character.Mem.ReadSafe<int>((IntPtr)(curObj + 0xA6C));
                    character.CastingSpellId2 = character.Mem.ReadSafe<int>((IntPtr)(curObj + 0xCB0));



                    break;
                }

                curObj = character.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
            }
        }

        private List<PlayerInfo> GetNearbyPlayers(WoWCharacter inst)
        {
            var players = new List<PlayerInfo>();
            uint curObj = inst.Mem.ReadSafe<uint>((IntPtr)(inst.ObjMgr + OFFSET_FIRST_OBJECT));
            HashSet<uint> visited = new HashSet<uint>();

            while (curObj != 0 && curObj > 0x10000 && !visited.Contains(curObj))
            {
                visited.Add(curObj);
                int type = inst.Mem.ReadSafe<int>((IntPtr)(curObj + OFFSET_OBJ_TYPE));

                // 1. Filtrado rápido por tipo según configuración antes de lecturas pesadas
                if (type == 3 && !Config.TrackNPCs)
                {
                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                    continue;
                }
                if (type == 5 && !Config.TrackGameObjects)
                {
                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                    continue;
                }
                if (type == 4 && !Config.TrackPlayers)
                {
                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                    continue;
                }

                if (type == 3 || type == 4 || type == 5) // Unit, Player o GameObject
                {
                    ulong guid = inst.Mem.ReadSafe<ulong>((IntPtr)(curObj + OFFSET_OBJ_GUID));

                    if (guid != inst.Guid)
                    {
                        var player = new PlayerInfo { Guid = guid, IsGameObject = (type == 5), IsNPC = (type == 3) };
                        uint descPtr = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_OBJ_DESCRIPTOR));
                        byte classId = 0;

                        if (descPtr > 0x10000 && !player.IsGameObject)
                        {
                            // Leer salud dinámica primero; si está muerto, lo ignoramos de inmediato
                            player.Hp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_HEALTH));
                            if (player.Hp <= 0)
                            {
                                curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                                continue;
                            }
                            player.MaxHp = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_MAX_HEALTH));

                            // Recuperar o leer detalles estáticos desde caché
                            if (playerDetailsCache.TryGetValue(guid, out var cached) && !string.IsNullOrEmpty(cached.Name) && !cached.Name.StartsWith("Jugador_"))
                            {
                                player.Name = cached.Name;
                                player.Level = cached.Level;
                                player.RaceId = cached.RaceId;
                                player.RaceName = cached.RaceName;
                                player.ClassName = cached.ClassName;
                                player.FactionId = cached.FactionId;
                                player.GuildName = cached.GuildName;
                                classId = GetClassIdByName(player.ClassName);
                            }
                            else
                            {
                                player.Name = player.IsNPC ? "NPC/Mob" : ResolvePlayerName(inst, guid);
                                player.Level = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + DESC_LEVEL));
                                uint bytes0 = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + DESC_BYTES_0));
                                player.RaceId = (byte)(bytes0 & 0xFF);
                                player.RaceName = GetRaceName(player.RaceId);
                                classId = (byte)((bytes0 >> 8) & 0xFF);
                                player.ClassName = GetClassName(classId);
                                player.FactionId = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xD4));

                                if (type == 4) // Player
                                {
                                    uint guildId = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0x25C));
                                    player.GuildName = ResolveGuildName(inst, guildId);
                                }
                                else
                                {
                                    player.GuildName = "";
                                }

                                // Guardar en caché
                                playerDetailsCache[guid] = new CachedPlayerDetails
                                {
                                    Name = player.Name,
                                    Level = player.Level,
                                    RaceId = player.RaceId,
                                    RaceName = player.RaceName,
                                    ClassName = player.ClassName,
                                    FactionId = player.FactionId,
                                    GuildName = player.GuildName
                                };
                            }

                            // 2. Filtrado rápido por facción (Aliado vs Enemigo) antes de lecturas dinámicas pesadas
                            if (type == 4)
                            {
                                bool isCharAlliance = IsAllianceRaceId(inst.RaceId);
                                bool isPlayerAlliance = IsAllianceRaceId(player.RaceId);
                                bool isSameFaction = (isCharAlliance == isPlayerAlliance);

                                if (isSameFaction && !Config.TrackAllies)
                                {
                                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                                    continue;
                                }
                                if (!isSameFaction && !Config.TrackEnemies)
                                {
                                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                                    continue;
                                }
                            }

                            uint bytes2 = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0x78));
                            player.IsPvPActive = ((bytes2 & 0xFF) & 0x01) != 0;

                            player.Flags = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xE8));
                            player.AuraState = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0xF0));
                            player.Bytes1 = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0x110));

                            bool hasStealthAura = (player.AuraState & 0x800) != 0;
                            player.IsStealthed = hasStealthAura && (classId == 4 || classId == 11 || player.RaceId == 4);

                            // Cargar valor de recurso basado en la clase
                            if (classId == 1) // Guerrero (Warrior)
                            {
                                player.PowerType = "Ira";
                                player.Power = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x68)) / 10;
                                player.MaxPower = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x88)) / 10;
                            }
                            else if (classId == 4) // Pícaro (Rogue)
                            {
                                player.PowerType = "Energía";
                                player.Power = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x70));
                                player.MaxPower = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x90));
                            }
                            else if (classId == 6) // Caballero de la Muerte (Death Knight)
                            {
                                player.PowerType = "Poder Rúnico";
                                player.Power = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x7C)) / 10;
                                player.MaxPower = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x9C)) / 10;
                            }
                            else // Cazadores, Magos, Sacerdotes, Paladines, etc.
                            {
                                player.PowerType = "Maná";
                                player.Power = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x64));
                                player.MaxPower = inst.Mem.ReadSafe<int>((IntPtr)(descPtr + 0x84));
                            }

                            ulong targetGuid = inst.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x48));
                            player.IsTargetingMe = (targetGuid == inst.Guid);
                        }

                        if (!player.IsGameObject)
                        {
                            player.X = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0x798));
                            player.Y = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0x79C));
                            player.Z = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0x7A0));
                            player.Facing = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0x7A8));

                            ulong transportGuid = inst.Mem.ReadSafe<ulong>((IntPtr)(curObj + 0x830));
                            if (transportGuid == 0 && descPtr > 0x10000)
                            {
                                ulong charmedBy = inst.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x10));
                                if (charmedBy != 0)
                                {
                                    transportGuid = charmedBy;
                                }
                                else
                                {
                                    ulong charm = inst.Mem.ReadSafe<ulong>((IntPtr)(descPtr + 0x00));
                                    if (charm != 0)
                                    {
                                        transportGuid = charm;
                                    }
                                }
                            }

                            if (transportGuid != 0)
                            {
                                var transportCoords = GetAbsoluteCoordinates(inst.Mem, inst.ObjMgr, transportGuid);
                                if (transportCoords != null)
                                {
                                    player.X = transportCoords.Value.X;
                                    player.Y = transportCoords.Value.Y;
                                    player.Z = transportCoords.Value.Z;
                                    player.Facing = transportCoords.Value.Facing;
                                }
                            }

                            player.CastingSpellId = inst.Mem.ReadSafe<int>((IntPtr)(curObj + 0xA60));
                            player.ChannelSpellId = inst.Mem.ReadSafe<int>((IntPtr)(curObj + 0xA6C));
                            player.CastingSpellId2 = inst.Mem.ReadSafe<int>((IntPtr)(curObj + 0xCB0));
                        }
                        else
                        {
                            if (descPtr > 0x10000)
                            {
                                uint bytes1 = inst.Mem.ReadSafe<uint>((IntPtr)(descPtr + 0x44));
                                byte goType = (byte)((bytes1 >> 8) & 0xFF);
                                if (goType != 3) // Omitir si no es un cofre/mina/hierba
                                {
                                    curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
                                    continue;
                                }
                            }

                            player.X = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0xE8));
                            player.Y = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0xEC));
                            player.Z = inst.Mem.ReadSafe<float>((IntPtr)(curObj + 0xF0));
                            player.Facing = 0f;
                            player.Name = "Objeto";
                        }

                        float dx = inst.X - player.X;
                        float dy = inst.Y - player.Y;
                        float dz = inst.Z - player.Z;
                        player.Distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                        if (player.Distance < 250f)
                        {
                            players.Add(player);
                        }
                    }
                }

                curObj = inst.Mem.ReadSafe<uint>((IntPtr)(curObj + OFFSET_NEXT_OBJECT));
            }

            players.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return players;
        }

        private struct CachedPlayerDetails
        {
            public string Name;
            public int Level;
            public byte RaceId;
            public string RaceName;
            public string ClassName;
            public uint FactionId;
            public string GuildName;
        }

        private Dictionary<ulong, CachedPlayerDetails> playerDetailsCache = new Dictionary<ulong, CachedPlayerDetails>();
        private Dictionary<ulong, string> playerNameCache = new Dictionary<ulong, string>();

        private string ResolvePlayerName(WoWCharacter inst, ulong guid)
        {
            if (char1.Connected && char1.Guid == guid) return char1.Name;
            if (char2.Connected && char2.Guid == guid) return char2.Name;

            if (playerNameCache.TryGetValue(guid, out string cachedName) && !cachedName.StartsWith("Jugador_"))
                return cachedName;

            try
            {
                uint nameStoreBase = ADDR_PLAYER_NAME_CACHE + 0x8;
                if (nameStoreBase > 0x10000)
                {
                    uint mask = inst.Mem.ReadSafe<uint>((IntPtr)(nameStoreBase + 0x24));
                    uint baseArray = inst.Mem.ReadSafe<uint>((IntPtr)(nameStoreBase + 0x1C));
                    uint shortGUID = (uint)(guid & 0xffffffff);
                    uint bucketIndex = mask & shortGUID;
                    uint bucketSlotAddress = baseArray + 12 * bucketIndex + 4;
                    uint currentEntry = inst.Mem.ReadSafe<uint>((IntPtr)bucketSlotAddress);
                    int maxIterations = 50;
                    while (currentEntry != 0 && currentEntry > 0x10000 && maxIterations > 0)
                    {
                        currentEntry = currentEntry & ~1u;
                        if (currentEntry == bucketSlotAddress)
                            break;

                        if (currentEntry >= baseArray && currentEntry < baseArray + 12 * (mask + 1))
                            break;

                        uint entryShortGUID = inst.Mem.ReadSafe<uint>((IntPtr)(currentEntry + 0x14));
                        if (entryShortGUID == shortGUID)
                        {
                            string name = ReadString(inst.Mem, currentEntry + 0x1C, 40);
                            if (!string.IsNullOrEmpty(name))
                            {
                                playerNameCache[guid] = name;
                                return name;
                            }
                        }
                        uint nextEntry = inst.Mem.ReadSafe<uint>((IntPtr)(currentEntry + 0x00));
                        if (nextEntry == 0 || (nextEntry & ~1u) == bucketSlotAddress || nextEntry == currentEntry)
                            break;
                        currentEntry = nextEntry;
                        maxIterations--;
                    }
                }
            }
            catch { }

            return $"Jugador_{guid & 0xFFFFFFFF:X}";
        }

        private Dictionary<uint, string> guildNameCache = new Dictionary<uint, string>();

        private string ResolveGuildName(WoWCharacter inst, uint guildId)
        {
            if (guildId == 0) return "";
            if (guildNameCache.TryGetValue(guildId, out string cachedName))
                return cachedName;

            try
            {
                // s_guildCache está en 0x00C60C88
                uint guildCachePtr = inst.Mem.ReadSafe<uint>((IntPtr)0x00C60C88);
                if (guildCachePtr > 0x10000)
                {
                    uint storeBase = guildCachePtr + 0x8;
                    uint mask = inst.Mem.ReadSafe<uint>((IntPtr)(storeBase + 0x24));
                    uint baseArray = inst.Mem.ReadSafe<uint>((IntPtr)(storeBase + 0x1C));

                    uint bucketIndex = mask & guildId;
                    uint currentEntry = inst.Mem.ReadSafe<uint>((IntPtr)(baseArray + 4 * bucketIndex));
                    int maxIterations = 50;

                    while (currentEntry != 0 && currentEntry > 0x10000 && maxIterations > 0)
                    {
                        // Guild cache entry key (Guild ID) está en +0x10 o +0x14
                        uint entryGuildId = inst.Mem.ReadSafe<uint>((IntPtr)(currentEntry + 0x10));
                        if (entryGuildId == guildId)
                        {
                            string name = ReadString(inst.Mem, currentEntry + 0x14, 40);
                            if (string.IsNullOrEmpty(name) || name.Length < 2)
                            {
                                // Intentar offset alternativo +0x1C
                                name = ReadString(inst.Mem, currentEntry + 0x1C, 40);
                            }

                            if (!string.IsNullOrEmpty(name))
                            {
                                guildNameCache[guildId] = name;
                                return name;
                            }
                        }
                        else
                        {
                            uint entryGuildIdAlt = inst.Mem.ReadSafe<uint>((IntPtr)(currentEntry + 0x14));
                            if (entryGuildIdAlt == guildId)
                            {
                                string name = ReadString(inst.Mem, currentEntry + 0x1C, 40);
                                if (!string.IsNullOrEmpty(name))
                                {
                                    guildNameCache[guildId] = name;
                                    return name;
                                }
                            }
                        }

                        uint nextEntry = inst.Mem.ReadSafe<uint>((IntPtr)(currentEntry + 0x0C));
                        if (nextEntry == currentEntry || nextEntry == 0) break;
                        currentEntry = nextEntry;
                        maxIterations--;
                    }
                }
            }
            catch { }

            return "";
        }



        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            int centerX = w / 2;
            int centerY = h / 2;
            int radarRadius = Math.Min(w, h) / 2 - 15; // Dinámico para soportar cambio de tamaño
            int radarSize = radarRadius * 2;

            // 1. Limpiar el fondo con la clave de transparencia Fuchsia (se volverá invisible sobre el juego)
            g.Clear(Color.Fuchsia);

            // 2. Dibujar el último anillo exterior (delimitación circular del radar) en verde neón brillante
            using (Pen outerNeonPen = new Pen(Color.FromArgb(255, 0, 255, 0), 2.5f)) // Verde neón sólido de alta intensidad
            {
                g.DrawEllipse(outerNeonPen, centerX - radarRadius, centerY - radarRadius, radarSize, radarSize);
            }

            WoWCharacter? activeChar = char1.Connected ? char1 : (char2.Connected ? char2 : null);

            if (activeChar != null)
            {
                uint mapId = activeChar.Mem.ReadSafe<uint>((IntPtr)STATIC_MAP_ID);

                // =========================================================================
                // MAPA DE NORTE ESTÁTICO (NORTH-UP) CON PUNTERO ROTATIVO
                // =========================================================================
                // Dibujar puntos cardinales estáticos en el borde (N, S, E, O)
                g.DrawString("N", compassFont, Brushes.Crimson, centerX - 6, centerY - radarRadius + 5);
                g.DrawString("S", compassFont, Brushes.Gray, centerX - 5, centerY + radarRadius - 18);
                g.DrawString("E", compassFont, Brushes.Gray, centerX + radarRadius - 16, centerY - 7);
                g.DrawString("O", compassFont, Brushes.Gray, centerX - radarRadius + 6, centerY - 7);

                // Escala para minimapa interactiva zoom/in-out
                float scale = mapScale;

                // Graficar jugadores de los alrededores
                foreach (var p in nearbyPlayers)
                {
                    // WoW Y aumenta al Oeste (-Y es Este). WoW X aumenta al Norte.
                    float screenDX = -(p.Y - activeChar.Y);
                    float screenDY = -(p.X - activeChar.X);

                    // Proyección estática sobre el mapa (Norte es Arriba, Este es Derecha)
                    float rotX = screenDX;
                    float rotY = screenDY;

                    int otherScreenX = (int)(centerX + rotX * scale);
                    int otherScreenY = (int)(centerY + rotY * scale);

                    // Validar límites del círculo
                    float distCenter = (float)Math.Sqrt(Math.Pow(otherScreenX - centerX, 2) + Math.Pow(otherScreenY - centerY, 2));
                    if (distCenter < radarRadius - 8)
                    {
                        if (p.IsGameObject && !Config.TrackGameObjects) continue;
                        if (p.IsNPC && !Config.TrackNPCs) continue;
                        if (!p.IsGameObject && !p.IsNPC && !Config.TrackPlayers) continue;

                        if (Config.FilterZAxis && Math.Abs(p.Z - activeChar.Z) > 30f) continue; // Filter by Z-Axis

                        bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
                        bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
                        bool isSameFaction = (isCharAlliance == isPlayerAlliance);
                        bool isEnemy = !p.IsGameObject && !p.IsNPC && !isSameFaction;

                        if (!p.IsGameObject && !p.IsNPC)
                        {
                            if (isSameFaction && !Config.TrackAllies) continue;
                            if (!isSameFaction && !Config.TrackEnemies) continue;
                        }

                        Color pColor = GetPlayerColor(p, activeChar, mapId);
                        int dotSize = isEnemy ? 10 : 8;

                        if (p.IsGameObject)
                        {
                            using (Brush objBrush = new SolidBrush(Color.LimeGreen))
                            {
                                g.FillEllipse(objBrush, otherScreenX - 3, otherScreenY - 3, 6, 6);
                            }
                        }
                        else if (p.IsNPC)
                        {
                            using (Brush npcBrush = new SolidBrush(Color.Yellow))
                            {
                                g.FillEllipse(npcBrush, otherScreenX - 4, otherScreenY - 4, 8, 8);
                            }
                        }
                        else if (p.IsGhost)
                        {
                            using (Pen ghostPen = new Pen(pColor, 2f))
                            {
                                ghostPen.DashStyle = DashStyle.Dot;
                                g.DrawEllipse(ghostPen, otherScreenX - 6, otherScreenY - 6, 12, 12);
                                g.DrawString("?", zFont, Brushes.White, otherScreenX + 5, otherScreenY - 10);
                            }
                        }
                        else
                        {
                            // 2. Dibujar flecha central rotativa para el jugador de los alrededores (sin bordes)
                            DrawPlayerArrow(g, otherScreenX, otherScreenY, pColor, p.Facing, dotSize);

                            // Indicador Z-Altitude (Arriba o Abajo)
                            float dz = p.Z - activeChar.Z;
                            if (dz > 5f)
                            {
                                g.DrawString("▴", zFont, Brushes.White, otherScreenX + 5, otherScreenY - 10);
                            }
                            else if (dz < -5f)
                            {
                                g.DrawString("▾", zFont, Brushes.Gray, otherScreenX + 5, otherScreenY + 2);
                            }
                        }

                        // Si es mi Target, resaltarlo con un círculo blanco
                        if (activeChar.TargetGuid == p.Guid)
                        {
                            using (Pen targetPen = new Pen(Color.White, 2f))
                            {
                                g.DrawEllipse(targetPen, otherScreenX - 12, otherScreenY - 12, 24, 24);
                            }
                        }

                        // Si el jugador me está haciendo target -> Dibujar un círculo pulsante/aviso de peligro a su alrededor
                        if (p.IsTargetingMe)
                        {
                            // Dibujar círculo de alerta rojo brillante sólido (sin transparencias para evitar halos violetas)
                            using (Pen warningPen = new Pen(Color.FromArgb(255, 255, 0, 0), 1.8f))
                            {
                                g.DrawEllipse(warningPen, otherScreenX - 9, otherScreenY - 9, 18, 18);
                            }
                            // Dibujar sutil línea snapline de amenaza sólida que conecta al enemigo con nuestro centro
                            using (Pen threatLine = new Pen(Color.FromArgb(255, 255, 0, 0), 1.2f))
                            {
                                threatLine.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                                g.DrawLine(threatLine, otherScreenX, otherScreenY, centerX, centerY);
                            }
                        }
                    }
                }

                // Dibujar jugador central (Active Player) apuntando a su rumbo real (Facing)
                DrawLocalPlayerWithFacing(g, centerX, centerY, Color.Cyan, activeChar.Name, activeChar.Facing);

                // Dibujar ícono de ajustes (Engranaje) abajo
                g.DrawString("⚙", gearFont, Brushes.Silver, w - 45, h - 45);

                // Dibujar 'X' de cerrar en la esquina superior derecha
                g.DrawString("X", xFont, Brushes.Red, w - 25, 10);

                // Línea de rumbo discontinua de color cian rotando con tu mirada hacia el borde del radar (Norte Fijo, rumbo real)
                float headingLen = radarRadius - 15;
                float headingX = centerX - (float)Math.Sin(activeChar.Facing) * headingLen;
                float headingY = centerY - (float)Math.Cos(activeChar.Facing) * headingLen;

                using (Pen headingPen = new Pen(Color.FromArgb(255, 0, 255, 255), 1.5f))
                {
                    headingPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawLine(headingPen, centerX, centerY, headingX, headingY);
                }

                // Dibujar panel de escala premium simplificado en la esquina inferior izquierda (sólo "Zoom: X%")
                int zoomPanelWidth = 72;
                int zoomPanelHeight = 20;
                int zoomX = 10;
                int zoomY = h - zoomPanelHeight - 10;

                // Fondo sólido del panel de zoom (para evitar halos violetas)
                using (SolidBrush zoomBg = new SolidBrush(Color.FromArgb(255, 15, 15, 20)))
                using (Pen zoomBorder = new Pen(Color.FromArgb(255, 0, 255, 0), 1f)) // Verde neón fino sólido
                {
                    g.FillRectangle(zoomBg, zoomX, zoomY, zoomPanelWidth, zoomPanelHeight);
                    g.DrawRectangle(zoomBorder, zoomX, zoomY, zoomPanelWidth, zoomPanelHeight);
                }

                // Dibujar texto del zoom
                float zoomPercent = (mapScale / 1.1f) * 100f;
                string zoomText = $"Zoom: {zoomPercent:F0}%";
                g.DrawString(zoomText, zoomFont, Brushes.Cyan, zoomX + 6, zoomY + 3);
            }
            else
            {
                // Esperando WoW
                string waitMsg = "Esperando proceso de WoW...";
                SizeF size = g.MeasureString(waitMsg, italicFont);
                g.DrawString(waitMsg, italicFont, Brushes.Yellow, centerX - size.Width / 2, centerY - size.Height / 2);
            }
        }

        private List<PlayerInfo> SmoothPlayerMovement(List<PlayerInfo> rawPlayers, WoWCharacter activeChar)
        {
            var smoothedList = new List<PlayerInfo>();

            // Factor de suavizado (0.0f a 1.0f). Menor valor = más suave pero con un toque de retraso/inercia.
            // 0.15f para posición y 0.12f para rotación brindan una fluidez exquisita libre de saltos.
            float posAlpha = 0.15f;
            float rotAlpha = 0.12f;

            var activeGuids = new HashSet<ulong>();

            foreach (var rp in rawPlayers)
            {
                activeGuids.Add(rp.Guid);

                // Solo agregar o actualizar en recientes fantasmas si es enemigo (o aliado en duelo)
                // y no es un objeto o NPC inofensivo.
                bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
                bool isPlayerAlliance = IsAllianceRaceId(rp.RaceId);
                bool isEnemy = (isCharAlliance != isPlayerAlliance);
                bool isDuelingAlly = !isEnemy && rp.IsTargetingMe && rp.Distance < 60f;
                bool isHostile = (isEnemy || isDuelingAlly);

                if (!rp.IsGameObject && !rp.IsNPC && isHostile)
                {
                    rp.LastSeenTime = DateTime.Now;
                    recentGhosts[rp.Guid] = rp;
                }

                if (!playerStates.TryGetValue(rp.Guid, out var state))
                {
                    state = new SmoothedPlayerState
                    {
                        X = rp.X,
                        Y = rp.Y,
                        Vx = (float)Math.Cos(rp.Facing),
                        Vy = (float)Math.Sin(rp.Facing),
                        IsInitialized = true
                    };
                    playerStates[rp.Guid] = state;
                }
                else
                {
                    // Interpolación lineal de la posición
                    state.X = state.X + (rp.X - state.X) * posAlpha;
                    state.Y = state.Y + (rp.Y - state.Y) * posAlpha;

                    // Interpolación matemática usando vectores unitarios para evitar problemas de wrap-around (salto de PI a -PI)
                    float targetVx = (float)Math.Cos(rp.Facing);
                    float targetVy = (float)Math.Sin(rp.Facing);

                    state.Vx = state.Vx + (targetVx - state.Vx) * rotAlpha;
                    state.Vy = state.Vy + (targetVy - state.Vy) * rotAlpha;
                }

                var sp = new PlayerInfo
                {
                    Guid = rp.Guid,
                    Name = rp.Name,
                    Level = rp.Level,
                    Hp = rp.Hp,
                    MaxHp = rp.MaxHp,
                    ClassName = rp.ClassName,
                    RaceName = rp.RaceName,
                    IsPvPActive = rp.IsPvPActive,
                    FactionColor = rp.FactionColor,
                    Power = rp.Power,
                    MaxPower = rp.MaxPower,
                    PowerType = rp.PowerType,
                    IsTargetingMe = rp.IsTargetingMe,
                    FactionId = rp.FactionId,
                    RaceId = rp.RaceId,
                    IsGameObject = rp.IsGameObject,
                    IsNPC = rp.IsNPC,
                    IsStealthed = rp.IsStealthed,
                    AuraState = rp.AuraState,
                    Flags = rp.Flags,
                    Bytes1 = rp.Bytes1,
                    CastingSpellId = rp.CastingSpellId,
                    ChannelSpellId = rp.ChannelSpellId,

                    // Asignar los valores suavizados
                    X = state.X,
                    Y = state.Y,
                    Z = rp.Z,
                    Facing = (float)Math.Atan2(state.Vy, state.Vx),
                };

                // Recalcular distancia con base en la posición suavizada
                float dx = activeChar.X - sp.X;
                float dy = activeChar.Y - sp.Y;
                float dz = activeChar.Z - sp.Z;
                sp.Distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                smoothedList.Add(sp);
            }

            // Eliminar entradas obsoletas de jugadores que se alejaron
            var toRemove = new List<ulong>();
            foreach (var guid in playerStates.Keys)
            {
                if (!activeGuids.Contains(guid))
                {
                    toRemove.Add(guid);
                }
            }
            foreach (var guid in toRemove)
            {
                playerStates.Remove(guid);
            }

            var ghostsToRemove = new List<ulong>();
            foreach (var kvp in recentGhosts)
            {
                if (!activeGuids.Contains(kvp.Key))
                {
                    if ((DateTime.Now - kvp.Value.LastSeenTime).TotalSeconds > 5.0)
                    {
                        ghostsToRemove.Add(kvp.Key);
                    }
                    else
                    {
                        var ghost = kvp.Value;

                        // Solo activar el fantasma si realmente se hizo invisible/sigilo,
                        // no si simplemente corrió fuera de rango (>75 yardas) o desapareció sin ser clase de sigilo.
                        bool isStealthClassOrRace = (ghost.ClassName == "Pícaro" || ghost.ClassName == "Druida" || ghost.ClassName == "Mago" || ghost.RaceId == 4 || ghost.RaceName == "Elfo de la noche");
                        bool wentInvisible = ghost.IsStealthed || (isStealthClassOrRace && ghost.Distance < 75f);

                        if (!wentInvisible)
                        {
                            ghostsToRemove.Add(kvp.Key);
                            continue;
                        }

                        ghost.IsGhost = true;

                        float dx = activeChar.X - ghost.X;
                        float dy = activeChar.Y - ghost.Y;
                        float dz = activeChar.Z - ghost.Z;
                        ghost.Distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

                        smoothedList.Add(ghost);
                    }
                }
            }
            foreach (var guid in ghostsToRemove)
            {
                recentGhosts.Remove(guid);
            }

            return smoothedList;
        }

        private void DrawLocalPlayerWithFacing(Graphics g, int x, int y, Color color, string name, float facing)
        {

            // Punta de flecha central de neón (Norte Fijo, rumbo real de WoW en radianes)
            PointF[] arrowPoints = new PointF[3];
            float arrowLen = 13f;
            float halfWidth = 6.5f;

            float tipX = x - (float)Math.Sin(facing) * arrowLen;
            float tipY = y - (float)Math.Cos(facing) * arrowLen;

            float baseLeftX = x - (float)Math.Sin(facing + 2.5f) * halfWidth;
            float baseLeftY = y - (float)Math.Cos(facing + 2.5f) * halfWidth;

            float baseRightX = x - (float)Math.Sin(facing - 2.5f) * halfWidth;
            float baseRightY = y - (float)Math.Cos(facing - 2.5f) * halfWidth;

            arrowPoints[0] = new PointF(tipX, tipY);
            arrowPoints[1] = new PointF(baseLeftX, baseLeftY);
            arrowPoints[2] = new PointF(baseRightX, baseRightY);

            using (Brush pBrush = new SolidBrush(color))
            {
                g.FillPolygon(pBrush, arrowPoints);
            }
        }

        private void DrawPlayerArrow(Graphics g, int x, int y, Color color, float facing, int size)
        {
            PointF[] arrowPoints = new PointF[3];
            float arrowLen = size * 1.1f;
            float halfWidth = size * 0.55f;

            // Norte Fijo, rumbo real de WoW en radianes (el mismo que el del local player!)
            float tipX = x - (float)Math.Sin(facing) * arrowLen;
            float tipY = y - (float)Math.Cos(facing) * arrowLen;

            float baseLeftX = x - (float)Math.Sin(facing + 2.5f) * halfWidth;
            float baseLeftY = y - (float)Math.Cos(facing + 2.5f) * halfWidth;

            float baseRightX = x - (float)Math.Sin(facing - 2.5f) * halfWidth;
            float baseRightY = y - (float)Math.Cos(facing - 2.5f) * halfWidth;

            arrowPoints[0] = new PointF(tipX, tipY);
            arrowPoints[1] = new PointF(baseLeftX, baseLeftY);
            arrowPoints[2] = new PointF(baseRightX, baseRightY);

            using (Brush pBrush = new SolidBrush(color))
            {
                g.FillPolygon(pBrush, arrowPoints);
            }
        }

        static bool IsAllianceRace(string race)
        {
            return race == "Humano" || race == "Enano" || race == "Elfo de la noche" || race == "Gnomo" || race == "Draenei";
        }

        static bool IsInSanctuaryZone(uint mapId, float x, float y, float z)
        {
            if (mapId == 571) // Northrend
            {
                if (x >= 5500f && x <= 6100f && y >= 300f && y <= 1000f && z > 550f)
                    return true;
            }
            if (mapId == 530) // Outland
            {
                if (x >= -2200f && x <= -1600f && y >= 5000f && y <= 5800f)
                    return true;
            }
            return false;
        }

        static bool IsAllianceFaction(uint factionId)
        {
            // Facciones de jugadores Alianza en WoW 3.3.5a: 1 (Humano), 3 (Enano), 4 (Elfo Nocturno), 115 (Gnomo), 1629 (Draenei)
            return factionId == 1 || factionId == 3 || factionId == 4 || factionId == 115 || factionId == 1629;
        }

        static bool IsAllianceRaceId(byte raceId)
        {
            // IDs de razas Alianza en WoW 3.3.5a: 1 (Humano), 3 (Enano), 4 (Elfo de la noche), 7 (Gnomo), 11 (Draenei)
            return raceId == 1 || raceId == 3 || raceId == 4 || raceId == 7 || raceId == 11;
        }

        private bool IsHostilePlayer(PlayerInfo p, WoWCharacter activeChar)
        {
            if (activeChar == null || p.IsGameObject || p.IsNPC) return false;
            bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
            bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
            return (isCharAlliance != isPlayerAlliance);
        }

        public static Color GetClassColor(string className)
        {
            switch (className)
            {
                case "Guerrero": return Color.FromArgb(199, 156, 110);
                case "Paladín": return Color.FromArgb(245, 140, 186);
                case "Cazador": return Color.FromArgb(171, 212, 115);
                case "Pícaro": return Color.FromArgb(255, 245, 105);
                case "Sacerdote": return Color.FromArgb(255, 255, 255);
                case "Caballero de la Muerte": return Color.FromArgb(196, 31, 59);
                case "Chamán": return Color.FromArgb(0, 112, 222);
                case "Mago": return Color.FromArgb(104, 205, 239);
                case "Brujo": return Color.FromArgb(148, 130, 201);
                case "Druida": return Color.FromArgb(255, 125, 10);
                default: return Color.DarkGray;
            }
        }

        private Color GetPlayerColor(PlayerInfo p, WoWCharacter activeChar, uint mapId)
        {
            if (p.IsGameObject) return Color.LimeGreen;

            // Identificar facción robusta mediante RaceId
            bool isCharAlliance = IsAllianceRaceId(activeChar.RaceId);
            bool isPlayerAlliance = IsAllianceRaceId(p.RaceId);
            bool isSameFaction = (isCharAlliance == isPlayerAlliance);

            // Colorear por clase según configuración
            if (!p.IsGameObject && !p.IsNPC)
            {
                if (isSameFaction && Config.ColorAlliesByClass)
                {
                    return GetClassColor(p.ClassName);
                }
                if (!isSameFaction && Config.ColorEnemiesByClass)
                {
                    // Priorizar el aviso de sigilo (Violeta oscuro) si es pícaro enemigo en sigilo
                    if (p.IsStealthed && p.ClassName == "Pícaro")
                    {
                        return Color.DarkViolet;
                    }
                    return GetClassColor(p.ClassName);
                }
            }

            if (p.IsStealthed && !isSameFaction && p.ClassName == "Pícaro")
            {
                return Color.DarkViolet; // Stealth enemigo (solo pícaro)
            }

            // Mismos bandos -> Aliados -> Azul brillante neón
            if (isSameFaction)
            {
                return Color.FromArgb(0, 191, 255);
            }

            // Bando contrario -> Enemigos -> Rojo brillante neón
            return Color.FromArgb(255, 0, 50);
        }

        static string ReadString(Memory m, uint address, int maxLen)
        {
            byte[] buffer = new byte[maxLen + 1];
            if (m.ReadBytes((IntPtr)address, buffer, maxLen))
            {
                int end = Array.IndexOf(buffer, (byte)0);
                if (end < 0) end = maxLen;
                string s = Encoding.UTF8.GetString(buffer, 0, end);
                if (s.Length > 0 && s[0] >= 32 && s[0] < 127)
                    return s;
            }
            return "";
        }

        static string GetClassName(byte classId)
        {
            switch (classId)
            {
                case 1: return "Guerrero";
                case 2: return "Paladín";
                case 3: return "Cazador";
                case 4: return "Pícaro";
                case 5: return "Sacerdote";
                case 6: return "Caballero de la Muerte";
                case 7: return "Chamán";
                case 8: return "Mago";
                case 9: return "Brujo";
                case 11: return "Druida";
                default: return "Clase";
            }
        }

        static byte GetClassIdByName(string className)
        {
            switch (className)
            {
                case "Guerrero": return 1;
                case "Paladín": return 2;
                case "Cazador": return 3;
                case "Pícaro": return 4;
                case "Sacerdote": return 5;
                case "Caballero de la Muerte": return 6;
                case "Chamán": return 7;
                case "Mago": return 8;
                case "Brujo": return 9;
                case "Druida": return 11;
                default: return 0;
            }
        }

        static string GetRaceName(byte raceId)
        {
            switch (raceId)
            {
                case 1: return "Humano";
                case 2: return "Orco";
                case 3: return "Enano";
                case 4: return "Elfo de la noche";
                case 5: return "No muerto";
                case 6: return "Tauren";
                case 7: return "Gnomo";
                case 8: return "Troll";
                case 10: return "Elfo de sangre";
                case 11: return "Draenei";
                default: return "Raza";
            }
        }
    }

    public class Memory
    {
        public IntPtr wowProc { get; set; }
        public Memory(IntPtr h) { wowProc = h; }

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryWrite = 0x00000020,
            QueryInformation = 0x00000400,
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(ProcessAccessFlags f, bool i, uint p);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr h, IntPtr a, [Out] byte[] b, int s, out IntPtr r);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, int s, out IntPtr r);

        public void WriteFloat(IntPtr address, float value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }

        public void WriteUInt(IntPtr address, uint value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }

        public void WriteUInt64(IntPtr address, ulong value)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            WriteProcessMemory(wowProc, address, buffer, buffer.Length, out _);
        }

        public T Read<T>(IntPtr address) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];
            if (ReadProcessMemory(wowProc, address, buffer, size, out _))
            {
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try { return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject()); }
                finally { handle.Free(); }
            }
            return default;
        }

        public T ReadSafe<T>(IntPtr address) where T : struct
        { try { return Read<T>(address); } catch { return default; } }

        public bool ReadBytes(IntPtr address, byte[] buffer, int size)
        {
            return ReadProcessMemory(wowProc, address, buffer, size, out _);
        }
    }

    public class EnemyDetailsForm : Form
    {
        private static readonly Font boldFont = new Font("Segoe UI", 8f, FontStyle.Bold);
        private static readonly Font regFont = new Font("Segoe UI", 7f, FontStyle.Regular);
        private static readonly Font hpFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        private static readonly Font pFont = new Font("Segoe UI", 7f, FontStyle.Bold);

        private List<RadarForm.PlayerInfo> currentTargets = new List<RadarForm.PlayerInfo>();
        private DateTime lastTargetTime = DateTime.MinValue;
        private System.Windows.Forms.Timer repaintTimer;
        private RadarForm radar;
        private int highlightedSlotIndex = -1;
        private DateTime highlightedSlotTime = DateTime.MinValue;

        public EnemyDetailsForm(RadarForm radar)
        {
            this.radar = radar;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(180, 4 + radar.Config.MaxDetailTargets * 56);
            this.BackColor = Color.Fuchsia; // Transparente
            this.TransparencyKey = Color.Fuchsia; // Clave de transparencia
            this.DoubleBuffered = true;
            this.TopMost = true;
            this.ShowInTaskbar = false;
            this.Visible = false; // Empezar oculto inicialmente

            // Temporizador interno para actualizar el dibujo a 30 FPS
            repaintTimer = new System.Windows.Forms.Timer();
            repaintTimer.Interval = 33;
            repaintTimer.Tick += (s, e) => this.Invalidate();
            repaintTimer.Start();

            // Lógica de clic de ventana de detalles para targetear en el juego
            this.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    int startY = 4;
                    int spacing = 56;
                    int slotHeight = 50;
                    for (int i = 0; i < currentTargets.Count; i++)
                    {
                        int slotY = startY + i * spacing;
                        if (e.Y >= slotY && e.Y <= slotY + slotHeight)
                        {
                            var target = currentTargets[i];
                            highlightedSlotIndex = i;
                            highlightedSlotTime = DateTime.Now;
                            this.Invalidate();
                            break;
                        }
                    }
                }
            };

            // Alineamiento dinámico durante cambio de tamaño de la ventana de detección
            this.Resize += (s, e) =>
            {
                if (this.Owner != null && !this.Owner.IsDisposed)
                {
                    this.Location = new Point(this.Owner.Location.X + this.Owner.Width + 6, this.Owner.Location.Y);
                }
            };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE (Evita que el panel de combate robe foco al hacer clic)
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW (Oculta de Alt-Tab)
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST (Siempre al frente)
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST)
            {
                Point pos = this.PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
                if (pos.X >= this.ClientSize.Width - 16 && pos.Y >= this.ClientSize.Height - 16)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                    return;
                }
            }
            base.WndProc(ref m);
        }

        public void UpdateTargets(List<RadarForm.PlayerInfo> targets, int displayTime)
        {
            // NUEVA CORRECCIÓN: Si el radar principal está oculto (por minimizado/cambio de ventana),
            // forzamos a la ventana de detalles a ocultarse y no procesamos nada.
            if (radar == null || !radar.Visible)
            {
                if (this.Visible)
                {
                    this.Visible = false;
                }
                return;
            }

            if (targets.Count > 0)
            {
                currentTargets = new List<RadarForm.PlayerInfo>(targets);
                lastTargetTime = DateTime.Now;

                if (!this.Visible)
                {
                    this.Visible = true;
                }
                this.Opacity = 0.95;
            }
            else
            {
                if (lastTargetTime == DateTime.MinValue)
                {
                    currentTargets.Clear();
                    this.Visible = false;
                }
                else
                {
                    double elapsed = (DateTime.Now - lastTargetTime).TotalSeconds;
                    if (elapsed >= (double)displayTime)
                    {
                        currentTargets.Clear();
                        this.Opacity = 0.0;
                        this.Visible = false;
                    }
                    else
                    {
                        double t = elapsed / (double)displayTime;
                        double currentOpacity = 0.95 - (0.95 * t);

                        if (currentOpacity <= 0.01)
                        {
                            currentTargets.Clear();
                            this.Opacity = 0.0;
                            this.Visible = false;
                        }
                        else
                        {
                            this.Opacity = currentOpacity;
                        }
                    }
                }
            }
        }

        private void DrawShadowText(Graphics g, string text, Font font, Brush brush, float x, float y)
        {
            // Sombra en diagonal de alta definición para legibilidad perfecta sin fondo de tarjeta
            g.DrawString(text, font, Brushes.Black, x + 1, y + 1);
            g.DrawString(text, font, brush, x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;

            // Limpiar fondo con la clave de transparencia Fuchsia (totalmente invisible)
            g.Clear(Color.Fuchsia);

            int slotHeight = 50;
            int startY = 4;
            int spacing = 56; // slotHeight + 6

            for (int i = 0; i < radar.Config.MaxDetailTargets; i++)
            {
                int currentY = startY + i * spacing;

                // Solo dibujar si hay amenazas activas. Las ranuras vacías son 100% transparentes e invisibles.
                if (i < currentTargets.Count)
                {
                    var target = currentTargets[i];
                    Color classColor = GetClassColor(target.ClassName);

                    bool isHighlighted = (i == highlightedSlotIndex && (DateTime.Now - highlightedSlotTime).TotalMilliseconds < 150);

                    // Relleno de fondo del contenedor (mezcla premium oscura del color de la clase para evitar el violeta/fucsia de la transparencia)
                    Color bgClassColor = Color.FromArgb(255,
                        (int)(20 * 0.75f + classColor.R * 0.25f),
                        (int)(20 * 0.75f + classColor.G * 0.25f),
                        (int)(25 * 0.75f + classColor.B * 0.25f));

                    if (isHighlighted)
                    {
                        bgClassColor = Color.FromArgb(255,
                            Math.Min(255, bgClassColor.R + 40),
                            Math.Min(255, bgClassColor.G + 40),
                            Math.Min(255, bgClassColor.B + 50));
                    }

                    using (Brush bgBrush = new SolidBrush(bgClassColor))
                    {
                        g.FillRectangle(bgBrush, 4, currentY, w - 8, slotHeight);
                    }

                    // Borde de ranura individual con el color de la clase
                    using (Pen slotBorder = new Pen(isHighlighted ? Color.White : classColor, isHighlighted ? 2.5f : 1.5f))
                    {
                        g.DrawRectangle(slotBorder, 4, currentY, w - 8, slotHeight);
                    }

                    // Fuentes estilizadas y ultra-compactas
                    {
                        // Nombre (en color de clase oficial, recortado si es muy largo) con sombra de contraste
                        string nameText = target.Name;
                        if (nameText.Length > 15) nameText = nameText.Substring(0, 13) + "..";

                        using (Brush nameBrush = new SolidBrush(classColor))
                        {
                            DrawShadowText(g, nameText, boldFont, nameBrush, 10, currentY + 2);
                        }

                        // Distancia (en color oro) con sombra de contraste
                        string distStr = $"{target.Distance:F1}y";
                        DrawShadowText(g, distStr, regFont, Brushes.Gold, w - 45, currentY + 3);

                        // Descripción breve de nivel, clase y hermandad/guild con sombra de contraste
                        string desc = $"N{target.Level} {target.ClassName}";
                        if (!string.IsNullOrEmpty(target.GuildName))
                        {
                            desc += $" <{target.GuildName}>";
                        }
                        if (desc.Length > 24) desc = desc.Substring(0, 22) + "..";
                        DrawShadowText(g, desc, regFont, Brushes.LightGray, 10, currentY + 14);

                        // Barra de Vida (Más alta para acomodar texto más grande)
                        int barWidth = w - 20;
                        int hpBarHeight = 11;
                        int hpY = currentY + 26;

                        float hpPercent = target.MaxHp > 0 ? (float)target.Hp / target.MaxHp : 0f;
                        hpPercent = Math.Clamp(hpPercent, 0f, 1f);

                        // Relleno de vida (verde neón premium)
                        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 20, 20, 20)), 10, hpY, barWidth, hpBarHeight);
                        int hpFill = (int)(barWidth * hpPercent);
                        if (hpFill > 0)
                        {
                            g.FillRectangle(new SolidBrush(Color.FromArgb(255, 0, 200, 80)), 10, hpY, hpFill, hpBarHeight);
                        }
                        g.DrawRectangle(new Pen(Color.FromArgb(255, 70, 70, 80)), 10, hpY, barWidth, hpBarHeight);

                        // Texto numérico de salud más grande y con sombra de contraste incrustada
                        string hpText = $"{target.Hp}/{target.MaxHp}";
                        SizeF hpSize = g.MeasureString(hpText, hpFont);
                        float hpTextX = 10 + (barWidth - hpSize.Width) / 2;
                        float hpTextY = hpY - 1f;

                        // Dibujar contorno de sombra en 4 direcciones para legibilidad perfecta
                        g.DrawString(hpText, hpFont, Brushes.Black, hpTextX + 1f, hpTextY);
                        g.DrawString(hpText, hpFont, Brushes.Black, hpTextX - 1f, hpTextY);
                        g.DrawString(hpText, hpFont, Brushes.Black, hpTextX, hpTextY + 1f);
                        g.DrawString(hpText, hpFont, Brushes.Black, hpTextX, hpTextY - 1f);
                        g.DrawString(hpText, hpFont, Brushes.White, hpTextX, hpTextY);

                        // Barra de recurso (Más alta para acomodar texto más grande)
                        int powerBarHeight = 9;
                        int powerY = currentY + 38;

                        float pPercent = target.MaxPower > 0 ? (float)target.Power / target.MaxPower : 0f;
                        pPercent = Math.Clamp(pPercent, 0f, 1f);

                        Color pColor = Color.FromArgb(255, 0, 112, 222); // Por defecto Maná (Azul)
                        if (target.PowerType == "Ira")
                        {
                            pColor = Color.FromArgb(255, 196, 31, 59); // Rojo
                        }
                        else if (target.PowerType == "Energía")
                        {
                            pColor = Color.FromArgb(255, 255, 245, 105); // Amarillo
                        }
                        else if (target.PowerType == "Poder Rúnico")
                        {
                            pColor = Color.FromArgb(255, 0, 200, 220); // Celeste
                        }

                        g.FillRectangle(new SolidBrush(Color.FromArgb(255, 20, 20, 20)), 10, powerY, barWidth, powerBarHeight);
                        int pFill = (int)(barWidth * pPercent);
                        if (pFill > 0)
                        {
                            g.FillRectangle(new SolidBrush(pColor), 10, powerY, pFill, powerBarHeight);
                        }
                        g.DrawRectangle(new Pen(Color.FromArgb(255, 70, 70, 80)), 10, powerY, barWidth, powerBarHeight);

                        // Texto numérico de recurso más grande (sin tipo de recurso, solo números) y con sombra de contraste
                        string pText = $"{target.Power}/{target.MaxPower}";
                        SizeF pSize = g.MeasureString(pText, pFont);
                        float pTextX = 10 + (barWidth - pSize.Width) / 2;
                        float pTextY = powerY - 1f;

                        // Dibujar contorno de sombra en 4 direcciones para legibilidad perfecta
                        g.DrawString(pText, pFont, Brushes.Black, pTextX + 1f, pTextY);
                        g.DrawString(pText, pFont, Brushes.Black, pTextX - 1f, pTextY);
                        g.DrawString(pText, pFont, Brushes.Black, pTextX, pTextY + 1f);
                        g.DrawString(pText, pFont, Brushes.Black, pTextX, pTextY - 1f);
                        g.DrawString(pText, pFont, Brushes.White, pTextX, pTextY);
                    }
                }
            }
        }

        private float centerXText(string text, Font font, Graphics g, int barWidth)
        {
            SizeF size = g.MeasureString(text, font);
            return (barWidth - size.Width) / 2;
        }

        private Color GetClassColor(string className)
        {
            return RadarForm.GetClassColor(className);
        }

        private Color GetPowerColor(string powerType)
        {
            switch (powerType)
            {
                case "Ira": return Color.FromArgb(196, 31, 59);
                case "Energía": return Color.FromArgb(255, 245, 105);
                case "Poder Rúnico": return Color.FromArgb(0, 200, 220);
                default: return Color.FromArgb(0, 112, 222);
            }
        }
    }
    public class SettingsForm : Form
    {
        private RadarForm radar;

        public SettingsForm(RadarForm radar)
        {
            this.radar = radar;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Configuración de Radar";
            this.Size = new Size(290, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(22, 24, 28);
            this.ForeColor = Color.FromArgb(230, 230, 230);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            Icon? customIcon = Program.GetApplicationIcon();
            if (customIcon != null)
            {
                this.Icon = customIcon;
            }

            Label lblHeader = new Label();
            lblHeader.Text = "Ajustes de Interfaz y Alertas";
            lblHeader.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblHeader.ForeColor = Color.Cyan;
            lblHeader.Location = new Point(15, 15);
            lblHeader.AutoSize = true;
            this.Controls.Add(lblHeader);

            int yOffset = 55;

            CheckBox chkPlayers = CreateCheckBox("Rastrear Jugadores", radar.Config.TrackPlayers, yOffset);
            yOffset += 30;

            CheckBox chkAllies = CreateCheckBox("  ├ Mostrar Aliados", radar.Config.TrackAllies, yOffset);
            yOffset += 28;

            CheckBox chkColorAllies = CreateCheckBox("  │  ├ Colorear por Clase", radar.Config.ColorAlliesByClass, yOffset);
            yOffset += 28;

            Label lblMaxAllies = new Label();
            lblMaxAllies.Text = "  │  └ Límite en radar:";
            lblMaxAllies.Location = new Point(20, yOffset);
            lblMaxAllies.AutoSize = true;
            lblMaxAllies.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(lblMaxAllies);

            NumericUpDown numMaxAllies = new NumericUpDown();
            numMaxAllies.Value = radar.Config.MaxAllies;
            numMaxAllies.Minimum = 5;
            numMaxAllies.Maximum = 200;
            numMaxAllies.Location = new Point(160, yOffset - 2);
            numMaxAllies.Width = 60;
            numMaxAllies.BackColor = Color.FromArgb(32, 34, 38);
            numMaxAllies.ForeColor = Color.White;
            numMaxAllies.BorderStyle = BorderStyle.FixedSingle;
            numMaxAllies.ValueChanged += (s, e) =>
            {
                radar.Config.MaxAllies = (int)numMaxAllies.Value;
                radar.SaveConfig();
            };
            this.Controls.Add(numMaxAllies);
            yOffset += 28;

            CheckBox chkEnemies = CreateCheckBox("  ├ Mostrar Enemigos", radar.Config.TrackEnemies, yOffset);
            yOffset += 28;

            CheckBox chkColorEnemies = CreateCheckBox("  │  ├ Colorear por Clase", radar.Config.ColorEnemiesByClass, yOffset);
            yOffset += 28;

            Label lblMaxEnemies = new Label();
            lblMaxEnemies.Text = "  │  └ Límite en radar:";
            lblMaxEnemies.Location = new Point(20, yOffset);
            lblMaxEnemies.AutoSize = true;
            lblMaxEnemies.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(lblMaxEnemies);

            NumericUpDown numMaxEnemies = new NumericUpDown();
            numMaxEnemies.Value = radar.Config.MaxEnemies;
            numMaxEnemies.Minimum = 5;
            numMaxEnemies.Maximum = 200;
            numMaxEnemies.Location = new Point(160, yOffset - 2);
            numMaxEnemies.Width = 60;
            numMaxEnemies.BackColor = Color.FromArgb(32, 34, 38);
            numMaxEnemies.ForeColor = Color.White;
            numMaxEnemies.BorderStyle = BorderStyle.FixedSingle;
            numMaxEnemies.ValueChanged += (s, e) =>
            {
                radar.Config.MaxEnemies = (int)numMaxEnemies.Value;
                radar.SaveConfig();
            };
            this.Controls.Add(numMaxEnemies);
            yOffset += 35;

            CheckBox chkNodes = CreateCheckBox("Rastrear Minas y Hierbas", radar.Config.TrackGameObjects, yOffset);
            yOffset += 28;

            CheckBox chkNPCs = CreateCheckBox("Rastrear NPCs/Monstruos", radar.Config.TrackNPCs, yOffset);
            yOffset += 28;

            CheckBox chkAudio = CreateCheckBox("Alertas Sonoras (Target)", radar.Config.PlayAudioAlerts, yOffset);
            yOffset += 28;

            Label lblAudioFaction = new Label();
            lblAudioFaction.Text = "  └ Sonar para:";
            lblAudioFaction.Location = new Point(20, yOffset);
            lblAudioFaction.AutoSize = true;
            lblAudioFaction.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lblAudioFaction.ForeColor = Color.White;
            this.Controls.Add(lblAudioFaction);

            ComboBox cbAudioFaction = new ComboBox();
            cbAudioFaction.DropDownStyle = ComboBoxStyle.DropDownList;
            cbAudioFaction.Items.AddRange(new string[] { "Solo Enemigos", "Solo Aliados", "Ambos" });
            cbAudioFaction.SelectedIndex = radar.Config.AudioAlertFaction;
            cbAudioFaction.Location = new Point(130, yOffset - 2);
            cbAudioFaction.Width = 120;
            cbAudioFaction.BackColor = Color.FromArgb(32, 34, 38);
            cbAudioFaction.ForeColor = Color.White;
            cbAudioFaction.FlatStyle = FlatStyle.Flat;
            this.Controls.Add(cbAudioFaction);
            yOffset += 35;

            CheckBox chkRogue = CreateCheckBox("[Defensa] Anti-Pícaro (Snapline)", radar.Config.AntiRogueAlerts, yOffset);
            yOffset += 35;

            CheckBox chkMinGame = CreateCheckBox("Minimizar con el juego", radar.Config.MinimizeWithGame, yOffset);
            yOffset += 35;

            Label lblMaxDetails = new Label();
            lblMaxDetails.Text = "Máx. enemigos en lista:";
            lblMaxDetails.Location = new Point(20, yOffset);
            lblMaxDetails.AutoSize = true;
            lblMaxDetails.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(lblMaxDetails);

            NumericUpDown numMaxDetails = new NumericUpDown();
            numMaxDetails.Value = radar.Config.MaxDetailTargets;
            numMaxDetails.Minimum = 3;
            numMaxDetails.Maximum = 10;
            numMaxDetails.Location = new Point(190, yOffset - 2);
            numMaxDetails.Width = 60;
            numMaxDetails.BackColor = Color.FromArgb(32, 34, 38);
            numMaxDetails.ForeColor = Color.White;
            numMaxDetails.BorderStyle = BorderStyle.FixedSingle;
            numMaxDetails.ValueChanged += (s, e) =>
            {
                radar.Config.MaxDetailTargets = (int)numMaxDetails.Value;
                radar.SaveConfig();
                if (radar.detailsForm != null && !radar.detailsForm.IsDisposed)
                {
                    radar.detailsForm.Height = 4 + radar.Config.MaxDetailTargets * 56;
                }
            };
            this.Controls.Add(numMaxDetails);
            yOffset += 35;

            Label lblDisplayTime = new Label();
            lblDisplayTime.Text = "Tiempo info enemigo (seg):";
            lblDisplayTime.Location = new Point(20, yOffset);
            lblDisplayTime.AutoSize = true;
            lblDisplayTime.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(lblDisplayTime);

            NumericUpDown numDisplayTime = new NumericUpDown();
            numDisplayTime.Value = radar.Config.InfoDisplayTime;
            numDisplayTime.Minimum = 1;
            numDisplayTime.Maximum = 60;
            numDisplayTime.Location = new Point(190, yOffset - 2);
            numDisplayTime.Width = 60;
            numDisplayTime.BackColor = Color.FromArgb(32, 34, 38);
            numDisplayTime.ForeColor = Color.White;
            numDisplayTime.BorderStyle = BorderStyle.FixedSingle;
            numDisplayTime.ValueChanged += (s, e) =>
            {
                radar.Config.InfoDisplayTime = (int)numDisplayTime.Value;
                radar.SaveConfig();
            };
            this.Controls.Add(numDisplayTime);
            yOffset += 35;

            Label lblCustomSound = new Label();
            lblCustomSound.Text = "Sonido Detección (.wav):";
            lblCustomSound.Location = new Point(20, yOffset);
            lblCustomSound.AutoSize = true;
            lblCustomSound.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(lblCustomSound);
            yOffset += 22;

            TextBox txtCustomSound = new TextBox();
            txtCustomSound.Text = radar.Config.CustomSoundPath;
            txtCustomSound.Location = new Point(20, yOffset);
            txtCustomSound.Width = 160;
            txtCustomSound.BackColor = Color.FromArgb(32, 34, 38);
            txtCustomSound.ForeColor = Color.White;
            txtCustomSound.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(txtCustomSound);

            Button btnBrowse = new Button();
            btnBrowse.Text = "Buscar";
            btnBrowse.Location = new Point(190, yOffset - 2);
            btnBrowse.Width = 70;
            btnBrowse.Height = 24;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderColor = Color.Gray;
            btnBrowse.BackColor = Color.FromArgb(45, 48, 54);
            btnBrowse.ForeColor = Color.White;
            btnBrowse.Cursor = Cursors.Hand;
            btnBrowse.Click += (s, e) =>
            {
                bool oldSettingsTopMost = this.TopMost;
                bool oldRadarTopMost = radar.TopMost;
                bool oldDetailsTopMost = radar.detailsForm != null ? radar.detailsForm.TopMost : false;

                this.TopMost = false;
                radar.TopMost = false;
                if (radar.detailsForm != null) radar.detailsForm.TopMost = false;

                try
                {
                    using (OpenFileDialog ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "Archivos de Audio (*.wav)|*.wav";
                        ofd.Title = "Seleccionar Sonido de Detección";
                        if (ofd.ShowDialog(this) == DialogResult.OK)
                        {
                            txtCustomSound.Text = ofd.FileName;
                            radar.Config.CustomSoundPath = ofd.FileName;
                            radar.SaveConfig();
                        }
                    }
                }
                finally
                {
                    this.TopMost = oldSettingsTopMost;
                    radar.TopMost = oldRadarTopMost;
                    if (radar.detailsForm != null) radar.detailsForm.TopMost = oldDetailsTopMost;
                }
            };
            this.Controls.Add(btnBrowse);

            txtCustomSound.TextChanged += (s, e) =>
            {
                radar.Config.CustomSoundPath = txtCustomSound.Text;
                radar.SaveConfig();
            };

            chkPlayers.CheckedChanged += (s, e) =>
            {
                radar.Config.TrackPlayers = chkPlayers.Checked;
                chkAllies.Enabled = chkPlayers.Checked;
                chkEnemies.Enabled = chkPlayers.Checked;
                chkColorAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                lblMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                numMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                chkColorEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                lblMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                numMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                radar.SaveConfig();
            };

            chkAllies.CheckedChanged += (s, e) =>
            {
                radar.Config.TrackAllies = chkAllies.Checked;
                chkColorAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                lblMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                numMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
                radar.SaveConfig();
            };

            chkEnemies.CheckedChanged += (s, e) =>
            {
                radar.Config.TrackEnemies = chkEnemies.Checked;
                chkColorEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                lblMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                numMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
                radar.SaveConfig();
            };

            chkColorAllies.CheckedChanged += (s, e) =>
            {
                radar.Config.ColorAlliesByClass = chkColorAllies.Checked;
                radar.SaveConfig();
            };

            chkColorEnemies.CheckedChanged += (s, e) =>
            {
                radar.Config.ColorEnemiesByClass = chkColorEnemies.Checked;
                radar.SaveConfig();
            };

            chkNodes.CheckedChanged += (s, e) => { radar.Config.TrackGameObjects = chkNodes.Checked; radar.SaveConfig(); };
            chkNPCs.CheckedChanged += (s, e) => { radar.Config.TrackNPCs = chkNPCs.Checked; radar.SaveConfig(); };
            chkAudio.CheckedChanged += (s, e) =>
            {
                radar.Config.PlayAudioAlerts = chkAudio.Checked;
                lblAudioFaction.Enabled = chkAudio.Checked;
                cbAudioFaction.Enabled = chkAudio.Checked;
                radar.SaveConfig();
            };
            cbAudioFaction.SelectedIndexChanged += (s, e) =>
            {
                radar.Config.AudioAlertFaction = cbAudioFaction.SelectedIndex;
                radar.SaveConfig();
            };
            chkRogue.CheckedChanged += (s, e) => { radar.Config.AntiRogueAlerts = chkRogue.Checked; radar.SaveConfig(); };
            chkMinGame.CheckedChanged += (s, e) => { radar.Config.MinimizeWithGame = chkMinGame.Checked; radar.SaveConfig(); };

            chkAllies.Enabled = chkPlayers.Checked;
            chkEnemies.Enabled = chkPlayers.Checked;
            chkColorAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
            lblMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
            numMaxAllies.Enabled = chkPlayers.Checked && chkAllies.Checked;
            chkColorEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
            lblMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
            numMaxEnemies.Enabled = chkPlayers.Checked && chkEnemies.Checked;
            lblAudioFaction.Enabled = chkAudio.Checked;
            cbAudioFaction.Enabled = chkAudio.Checked;
        }

        private CheckBox CreateCheckBox(string text, bool isChecked, int y)
        {
            var chk = new CheckBox();
            chk.Text = text;
            chk.Checked = isChecked;
            chk.Location = new Point(20, y);
            chk.AutoSize = true;
            chk.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            chk.FlatStyle = FlatStyle.Standard;
            chk.ForeColor = Color.White;
            chk.Cursor = Cursors.Hand;

            // Workaround para ventanas superpuestas (TopMost) que no roban el foco de Windows:
            chk.AutoCheck = false;
            chk.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    chk.Checked = !chk.Checked;
                }
            };

            this.Controls.Add(chk);
            return chk;
        }
    }

    public class ProcessSelectorForm : Form
    {
        public int SelectedPid { get; private set; }
        private ListBox listBox;

        public ProcessSelectorForm()
        {
            this.Text = "LoDGem-Radar - Selección de Proceso";
            this.Size = new Size(320, 280);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(22, 24, 28);
            this.ForeColor = Color.White;

            Icon? customIcon = Program.GetApplicationIcon();
            if (customIcon != null)
            {
                this.Icon = customIcon;
            }

            Label lbl = new Label();
            lbl.Text = "Ventanas de World of Warcraft detectadas:";
            lbl.Location = new Point(12, 12);
            lbl.AutoSize = true;
            lbl.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lbl.ForeColor = Color.Cyan;
            this.Controls.Add(lbl);

            listBox = new ListBox();
            listBox.Location = new Point(12, 35);
            listBox.Size = new Size(280, 130);
            listBox.BackColor = Color.FromArgb(32, 34, 38);
            listBox.ForeColor = Color.White;
            listBox.BorderStyle = BorderStyle.FixedSingle;
            listBox.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(listBox);

            Button btnRefresh = new Button();
            btnRefresh.Text = "Actualizar";
            btnRefresh.Location = new Point(12, 180);
            btnRefresh.Size = new Size(130, 35);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.FlatAppearance.BorderColor = Color.Cyan;
            btnRefresh.BackColor = Color.FromArgb(45, 48, 54);
            btnRefresh.ForeColor = Color.Cyan;
            btnRefresh.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnRefresh.Cursor = Cursors.Hand;
            btnRefresh.Click += (s, e) => LoadProcesses();
            this.Controls.Add(btnRefresh);

            Button btnConnect = new Button();
            btnConnect.Text = "Conectar";
            btnConnect.Location = new Point(162, 180);
            btnConnect.Size = new Size(130, 35);
            btnConnect.FlatStyle = FlatStyle.Flat;
            btnConnect.FlatAppearance.BorderColor = Color.LimeGreen;
            btnConnect.BackColor = Color.FromArgb(45, 48, 54);
            btnConnect.ForeColor = Color.LimeGreen;
            btnConnect.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btnConnect.Cursor = Cursors.Hand;
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
                string charName = "Desconocido";
                try
                {
                    IntPtr handle = Memory.OpenProcess(Memory.ProcessAccessFlags.VirtualMemoryRead | Memory.ProcessAccessFlags.QueryInformation, false, (uint)p.Id);
                    if (handle != IntPtr.Zero)
                    {
                        var mem = new Memory(handle);
                        byte[] buffer = new byte[32];
                        if (mem.ReadBytes((IntPtr)0x00C79D18, buffer, buffer.Length))
                        {
                            int len = 0;
                            while (len < buffer.Length && buffer[len] != 0) len++;
                            if (len > 0)
                            {
                                charName = System.Text.Encoding.UTF8.GetString(buffer, 0, len);
                            }
                            else
                            {
                                charName = "Menú / Selección de Pj";
                            }
                        }
                        Memory.CloseHandle(handle);
                    }
                }
                catch { }

                listBox.Items.Add($"Personaje: {charName} (PID: {p.Id}) - {p.MainWindowTitle}");
            }
            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = 0;
            else
                listBox.Items.Add("No se encontró ningún proceso Wow.exe");
        }

        private void BtnConnect_Click(object? sender, EventArgs e)
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
}
