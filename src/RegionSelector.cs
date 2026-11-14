using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace GravadorDeTela
{
    /// <summary>
    /// Seleção de área no estilo "print": congela a tela, escurece tudo e o usuário
    /// arrasta um retângulo. A seleção fica limitada ao monitor onde o arrasto começou.
    /// </summary>
    class RegionSelector : Form
    {
        const int MinSize = 32;

        readonly Rectangle _virtual;
        readonly Bitmap _shot;
        readonly Bitmap _dark;
        Point _start;
        Rectangle _limit;
        Rectangle _sel;
        bool _dragging;

        /// <summary>Área escolhida, em coordenadas de tela (pixels reais).</summary>
        public Rectangle Selection { get; private set; }

        public RegionSelector()
        {
            _virtual = SystemInformation.VirtualScreen;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = _virtual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            Text = "Selecionar área";

            _shot = new Bitmap(_virtual.Width, _virtual.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(_shot))
                g.CopyFromScreen(_virtual.Location, Point.Empty, _virtual.Size);

            _dark = new Bitmap(_shot);
            using (Graphics g = Graphics.FromImage(_dark))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
                g.FillRectangle(b, 0, 0, _dark.Width, _dark.Height);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Bounds = _virtual;
            Activate();
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Rectangle all = new Rectangle(0, 0, _dark.Width, _dark.Height);
            g.DrawImage(_dark, all, all, GraphicsUnit.Pixel);

            if (_sel.Width > 0 && _sel.Height > 0)
            {
                g.DrawImage(_shot, _sel, _sel, GraphicsUnit.Pixel);
                using (Pen p = new Pen(Color.FromArgb(230, 40, 40), 2f))
                    g.DrawRectangle(p, _sel);

                string size = _sel.Width + " x " + _sel.Height;
                using (Font f = new Font("Segoe UI", 10f, FontStyle.Bold))
                {
                    SizeF ts = g.MeasureString(size, f);
                    float tx = _sel.X;
                    float ty = _sel.Y - ts.Height - 6;
                    if (ty < _limit.Y) ty = _sel.Y + 6;
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                        g.FillRectangle(bg, tx, ty, ts.Width + 8, ts.Height + 2);
                    g.DrawString(size, f, Brushes.White, tx + 4, ty + 1);
                }
            }
            else
            {
                Rectangle primary = Screen.PrimaryScreen.Bounds;
                primary.Offset(-_virtual.X, -_virtual.Y);
                string hint = "Arraste para selecionar a área que será gravada   •   Esc cancela";
                using (Font f = new Font("Segoe UI", 14f, FontStyle.Bold))
                {
                    SizeF ts = g.MeasureString(hint, f);
                    float tx = primary.X + (primary.Width - ts.Width) / 2f;
                    float ty = primary.Y + 60;
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                        g.FillRectangle(bg, tx - 14, ty - 8, ts.Width + 28, ts.Height + 16);
                    g.DrawString(hint, f, Brushes.White, tx, ty);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            if (e.Button != MouseButtons.Left) return;

            _start = e.Location;
            Point screenPt = new Point(e.X + _virtual.X, e.Y + _virtual.Y);
            _limit = Screen.FromPoint(screenPt).Bounds;
            _limit.Offset(-_virtual.X, -_virtual.Y);
            _sel = Rectangle.Empty;
            _dragging = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            Rectangle r = Rectangle.FromLTRB(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
            _sel = Rectangle.Intersect(r, _limit);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_dragging || e.Button != MouseButtons.Left) return;
            _dragging = false;

            if (_sel.Width >= MinSize && _sel.Height >= MinSize)
            {
                Rectangle r = _sel;
                r.Width -= r.Width % 2;   // o H.264 exige largura e altura pares
                r.Height -= r.Height % 2;
                r.Offset(_virtual.X, _virtual.Y);
                Selection = r;
                DialogResult = DialogResult.OK;
            }
            else
            {
                _sel = Rectangle.Empty;
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shot.Dispose();
                _dark.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
