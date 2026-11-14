using System;
using System.Collections.Generic;
using System.Drawing;
using ScreenRecorderLib;

namespace GravadorDeTela
{
    enum EngineStatus { Idle, Recording, Paused, Finishing }

    class MicDevice
    {
        public string Name;
        public string Id;
        public bool IsDefault;
        public override string ToString() { return Name; }
    }

    class RecordingRequest
    {
        /// <summary>Nome do monitor, ex.: \\.\DISPLAY1</summary>
        public string DisplayDeviceName;
        /// <summary>Recorte em pixels, relativo ao canto do monitor. Null = monitor inteiro.</summary>
        public Rectangle? Crop;
        public bool Mic;
        public string MicDeviceId;
        public bool SystemAudio;
        public string FilePath;
    }

    /// <summary>
    /// Camada fina sobre a ScreenRecorderLib. O vídeo é codificado em H.264 e escrito
    /// direto no disco durante a gravação, então não existe limite de duração ou tamanho.
    /// Os eventos chegam em uma thread que não é a da interface.
    /// </summary>
    class Engine : IDisposable
    {
        Recorder _rec;

        public event Action<EngineStatus> StatusChanged;
        public event Action<string> Completed;
        public event Action<string> Failed;

        /// <summary>Força o carregamento da biblioteca; lança exceção se faltar alguma dependência.</summary>
        public static string Probe()
        {
            return typeof(Recorder).Assembly.FullName;
        }

        public static List<MicDevice> ListMics()
        {
            List<MicDevice> result = new List<MicDevice>();
            foreach (RecordableAudioCaptureDevice d in Recorder.GetSystemAudioCaptureDevices())
            {
                MicDevice m = new MicDevice();
                m.Name = d.FriendlyName;
                m.Id = d.DeviceName;
                m.IsDefault = d.IsDefaultDevice;
                result.Add(m);
            }
            return result;
        }

        /// <summary>Faz a janela não aparecer no vídeo gravado (Windows 10 2004 ou superior).</summary>
        public static void ExcludeFromCapture(IntPtr hwnd)
        {
            try { Recorder.SetExcludeFromCapture(hwnd, true); }
            catch { }
        }

        public void Start(RecordingRequest req)
        {
            DisposeRecorder();

            DisplayRecordingSource source = new DisplayRecordingSource(req.DisplayDeviceName);
            source.IsCursorCaptureEnabled = true;
            if (req.Crop.HasValue)
            {
                Rectangle c = req.Crop.Value;
                source.SourceRect = new ScreenRect(c.X, c.Y, c.Width, c.Height);
            }

            List<AudioSourceBase> audio = new List<AudioSourceBase>();
            if (req.Mic)
            {
                CaptureAudioSource mic = string.IsNullOrEmpty(req.MicDeviceId)
                    ? CaptureAudioSource.Default
                    : new CaptureAudioSource(req.MicDeviceId);
                if (mic != null) audio.Add(mic);
            }
            if (req.SystemAudio)
            {
                LoopbackAudioSource loopback = LoopbackAudioSource.Default;
                if (loopback != null) audio.Add(loopback);
            }

            RecorderOptions opts = RecorderOptions.Default;
            opts.SourceOptions.RecordingSources.Add(source);
            opts.OutputOptions.RecorderMode = RecorderMode.Video;
            opts.AudioOptions.IsAudioEnabled = audio.Count > 0;
            opts.AudioOptions.AudioSources = audio;
            opts.AudioOptions.Bitrate = AudioBitrate.bitrate_128kbps;
            opts.AudioOptions.Channels = AudioChannels.Stereo;
            opts.VideoEncoderOptions.Framerate = 30;
            opts.VideoEncoderOptions.Quality = 70;
            opts.VideoEncoderOptions.IsHardwareEncodingEnabled = true;
            // Sem "fast start": evita reescrever o arquivo inteiro ao finalizar gravações longas.
            opts.VideoEncoderOptions.IsMp4FastStartEnabled = false;
            opts.VideoEncoderOptions.IsFragmentedMp4Enabled = false;
            opts.MouseOptions.IsMousePointerEnabled = true;

            _rec = Recorder.CreateRecorder(opts);
            _rec.OnStatusChanged += OnStatus;
            _rec.OnRecordingComplete += OnComplete;
            _rec.OnRecordingFailed += OnFailed;
            _rec.Record(req.FilePath);
        }

        public void Pause() { if (_rec != null) _rec.Pause(); }
        public void Resume() { if (_rec != null) _rec.Resume(); }
        public void Stop() { if (_rec != null) _rec.Stop(); }

        void OnStatus(object sender, RecordingStatusEventArgs e)
        {
            EngineStatus s;
            switch (e.Status)
            {
                case RecorderStatus.Recording: s = EngineStatus.Recording; break;
                case RecorderStatus.Paused: s = EngineStatus.Paused; break;
                case RecorderStatus.Finishing: s = EngineStatus.Finishing; break;
                default: s = EngineStatus.Idle; break;
            }
            Action<EngineStatus> h = StatusChanged;
            if (h != null) h(s);
        }

        void OnComplete(object sender, RecordingCompleteEventArgs e)
        {
            Action<string> h = Completed;
            if (h != null) h(e.FilePath);
        }

        void OnFailed(object sender, RecordingFailedEventArgs e)
        {
            Action<string> h = Failed;
            if (h != null) h(e.Error);
        }

        void DisposeRecorder()
        {
            if (_rec == null) return;
            _rec.OnStatusChanged -= OnStatus;
            _rec.OnRecordingComplete -= OnComplete;
            _rec.OnRecordingFailed -= OnFailed;
            try { _rec.Dispose(); }
            catch { }
            _rec = null;
        }

        public void Dispose()
        {
            DisposeRecorder();
        }
    }
}
