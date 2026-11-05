// Punto de entrada de CodeLock.exe (lo compila CodeLock.ps1 con el csc.exe que trae Windows).

using System;
using System.IO;
using System.Windows.Forms;

namespace CodeLock
{
    static class Program
    {
        /// <param name="args">args[0]: carpeta de preguntas (opcional).</param>
        [STAThread]
        static void Main(string[] args)
        {
            string carpeta = args.Length > 0 ? args[0] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preguntas");
            string datos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodeLock");
            try
            {
                App.Run(carpeta, datos);
            }
            catch (Exception e)
            {
                MessageBox.Show("CodeLock se cerró por un error:\n\n" + e, "CodeLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
