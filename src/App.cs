// CodeLock para Windows: ícono junto al reloj, cuándo preguntar y guardar el historial.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodeLock
{
    public static class App
    {
        [DllImport("shell32.dll")]
        static extern int SHQueryUserNotificationState(out int estado);

        [StructLayout(LayoutKind.Sequential)]
        struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        static string carpetaPreguntas;
        static string rutaAjustes;
        static string rutaHistorial;
        static Ajustes ajustes;
        static Historial historial;
        static List<Pregunta> preguntas = new List<Pregunta>();
        static NotifyIcon icono;
        static Icon iconoVentana;
        static VentanaPregunta abierta;
        static bool bloqueada;
        static readonly Random azar = new Random();
        static System.Windows.Forms.Timer reloj;
        static System.Windows.Forms.Timer trasDesbloqueo;
        static ToolStripMenuItem[] opcionesFrecuencia;
        static ToolStripMenuItem itemDesbloquear, itemPausar, itemReanudar;

        static readonly int[] Frecuencias = { 15, 30, 60, 120, 0 };

        /// <param name="carpeta">Carpeta con los .json de preguntas.</param>
        /// <param name="carpetaDatos">Dónde guardar ajustes e historial (normalmente %APPDATA%\CodeLock).</param>
        public static void Run(string carpeta, string carpetaDatos)
        {
            bool primera;
            using (var unico = new Mutex(true, "CodeLockWindows_dseu1", out primera))
            {
                if (!primera)
                {
                    MessageBox.Show("CodeLock ya está abierto (ícono junto al reloj).", "CodeLock");
                    return;
                }
                Application.EnableVisualStyles();

                carpetaPreguntas = carpeta;
                rutaAjustes = Path.Combine(carpetaDatos, "ajustes.json");
                rutaHistorial = Path.Combine(carpetaDatos, "historial.json");
                ajustes = Almacen.Cargar<Ajustes>(rutaAjustes);
                historial = Almacen.Cargar<Historial>(rutaHistorial);
                var errores = Recargar();

                using (icono = new NotifyIcon())
                {
                    icono.Icon = CargarIcono(SystemInformation.SmallIconSize);
                    iconoVentana = CargarIcono(new Size(32, 32));
                    icono.ContextMenuStrip = CrearMenu();
                    icono.DoubleClick += delegate { Preguntar(true); };
                    icono.Visible = true;
                    ActualizarMenu();

                    if (errores.Count > 0)
                        Avisar(errores.Count + " problema(s) en las preguntas. El primero: " + errores[0], ToolTipIcon.Warning);
                    else
                        Avisar("Activo con " + preguntas.Count + " preguntas. " + Resumen() + " Doble clic en el ícono para una pregunta ya.", ToolTipIcon.Info);

                    SystemEvents.SessionSwitch += AlCambiarSesion;
                    reloj = new System.Windows.Forms.Timer { Interval = 30000 };
                    reloj.Tick += delegate { Revisar(); };
                    reloj.Start();
                    trasDesbloqueo = new System.Windows.Forms.Timer { Interval = 2500 };
                    trasDesbloqueo.Tick += delegate
                    {
                        trasDesbloqueo.Stop();
                        if (Frecuencia.TocaAlDesbloquear(ajustes, historial, Reloj.AhoraMs()) && !Ocupado()) Preguntar(false);
                    };

                    try
                    {
                        Application.Run();
                    }
                    finally
                    {
                        SystemEvents.SessionSwitch -= AlCambiarSesion;
                        reloj.Stop();
                        icono.Visible = false;
                    }
                }
            }
        }

        // ---------- cuándo preguntar ----------

        static void Revisar()
        {
            if (abierta != null || bloqueada) return;
            if (!Frecuencia.TocaPorTiempo(ajustes, historial, Reloj.AhoraMs())) return;
            if (SegundosInactivo() > 300) return;     // no hay nadie: se preguntará al volver
            if (Ocupado()) return;                    // pantalla completa o presentación: esperar
            Preguntar(false);
        }

        static void AlCambiarSesion(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock) bloqueada = true;
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                bloqueada = false;
                if (abierta == null) { trasDesbloqueo.Stop(); trasDesbloqueo.Start(); }
            }
        }

        /// <summary>true si hay un juego o video en pantalla completa, una presentación o "No molestar".</summary>
        static bool Ocupado()
        {
            int estado;
            try
            {
                if (SHQueryUserNotificationState(out estado) != 0) return false;
            }
            catch (Exception) { return false; }
            // 2 ocupado (pantalla completa), 3 Direct3D a pantalla completa, 4 presentación, 6 horas tranquilas
            return estado == 2 || estado == 3 || estado == 4 || estado == 6;
        }

        static double SegundosInactivo()
        {
            try
            {
                var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) };
                if (!GetLastInputInfo(ref info)) return 0;
                return unchecked((uint)Environment.TickCount - info.dwTime) / 1000.0;
            }
            catch (Exception) { return 0; }
        }

        // ---------- mostrar una pregunta ----------

        static void Preguntar(bool aPedido)
        {
            if (abierta != null)
            {
                abierta.Activate();
                return;
            }
            var ahora = Reloj.AhoraMs();
            var p = Selector.Elegir(preguntas, historial, ahora, azar);
            if (p == null)
            {
                Avisar("No hay preguntas. Agrega archivos .json en la carpeta de preguntas.", ToolTipIcon.Warning);
                return;
            }
            historial.Mostrada(p.Id, ahora);
            Guardar();

            var contador = historial.Aciertos + "/" + historial.Respondidas + " aciertos";
            abierta = new VentanaPregunta(p, contador);
            if (iconoVentana != null) abierta.Icon = iconoVentana;
            abierta.Respondio += delegate(Resultado r)
            {
                var t = Reloj.AhoraMs();
                if (r == Resultado.Saltada) historial.Saltada(p.Id, t);
                else if (r == Resultado.NoSe) historial.NoSabia(p.Id, t);
                else historial.Respondida(p.Id, r == Resultado.Correcta, t);
                Guardar();
            };
            abierta.FormClosed += delegate
            {
                bool otra = abierta.QuiereOtra;
                abierta.Dispose();
                abierta = null;
                if (otra) Preguntar(true);
            };
            abierta.Shown += delegate { abierta.Ubicar(); abierta.Activate(); };
            abierta.Show();
        }

        // ---------- menú ----------

        static ContextMenuStrip CrearMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Pregunta ahora", null, delegate { Preguntar(true); }).Font =
                new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(new ToolStripSeparator());

            var frecuencia = new ToolStripMenuItem("Frecuencia");
            opcionesFrecuencia = Frecuencias.Select(min =>
            {
                var item = new ToolStripMenuItem(NombreFrecuencia(min));
                item.Click += delegate
                {
                    ajustes.CadaMinutos = min;
                    Guardar();
                    ActualizarMenu();
                    Avisar(Resumen(), ToolTipIcon.Info);
                };
                return item;
            }).ToArray();
            frecuencia.DropDownItems.AddRange(opcionesFrecuencia);
            menu.Items.Add(frecuencia);

            itemDesbloquear = new ToolStripMenuItem("Preguntar al desbloquear Windows");
            itemDesbloquear.Click += delegate
            {
                ajustes.AlDesbloquear = !ajustes.AlDesbloquear;
                Guardar();
                ActualizarMenu();
            };
            menu.Items.Add(itemDesbloquear);

            itemPausar = new ToolStripMenuItem("Pausar 1 hora");
            itemPausar.Click += delegate
            {
                ajustes.PausaHasta = Reloj.AhoraMs() + 60 * 60000L;
                Guardar();
                ActualizarMenu();
                Avisar("En pausa hasta las " + DateTime.Now.AddHours(1).ToString("HH:mm") + ".", ToolTipIcon.Info);
            };
            itemReanudar = new ToolStripMenuItem("Reanudar");
            itemReanudar.Click += delegate
            {
                ajustes.PausaHasta = 0;
                Guardar();
                ActualizarMenu();
            };
            menu.Items.Add(itemPausar);
            menu.Items.Add(itemReanudar);
            menu.Items.Add(new ToolStripSeparator());

            menu.Items.Add("Estadísticas", null, delegate { MostrarEstadisticas(); });
            menu.Items.Add("Abrir carpeta de preguntas", null, delegate
            {
                try { Process.Start("explorer.exe", "\"" + carpetaPreguntas + "\""); } catch (Exception) { }
            });
            menu.Items.Add("Recargar preguntas", null, delegate
            {
                var errores = Recargar();
                Avisar(preguntas.Count + " preguntas cargadas." +
                       (errores.Count > 0 ? " " + errores.Count + " problema(s): " + errores[0] : ""),
                       errores.Count > 0 ? ToolTipIcon.Warning : ToolTipIcon.Info);
            });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Salir", null, delegate { Application.Exit(); });
            menu.Opening += delegate { ActualizarMenu(); };
            return menu;
        }

        static void ActualizarMenu()
        {
            for (int i = 0; i < Frecuencias.Length; i++) opcionesFrecuencia[i].Checked = ajustes.CadaMinutos == Frecuencias[i];
            itemDesbloquear.Checked = ajustes.AlDesbloquear;
            bool pausa = Frecuencia.EnPausa(ajustes, Reloj.AhoraMs());
            itemPausar.Visible = !pausa;
            itemReanudar.Visible = pausa;
            icono.Text = Recortar("CodeLock · " + (pausa ? "en pausa" : NombreFrecuencia(ajustes.CadaMinutos).ToLowerInvariant()), 63);
        }

        static string NombreFrecuencia(int minutos)
        {
            switch (minutos)
            {
                case 0: return "Solo al desbloquear (o a pedido)";
                case 60: return "Cada hora";
                case 120: return "Cada 2 horas";
                default: return "Cada " + minutos + " minutos";
            }
        }

        static string Resumen()
        {
            var partes = new List<string>();
            if (ajustes.CadaMinutos > 0) partes.Add(NombreFrecuencia(ajustes.CadaMinutos).ToLowerInvariant());
            if (ajustes.AlDesbloquear) partes.Add("al desbloquear Windows");
            if (partes.Count == 0) return "Solo pregunta cuando lo pidas.";
            return "Pregunta " + string.Join(" y ", partes.ToArray()) + ".";
        }

        static void MostrarEstadisticas()
        {
            var h = historial;
            int porcentaje = h.Respondidas > 0 ? (int)Math.Round(100.0 * h.Aciertos / h.Respondidas) : 0;
            var porTema = preguntas.GroupBy(p => p.Categoria).Select(g =>
            {
                int veces = 0, fallos = 0;
                foreach (var p in g)
                {
                    var e = h.Preguntas.FirstOrDefault(x => x.Id == p.Id);
                    if (e != null) { veces += e.Veces; fallos += e.Fallos; }
                }
                return "  " + g.Key + ": " + (veces - fallos) + "/" + veces + " aciertos";
            });
            MessageBox.Show(
                "Respondidas: " + h.Respondidas + "\nAciertos: " + h.Aciertos + " (" + porcentaje + " %)\nNo sé: " + h.NoSe + "\nSaltadas: " + h.Saltadas +
                "\n\nPor tema:\n" + string.Join("\n", porTema.ToArray()) +
                "\n\n" + preguntas.Count + " preguntas en total.",
                "CodeLock · Estadísticas", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- utilidades ----------

        static List<string> Recargar()
        {
            var errores = new List<string>();
            preguntas = Banco.CargarCarpeta(carpetaPreguntas, errores);
            return errores;
        }

        static void Guardar()
        {
            try
            {
                Almacen.Guardar(rutaAjustes, ajustes);
                Almacen.Guardar(rutaHistorial, historial);
            }
            catch (Exception e)
            {
                Avisar("No se pudo guardar el historial: " + e.Message, ToolTipIcon.Error);
            }
        }

        static void Avisar(string texto, ToolTipIcon tipo)
        {
            if (icono != null) icono.ShowBalloonTip(5000, "CodeLock", texto, tipo);
        }

        static string Recortar(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max);
        }

        /// <summary>El ícono del arcano V · El Hierofante (codelock.ico junto al .exe); si falta, uno dibujado.</summary>
        static Icon CargarIcono(Size tamano)
        {
            try
            {
                var ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "codelock.ico");
                if (File.Exists(ruta)) return new Icon(ruta, tamano);
            }
            catch (Exception) { }
            return DibujarIcono();
        }

        /// <summary>Ícono de respaldo 32×32: dos llaves cruzadas sobre azul noche.</summary>
        static Icon DibujarIcono()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            using (var fondo = new SolidBrush(VentanaPregunta.Noche))
            using (var trazo = new Pen(VentanaPregunta.Durazno, 3f))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var ruta = new GraphicsPath())
                {
                    ruta.AddArc(0, 0, 10, 10, 180, 90);
                    ruta.AddArc(21, 0, 10, 10, 270, 90);
                    ruta.AddArc(21, 21, 10, 10, 0, 90);
                    ruta.AddArc(0, 21, 10, 10, 90, 90);
                    ruta.CloseFigure();
                    g.FillPath(fondo, ruta);
                }
                // llave 1 (de arriba izquierda a abajo derecha)
                g.DrawEllipse(trazo, 5, 5, 8, 8);
                g.DrawLine(trazo, 12, 12, 25, 25);
                g.DrawLine(trazo, 21, 21, 24, 18);
                // llave 2 (de arriba derecha a abajo izquierda)
                g.DrawEllipse(trazo, 19, 5, 8, 8);
                g.DrawLine(trazo, 20, 12, 7, 25);
                g.DrawLine(trazo, 11, 21, 8, 18);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }
    }
}
