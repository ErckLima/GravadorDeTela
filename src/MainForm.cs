using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace GravadorDeTela
{
    enum AppState { Idle, Starting, Recording, Paused, Finishing }

    class ScreenItem
    {
        public Screen Screen;
        public string Label;
        public override string ToString() { return Label; }
    }

    class MainForm : Form
    {
        const int WM_HOTKEY = 0x0312;
        const int HotkeyRecord = 1;
        const int HotkeyPause = 2;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_NOREPEAT = 0x4000;
        const uint VK_F9 = 0x78, VK_F10 = 0x79;

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        readonly Engine _engine = new Engine();
        readonly Settings _settings;
        readonly Stopwatch _watch = new Stopwatch();
        readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();
        readonly RegionFrame _frame = new RegionFrame();

        AppState _state = AppState.Idle;
        Rectangle _area = Rectangle.Empty;
        string _currentFile;
        string _lastFile;
        bool _closePending;

        GroupBox gbVideo, gbAudio;
        RadioButton rbFull, rbArea;
        ComboBox cbScreen, cbMic;
        CheckBox chkTaskbar, chkMic, chkSys;
        Button btnArea, btnFolder, btnOpen, btnRec, btnPause, btnStop;
        Label lblArea, lblFolder, lblTimer, lblStatus, lblHint;
        TextBox txtFolder;
        LinkLabel lnkFile;

        public MainForm()
        {
            _settings = Settings.Load();
            BuildUi();
            LoadScreens();
            LoadMics();
            ApplySettings();

            _engine.StatusChanged += delegate(EngineStatus s) { OnUi(delegate { EngineStatusChanged(s); }); };
            _engine.Completed += delegate(string path) { OnUi(delegate { RecordingCompleted(path); }); };
            _engine.Failed += delegate(string error) { OnUi(delegate { RecordingFailed(error); }); };

            _timer.Interval = 250;
            _timer.Tick += delegate { UpdateClock(); };

            SetState(AppState.Idle);
            lblStatus.Text = "Pronto para gravar";
        }

        // ---------------------------------------------------------------- interface

        void BuildUi()
        {
            SuspendLayout();
            Text = "Gravador de Tela";
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(360, 432);

            gbVideo = new GroupBox();
            gbVideo.Text = "O que gravar";
            gbVideo.SetBounds(10, 8, 340, 132);

            rbFull = new RadioButton();
            rbFull.Text = "Tela inteira";
            rbFull.SetBounds(12, 22, 124, 22);
            rbFull.Checked = true;
            rbFull.CheckedChanged += delegate { ModeChanged(); };

            cbScreen = new ComboBox();
            cbScreen.DropDownStyle = ComboBoxStyle.DropDownList;
            cbScreen.SetBounds(140, 21, 188, 23);

            chkTaskbar = new CheckBox();
            chkTaskbar.Text = "Não mostrar a barra de tarefas";
            chkTaskbar.SetBounds(30, 47, 290, 22);

            rbArea = new RadioButton();
            rbArea.Text = "Área selecionada";
            rbArea.SetBounds(12, 76, 124, 22);

            btnArea = new Button();
            btnArea.Text = "Selecionar área...";
            btnArea.SetBounds(140, 73, 130, 27);
            btnArea.Click += delegate { PickArea(); };

            lblArea = new Label();
            lblArea.ForeColor = SystemColors.GrayText;
            lblArea.SetBounds(30, 104, 300, 18);

            gbVideo.Controls.AddRange(new Control[] { rbFull, cbScreen, chkTaskbar, rbArea, btnArea, lblArea });

            gbAudio = new GroupBox();
            gbAudio.Text = "Áudio";
            gbAudio.SetBounds(10, 146, 340, 84);

            chkMic = new CheckBox();
            chkMic.Text = "Microfone";
            chkMic.SetBounds(12, 23, 124, 22);
            chkMic.CheckedChanged += delegate { cbMic.Enabled = chkMic.Checked && _state == AppState.Idle; };

            cbMic = new ComboBox();
            cbMic.DropDownStyle = ComboBoxStyle.DropDownList;
            cbMic.SetBounds(140, 22, 188, 23);
            cbMic.DropDown += delegate { LoadMics(); };

            chkSys = new CheckBox();
            chkSys.Text = "Som do computador";
            chkSys.SetBounds(12, 52, 300, 22);

            gbAudio.Controls.AddRange(new Control[] { chkMic, cbMic, chkSys });

            lblFolder = new Label();
            lblFolder.Text = "Salvar em:";
            lblFolder.SetBounds(10, 243, 66, 18);

            txtFolder = new TextBox();
            txtFolder.ReadOnly = true;
            txtFolder.SetBounds(78, 240, 178, 23);

            btnFolder = new Button();
            btnFolder.Text = "...";
            btnFolder.SetBounds(260, 239, 32, 25);
            btnFolder.Click += delegate { ChooseFolder(); };

            btnOpen = new Button();
            btnOpen.Text = "Abrir";
            btnOpen.SetBounds(296, 239, 54, 25);
            btnOpen.Click += delegate { OpenFolder(); };

            lblTimer = new Label();
            lblTimer.Text = "00:00:00";
            lblTimer.Font = new Font("Segoe UI", 24f, FontStyle.Bold);
            lblTimer.TextAlign = ContentAlignment.MiddleCenter;
            lblTimer.SetBounds(10, 270, 340, 48);

            lblStatus = new Label();
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.SetBounds(10, 318, 340, 20);

            lnkFile = new LinkLabel();
            lnkFile.TextAlign = ContentAlignment.MiddleCenter;
            lnkFile.AutoEllipsis = true;
            lnkFile.SetBounds(10, 338, 340, 18);
            lnkFile.Visible = false;
            lnkFile.LinkClicked += delegate { ShowLastFile(); };

            btnRec = new Button();
            btnRec.Text = "Gravar";
            btnRec.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnRec.ForeColor = Color.FromArgb(200, 30, 30);
            btnRec.SetBounds(10, 364, 124, 38);
            btnRec.Click += delegate { StartRecording(); };

            btnPause = new Button();
            btnPause.Text = "Pausar";
            btnPause.SetBounds(140, 364, 108, 38);
            btnPause.Click += delegate { TogglePause(); };

            btnStop = new Button();
            btnStop.Text = "Parar";
            btnStop.SetBounds(254, 364, 96, 38);
            btnStop.Click += delegate { StopRecording(); };

            lblHint = new Label();
            lblHint.Text = "Ctrl+Alt+F9: gravar / parar     Ctrl+Alt+F10: pausar";
            lblHint.ForeColor = SystemColors.GrayText;
            lblHint.TextAlign = ContentAlignment.MiddleCenter;
            lblHint.SetBounds(10, 408, 340, 18);

            Controls.AddRange(new Control[] {
                gbVideo, gbAudio, lblFolder, txtFolder, btnFolder, btnOpen,
                lblTimer, lblStatus, lnkFile, btnRec, btnPause, btnStop, lblHint });

            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();
        }

        void LoadScreens()
        {
            cbScreen.Items.Clear();
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                ScreenItem item = new ScreenItem();
                item.Screen = screens[i];
                item.Label = "Tela " + (i + 1) + " (" + screens[i].Bounds.Width + " x " + screens[i].Bounds.Height + ")"
                    + (screens[i].Primary ? " - principal" : "");
                cbScreen.Items.Add(item);
                if (screens[i].Primary) cbScreen.SelectedIndex = i;
            }
            if (cbScreen.SelectedIndex < 0 && cbScreen.Items.Count > 0) cbScreen.SelectedIndex = 0;
        }

        void LoadMics()
        {
            MicDevice current = cbMic.SelectedItem as MicDevice;
            string wanted = current != null ? current.Id : _settings.MicId;

            List<MicDevice> mics;
            try { mics = Engine.ListMics(); }
            catch { mics = new List<MicDevice>(); }

            cbMic.Items.Clear();
            int select = -1;
            for (int i = 0; i < mics.Count; i++)
            {
                cbMic.Items.Add(mics[i]);
                if (mics[i].Id == wanted) select = i;
                else if (select < 0 && mics[i].IsDefault && string.IsNullOrEmpty(wanted)) select = i;
            }
            if (select < 0)
                for (int i = 0; i < mics.Count; i++)
                    if (mics[i].IsDefault) select = i;
            if (select < 0 && mics.Count > 0) select = 0;

            if (mics.Count == 0)
            {
                cbMic.Items.Add("(nenhum microfone encontrado)");
                cbMic.SelectedIndex = 0;
            }
            else
            {
                cbMic.SelectedIndex = select;
            }
        }

        void ApplySettings()
        {
            chkMic.Checked = _settings.Mic;
            chkSys.Checked = _settings.SystemAudio;
            chkTaskbar.Checked = _settings.HideTaskbar;
            txtFolder.Text = _settings.Folder;

            // Só reaproveita a área salva se ela ainda couber em algum monitor.
            if (!_settings.Area.IsEmpty)
            {
                foreach (Screen s in Screen.AllScreens)
                    if (s.Bounds.Contains(_settings.Area)) _area = _settings.Area;
            }
            rbArea.Checked = _settings.AreaMode && !_area.IsEmpty;
            if (!rbArea.Checked) rbFull.Checked = true;
            ModeChanged();
        }

        void SaveSettings()
        {
            _settings.Mic = chkMic.Checked;
            MicDevice mic = cbMic.SelectedItem as MicDevice;
            _settings.MicId = mic != null ? mic.Id : "";
            _settings.SystemAudio = chkSys.Checked;
            _settings.HideTaskbar = chkTaskbar.Checked;
            _settings.AreaMode = rbArea.Checked;
            _settings.Area = _area;
            _settings.Folder = txtFolder.Text;
            _settings.Save();
        }

        void ModeChanged()
        {
            bool idle = _state == AppState.Idle;
            cbScreen.Enabled = idle && rbFull.Checked;
            chkTaskbar.Enabled = idle && rbFull.Checked;
            btnArea.Enabled = idle;
            lblArea.Text = _area.IsEmpty
                ? "Nenhuma área selecionada"
                : "Área: " + _area.Width + " x " + _area.Height + " pixels";
            UpdateFrame();
        }

        void UpdateFrame()
        {
            if (rbArea.Checked && !_area.IsEmpty && Visible)
            {
                _frame.BackColor =
                    _state == AppState.Recording ? Color.FromArgb(230, 40, 40) :
                    _state == AppState.Paused ? Color.Orange :
                    Color.DodgerBlue;
                _frame.SetArea(_area);
                if (!_frame.Visible) _frame.Show();
            }
            else if (_frame.Visible)
            {
                _frame.Hide();
            }
        }

        void SetState(AppState state)
        {
            _state = state;
            bool idle = state == AppState.Idle;
            bool active = state == AppState.Recording || state == AppState.Paused;

            rbFull.Enabled = idle;
            rbArea.Enabled = idle;
            chkMic.Enabled = idle && cbMic.SelectedItem is MicDevice;
            cbMic.Enabled = idle && chkMic.Checked;
            chkSys.Enabled = idle;
            btnFolder.Enabled = idle;
            btnRec.Enabled = idle;
            btnPause.Enabled = active;
            btnStop.Enabled = active;
            btnPause.Text = state == AppState.Paused ? "Continuar" : "Pausar";
            lblTimer.ForeColor =
                state == AppState.Recording ? Color.FromArgb(200, 30, 30) :
                state == AppState.Paused ? Color.DarkOrange :
                SystemColors.ControlText;

            if (!(cbMic.SelectedItem is MicDevice)) chkMic.Checked = false;
            ModeChanged();
        }

        void OnUi(MethodInvoker action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }

        // ---------------------------------------------------------------- ações

        bool PickArea()
        {
            if (_state != AppState.Idle) return false;

            // Esconde as janelas do gravador para elas não saírem no "print" da seleção.
            _frame.Hide();
            Hide();
            Application.DoEvents();
            Thread.Sleep(200);

            bool ok;
            Rectangle selection = Rectangle.Empty;
            using (RegionSelector selector = new RegionSelector())
            {
                ok = selector.ShowDialog() == DialogResult.OK;
                selection = selector.Selection;
            }

            Show();
            Activate();
            if (ok)
            {
                _area = selection;
                rbArea.Checked = true;
            }
            ModeChanged();
            return ok;
        }

        void StartRecording()
        {
            if (_state != AppState.Idle) return;

            Screen screen;
            Rectangle? crop = null;

            if (rbArea.Checked)
            {
                if (_area.IsEmpty && !PickArea()) return;
                screen = Screen.FromRectangle(_area);
                Rectangle r = Rectangle.Intersect(_area, screen.Bounds);
                if (r.Width < 32 || r.Height < 32)
                {
                    _area = Rectangle.Empty;
                    if (!PickArea()) return;
                    screen = Screen.FromRectangle(_area);
                    r = Rectangle.Intersect(_area, screen.Bounds);
                }
                r.Offset(-screen.Bounds.X, -screen.Bounds.Y);
                crop = MakeEven(r);
            }
            else
            {
                ScreenItem item = cbScreen.SelectedItem as ScreenItem;
                screen = item != null ? item.Screen : Screen.PrimaryScreen;
                if (chkTaskbar.Checked)
                {
                    Rectangle work = Rectangle.Intersect(screen.WorkingArea, screen.Bounds);
                    if (work != screen.Bounds && work.Width >= 32 && work.Height >= 32)
                    {
                        work.Offset(-screen.Bounds.X, -screen.Bounds.Y);
                        crop = MakeEven(work);
                    }
                }
            }

            string folder = txtFolder.Text;
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível usar a pasta de destino:\n" + folder + "\n\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MicDevice mic = cbMic.SelectedItem as MicDevice;

            RecordingRequest req = new RecordingRequest();
            req.DisplayDeviceName = screen.DeviceName;
            req.Crop = crop;
            req.Mic = chkMic.Checked && mic != null;
            req.MicDeviceId = mic != null ? mic.Id : null;
            req.SystemAudio = chkSys.Checked;
            req.FilePath = Path.Combine(folder, "Gravacao_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".mp4");

            _currentFile = req.FilePath;
            _watch.Reset();
            lblTimer.Text = "00:00:00";
            lnkFile.Visible = false;
            lblStatus.Text = "Iniciando...";
            SetState(AppState.Starting);
            SaveSettings();

            try
            {
                _engine.Start(req);
            }
            catch (Exception ex)
            {
                RecordingFailed(ex.Message);
            }
        }

        static Rectangle MakeEven(Rectangle r)
        {
            r.Width -= r.Width % 2;
            r.Height -= r.Height % 2;
            return r;
        }

        void TogglePause()
        {
            if (_state == AppState.Recording) _engine.Pause();
            else if (_state == AppState.Paused) _engine.Resume();
        }

        void StopRecording()
        {
            if (_state != AppState.Recording && _state != AppState.Paused) return;
            _watch.Stop();
            lblStatus.Text = "Finalizando o vídeo...";
            SetState(AppState.Finishing);
            _engine.Stop();
        }

        void ChooseFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pasta onde os vídeos serão salvos";
                dlg.SelectedPath = txtFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    txtFolder.Text = dlg.SelectedPath;
                    SaveSettings();
                }
            }
        }

        void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(txtFolder.Text);
                Process.Start("explorer.exe", "\"" + txtFolder.Text + "\"");
            }
            catch { }
        }

        void ShowLastFile()
        {
            try
            {
                if (_lastFile != null && File.Exists(_lastFile))
                    Process.Start("explorer.exe", "/select,\"" + _lastFile + "\"");
            }
            catch { }
        }

        // ---------------------------------------------------------------- eventos da gravação

        void EngineStatusChanged(EngineStatus status)
        {
            if (status == EngineStatus.Recording && (_state == AppState.Starting || _state == AppState.Paused))
            {
                _watch.Start();
                _timer.Start();
                SetState(AppState.Recording);
                UpdateClock();
            }
            else if (status == EngineStatus.Paused && _state == AppState.Recording)
            {
                _watch.Stop();
                SetState(AppState.Paused);
                UpdateClock();
            }
        }

        void RecordingCompleted(string path)
        {
            _timer.Stop();
            _watch.Stop();
            _lastFile = string.IsNullOrEmpty(path) ? _currentFile : path;
            SetState(AppState.Idle);

            lblStatus.Text = "Gravação salva (" + FormatSize(FileSize(_lastFile)) + ")";
            lnkFile.Text = Path.GetFileName(_lastFile);
            lnkFile.Visible = true;

            if (_closePending) Close();
        }

        void RecordingFailed(string error)
        {
            _timer.Stop();
            _watch.Stop();
            SetState(AppState.Idle);
            lblStatus.Text = "A gravação falhou";

            try
            {
                if (_currentFile != null && File.Exists(_currentFile) && new FileInfo(_currentFile).Length == 0)
                    File.Delete(_currentFile);
            }
            catch { }

            if (_closePending)
            {
                Close();
                return;
            }
            MessageBox.Show(this, "Não foi possível gravar.\n\n" + error, Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        void UpdateClock()
        {
            TimeSpan t = _watch.Elapsed;
            lblTimer.Text = ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
            if (_state == AppState.Recording)
                lblStatus.Text = "Gravando  -  " + FormatSize(FileSize(_currentFile));
            else if (_state == AppState.Paused)
                lblStatus.Text = "Pausado  -  " + FormatSize(FileSize(_currentFile));
        }

        static long FileSize(string path)
        {
            try { return new FileInfo(path).Length; }
            catch { return 0; }
        }

        static string FormatSize(long bytes)
        {
            double mb = bytes / (1024.0 * 1024.0);
            if (mb >= 1024) return (mb / 1024.0).ToString("0.00", PtBr) + " GB";
            return mb.ToString("0.0", PtBr) + " MB";
        }

        // ---------------------------------------------------------------- janela

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // O painel do gravador não aparece no vídeo, mesmo gravando a tela inteira.
            Engine.ExcludeFromCapture(Handle);
            RegisterHotKey(Handle, HotkeyRecord, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F9);
            RegisterHotKey(Handle, HotkeyPause, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F10);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeyRecord);
            UnregisterHotKey(Handle, HotkeyPause);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeyRecord)
                {
                    if (_state == AppState.Idle) StartRecording();
                    else StopRecording();
                }
                else if (id == HotkeyPause)
                {
                    TogglePause();
                }
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateFrame();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_state != AppState.Idle)
            {
                // Fecha só depois de o arquivo ser finalizado, para não perder a gravação.
                e.Cancel = true;
                _closePending = true;
                StopRecording();
                return;
            }
            SaveSettings();
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _frame.Close();
            _engine.Dispose();
            base.OnFormClosed(e);
        }
    }
}
