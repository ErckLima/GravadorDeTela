using System;
using System.Drawing;
using System.Windows.Forms;

namespace GravadorDeTela
{
    /// <summary>
    /// Moldura fina desenhada do lado de fora da área selecionada, para o usuário ver
    /// o que está sendo gravado. Não recebe cliques e não aparece no vídeo.
    /// </summary>
    class RegionFrame : Form
    {
        const int Thickness = 3;

        public RegionFrame()
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Opacity = 0.9; // torna a janela "layered", necessário para os cliques atravessarem
            BackColor = Color.DodgerBlue;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20        // WS_EX_TRANSPARENT: cliques atravessam
                            | 0x80        // WS_EX_TOOLWINDOW: fora do Alt+Tab
                            | 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Engine.ExcludeFromCapture(Handle);
        }

        public void SetArea(Rectangle area)
        {
            Rectangle outer = Rectangle.Inflate(area, Thickness, Thickness);
            Bounds = outer;
            Region old = Region;
            Region r = new Region(new Rectangle(0, 0, outer.Width, outer.Height));
            r.Exclude(new Rectangle(Thickness, Thickness, area.Width, area.Height));
            Region = r;
            if (old != null) old.Dispose();
        }
    }
}
