using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Gravador de Tela")]
[assembly: AssemblyProduct("Gravador de Tela")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace GravadorDeTela
{
    static class Program
    {
        const string LibName = "ScreenRecorderLib";
        const string VcRedistUrl = "https://aka.ms/vs/17/release/vc_redist.x64.exe";

        [STAThread]
        static int Main(string[] args)
        {
            // Precisa ser registrado antes de qualquer código que use a biblioteca.
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbedded;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 2 && args[0] == "--teste")
                return SelfTest(args[1]);

            bool created;
            using (Mutex mutex = new Mutex(true, "GravadorDeTela_InstanciaUnica", out created))
            {
                if (!created)
                {
                    MessageBox.Show("O Gravador de Tela já está aberto.", "Gravador de Tela",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                return Run();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Run()
        {
            try
            {
                Engine.Probe();
            }
            catch (Exception ex)
            {
                DialogResult r = MessageBox.Show(
                    "Não foi possível carregar o componente de gravação.\n\n" +
                    "Normalmente isso significa que falta o \"Microsoft Visual C++ Redistributable (x64)\" " +
                    "neste computador. Deseja abrir a página de download da Microsoft?\n\n" +
                    "Detalhe: " + ex.Message,
                    "Gravador de Tela", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (r == DialogResult.Yes)
                {
                    try { System.Diagnostics.Process.Start(VcRedistUrl); }
                    catch { }
                }
                return 2;
            }

            Application.Run(new MainForm());
            return 0;
        }

        /// <summary>
        /// A ScreenRecorderLib.dll vai embutida no .exe. Como ela contém código nativo, não pode
        /// ser carregada da memória: é extraída uma vez para %LOCALAPPDATA% e carregada de lá.
        /// </summary>
        static Assembly ResolveEmbedded(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            if (!string.Equals(name, LibName, StringComparison.OrdinalIgnoreCase)) return null;

            Assembly self = typeof(Program).Assembly;
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GravadorDeTela", "lib", self.GetName().Version.ToString());
            string path = Path.Combine(dir, LibName + ".dll");

            using (Stream src = self.GetManifestResourceStream(LibName + ".dll"))
            {
                if (src == null) return null;
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists || fi.Length != src.Length)
                {
                    Directory.CreateDirectory(dir);
                    using (FileStream dst = File.Create(path))
                        src.CopyTo(dst);
                }
            }
            return Assembly.LoadFrom(path);
        }

        /// <summary>
        /// Diagnóstico sem interface: "GravadorDeTela.exe --teste arquivo.mp4" grava ~5 s de um
        /// recorte da tela principal (3 s, pausa de 2 s, mais 2 s) com microfone e som do sistema.
        /// O resultado ("OK" ou o erro) fica em arquivo.mp4.log.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int SelfTest(string path)
        {
            string error = null;
            try
            {
                ManualResetEvent done = new ManualResetEvent(false);
                using (Engine engine = new Engine())
                {
                    engine.Completed += delegate(string p) { done.Set(); };
                    engine.Failed += delegate(string err) { error = err; done.Set(); };

                    RecordingRequest req = new RecordingRequest();
                    req.DisplayDeviceName = Screen.PrimaryScreen.DeviceName;
                    req.Crop = new Rectangle(100, 100, 640, 360);
                    req.Mic = true;
                    req.SystemAudio = true;
                    req.FilePath = path;

                    engine.Start(req);
                    Thread.Sleep(3000);
                    engine.Pause();
                    Thread.Sleep(2000);
                    engine.Resume();
                    Thread.Sleep(2000);
                    engine.Stop();
                    if (!done.WaitOne(30000)) error = "Tempo esgotado ao finalizar a gravação.";
                }
            }
            catch (Exception ex)
            {
                error = ex.ToString();
            }
            try { File.WriteAllText(path + ".log", error ?? "OK"); }
            catch { }
            return error == null ? 0 : 1;
        }
    }
}
