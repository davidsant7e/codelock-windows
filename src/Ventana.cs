// Ventana de la pregunta, en el estilo duotono de los Arcanos (azul noche + durazno).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CodeLock
{
    public enum Resultado { Correcta, Incorrecta, Saltada }

    public class VentanaPregunta : Form
    {
        public static readonly Color Noche = Color.FromArgb(0x1f, 0x2a, 0x5c);
        public static readonly Color NocheClara = Color.FromArgb(0x2b, 0x38, 0x73);
        public static readonly Color Durazno = Color.FromArgb(0xf4, 0xa5, 0x82);
        public static readonly Color Crema = Color.FromArgb(0xfb, 0xe9, 0xdf);
        public static readonly Color Error = Color.FromArgb(0x9e, 0x3b, 0x4f);

        const int Ancho = 560;

        readonly Pregunta pregunta;
        readonly List<Button> botones = new List<Button>();
        readonly Label lblResultado;
        readonly Label lblExplicacion;
        readonly Button btnSaltar;
        readonly Button btnOtra;
        readonly Button btnCerrar;
        bool respondida;

        /// <summary>Se llama una sola vez con el resultado (saltada si se cierra sin responder).</summary>
        public event Action<Resultado> Respondio;
        /// <summary>El usuario pidió otra pregunta al terminar.</summary>
        public bool QuiereOtra { get; private set; }

        public VentanaPregunta(Pregunta p, string contador)
        {
            pregunta = p;
            Text = "CodeLock";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            ShowInTaskbar = true;
            KeyPreview = true;
            BackColor = Noche;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(24, 20, 24, 20);

            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                BackColor = Noche,
            };

            panel.Controls.Add(Etiqueta("V · CODELOCK   ·   " + p.Categoria.ToUpperInvariant() + "   ·   " + contador,
                new Font("Segoe UI", 8.5f, FontStyle.Bold), Durazno, new Padding(0, 0, 0, 10)));
            panel.Controls.Add(Etiqueta(p.Enunciado, new Font("Segoe UI", 14f), Color.White, new Padding(0, 0, 0, 14)));

            for (int i = 0; i < p.Opciones.Count; i++)
            {
                int indice = i;
                var b = new Button
                {
                    Text = (i + 1) + "   " + p.Opciones[i],
                    TextAlign = ContentAlignment.MiddleLeft,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = NocheClara,
                    ForeColor = Color.White,
                    Width = Ancho,
                    Margin = new Padding(0, 0, 0, 8),
                    Padding = new Padding(10, 6, 10, 6),
                    Cursor = Cursors.Hand,
                    UseMnemonic = false,
                    TabStop = true,
                };
                b.FlatAppearance.BorderColor = Durazno;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x3a, 0x48, 0x8a);
                b.Height = Math.Max(40, TextRenderer.MeasureText(b.Text, Font, new Size(Ancho - 40, 0), TextFormatFlags.WordBreak).Height + 18);
                b.Click += delegate { Responder(indice); };
                botones.Add(b);
                panel.Controls.Add(b);
            }

            lblResultado = Etiqueta("", new Font("Segoe UI", 11f, FontStyle.Bold), Durazno, new Padding(0, 8, 0, 4));
            lblExplicacion = Etiqueta("", Font, Crema, new Padding(0, 0, 0, 8));
            lblResultado.Visible = false;
            lblExplicacion.Visible = false;
            panel.Controls.Add(lblResultado);
            panel.Controls.Add(lblExplicacion);

            var fila = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Width = Ancho,
                MinimumSize = new Size(Ancho, 0),
                Margin = new Padding(0, 6, 0, 0),
                BackColor = Noche,
            };
            btnCerrar = Boton("Cerrar  (Esc)", false);
            btnCerrar.Click += delegate { Close(); };
            btnOtra = Boton("Otra pregunta", true);
            btnOtra.Click += delegate { QuiereOtra = true; Close(); };
            btnOtra.Visible = false;
            btnSaltar = Boton("Saltar", false);
            btnSaltar.Click += delegate { Close(); };
            fila.Controls.Add(btnCerrar);
            fila.Controls.Add(btnOtra);
            fila.Controls.Add(btnSaltar);
            panel.Controls.Add(fila);

            Controls.Add(panel);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            KeyDown += AlPresionarTecla;
            FormClosed += delegate { if (!respondida) Avisar(Resultado.Saltada); };
        }

        /// <summary>Centra la ventana en la pantalla donde está el mouse.</summary>
        public void Ubicar()
        {
            var area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 3);
        }

        void AlPresionarTecla(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; return; }
            if (respondida)
            {
                if (e.KeyCode == Keys.Enter) { Close(); e.Handled = true; }
                if (e.KeyCode == Keys.N) { QuiereOtra = true; Close(); e.Handled = true; }
                return;
            }
            int indice = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D6) indice = e.KeyCode - Keys.D1;
            else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad6) indice = e.KeyCode - Keys.NumPad1;
            else if (e.KeyCode >= Keys.A && e.KeyCode <= Keys.F) indice = e.KeyCode - Keys.A;
            if (indice >= 0 && indice < botones.Count)
            {
                Responder(indice);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        void Responder(int indice)
        {
            if (respondida) return;
            respondida = true;
            bool bien = indice == pregunta.Correcta;
            for (int i = 0; i < botones.Count; i++)
            {
                var b = botones[i];
                b.Enabled = false;
                b.Cursor = Cursors.Default;
                if (i == pregunta.Correcta)
                {
                    b.BackColor = Durazno;
                    b.ForeColor = Noche;
                }
                else if (i == indice)
                {
                    b.BackColor = Error;
                }
                // Botones deshabilitados: Windows los pinta en gris; los repintamos con sus colores.
                b.Paint += PintarBotonFijo;
                b.Invalidate();
            }
            lblResultado.Text = bien ? "✓  ¡Correcto!" : "✗  No era esa. La correcta: " + (pregunta.Correcta + 1) + ".";
            lblResultado.Visible = true;
            if (pregunta.Explicacion.Length > 0)
            {
                lblExplicacion.Text = pregunta.Explicacion;
                lblExplicacion.Visible = true;
            }
            btnSaltar.Visible = false;
            btnOtra.Visible = true;
            btnCerrar.Text = "Cerrar  (Enter)";
            btnOtra.Focus();
            Avisar(bien ? Resultado.Correcta : Resultado.Incorrecta);
        }

        void PintarBotonFijo(object sender, PaintEventArgs e)
        {
            var b = (Button)sender;
            using (var fondo = new SolidBrush(b.BackColor))
            using (var borde = new Pen(Durazno))
            {
                e.Graphics.FillRectangle(fondo, b.ClientRectangle);
                e.Graphics.DrawRectangle(borde, 0, 0, b.Width - 1, b.Height - 1);
            }
            var rect = new Rectangle(b.Padding.Left, 0, b.Width - b.Padding.Horizontal, b.Height);
            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, rect, b.ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        void Avisar(Resultado r)
        {
            var h = Respondio;
            Respondio = null;
            if (h != null) h(r);
        }

        Label Etiqueta(string texto, Font fuente, Color color, Padding margen)
        {
            return new Label
            {
                Text = texto,
                Font = fuente,
                ForeColor = color,
                BackColor = Noche,
                AutoSize = true,
                MaximumSize = new Size(Ancho, 0),
                Margin = margen,
                UseMnemonic = false,
            };
        }

        Button Boton(string texto, bool principal)
        {
            var b = new Button
            {
                Text = texto,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = principal ? Durazno : Noche,
                ForeColor = principal ? Noche : Durazno,
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(8, 0, 0, 0),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Durazno;
            return b;
        }
    }
}
