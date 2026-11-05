// CodeLock para Windows — núcleo sin interfaz: preguntas, selección, frecuencia e historial.
// Escrito en C# 5 (el compilador que trae Windows PowerShell 5.1). Se prueba con tests/CoreTests.cs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace CodeLock
{
    // ---------- Formato JSON del banco (el mismo de la app Android) ----------

    [DataContract]
    public class BancoJson
    {
        [DataMember(Name = "version")] public int Version;
        [DataMember(Name = "categories")] public List<CategoriaJson> Categorias;
    }

    [DataContract]
    public class CategoriaJson
    {
        [DataMember(Name = "name")] public string Nombre;
        [DataMember(Name = "questions")] public List<PreguntaJson> Preguntas;
    }

    [DataContract]
    public class PreguntaJson
    {
        [DataMember(Name = "statement")] public string Enunciado;
        [DataMember(Name = "options")] public List<string> Opciones;
        [DataMember(Name = "correct")] public int Correcta = -1;
        [DataMember(Name = "explanation")] public string Explicacion;
        [DataMember(Name = "difficulty")] public int Dificultad;
        [DataMember(Name = "source")] public string Fuente;
        [DataMember(Name = "sourceUrl")] public string FuenteUrl;
    }

    /// <summary>Una pregunta lista para mostrar.</summary>
    public class Pregunta
    {
        public string Id;          // huella estable de tema + enunciado
        public string Categoria;
        public string Enunciado;
        public List<string> Opciones;
        public int Correcta;
        public string Explicacion;
        public string Fuente;      // de dónde sale: "Documentación de Kotlin · Data classes" (puede ser vacío)
        public string FuenteUrl;   // enlace https:// a la documentación, o null
        public string Archivo;     // archivo .json de donde se cargó
    }

    /// <summary>El enlace para "Ver documentación": el de la pregunta, o una búsqueda si no tiene (igual que en Android).</summary>
    public static class Enlaces
    {
        public static bool EsWeb(string url)
        {
            return url != null && (url.StartsWith("https://") || url.StartsWith("http://")) && url.Length > 10 && url.IndexOf(' ') < 0;
        }

        public static string Limpiar(string url)
        {
            if (url == null) return null;
            url = url.Trim();
            return EsWeb(url) ? url : null;
        }

        public static string Para(Pregunta p)
        {
            var url = Limpiar(p.FuenteUrl);
            if (url != null) return url;
            return "https://www.google.com/search?q=" + Uri.EscapeDataString(p.Categoria + " " + p.Enunciado).Replace("%20", "+");
        }
    }

    /// <summary>Carga y valida los archivos .json de la carpeta de preguntas.</summary>
    public static class Banco
    {
        public static List<Pregunta> CargarCarpeta(string carpeta, List<string> errores)
        {
            var resultado = new List<Pregunta>();
            var vistos = new HashSet<string>();
            if (!Directory.Exists(carpeta))
            {
                errores.Add("No existe la carpeta de preguntas: " + carpeta);
                return resultado;
            }
            foreach (var archivo in Directory.GetFiles(carpeta, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var p in CargarTexto(File.ReadAllText(archivo, Encoding.UTF8), Path.GetFileName(archivo), errores))
                {
                    if (vistos.Add(p.Id)) resultado.Add(p);
                }
            }
            return resultado;
        }

        public static List<Pregunta> CargarTexto(string json, string origen, List<string> errores)
        {
            var lista = new List<Pregunta>();
            BancoJson banco;
            try
            {
                banco = Json.Leer<BancoJson>(json);
            }
            catch (Exception e)
            {
                errores.Add(origen + ": no es un JSON válido (" + e.Message + ")");
                return lista;
            }
            if (banco == null || banco.Categorias == null)
            {
                errores.Add(origen + ": falta la lista \"categories\".");
                return lista;
            }
            for (int i = 0; i < banco.Categorias.Count; i++)
            {
                var cat = banco.Categorias[i];
                if (cat == null) continue;
                var nombre = (cat.Nombre ?? "").Trim();
                if (nombre.Length == 0)
                {
                    errores.Add(origen + ", tema " + (i + 1) + ": falta el nombre.");
                    continue;
                }
                if (cat.Preguntas == null) continue;
                for (int j = 0; j < cat.Preguntas.Count; j++)
                {
                    var q = cat.Preguntas[j];
                    if (q == null) continue;
                    var enunciado = (q.Enunciado ?? "").Trim();
                    var opciones = (q.Opciones ?? new List<string>())
                        .Select(o => (o ?? "").Trim()).Where(o => o.Length > 0).ToList();
                    string problema = null;
                    if (enunciado.Length == 0) problema = "falta el enunciado";
                    else if (opciones.Count < 2 || opciones.Count > 6) problema = "debe tener entre 2 y 6 opciones";
                    else if (q.Correcta < 0 || q.Correcta >= opciones.Count) problema = "\"correct\" debe ser la posición (desde 0) de la opción correcta";
                    if (problema != null)
                    {
                        errores.Add(origen + ", " + nombre + ", pregunta " + (j + 1) + ": " + problema + ".");
                        continue;
                    }
                    lista.Add(new Pregunta
                    {
                        Id = Huella(nombre, enunciado),
                        Categoria = nombre,
                        Enunciado = enunciado,
                        Opciones = opciones,
                        Correcta = q.Correcta,
                        Explicacion = (q.Explicacion ?? "").Trim(),
                        Fuente = (q.Fuente ?? "").Trim(),
                        FuenteUrl = Enlaces.Limpiar(q.FuenteUrl),
                        Archivo = origen,
                    });
                }
            }
            return lista;
        }

        public static string Huella(string categoria, string enunciado)
        {
            using (var sha = SHA1.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(categoria.ToLowerInvariant() + "\n" + enunciado));
                return BitConverter.ToString(bytes, 0, 8).Replace("-", "").ToLowerInvariant();
            }
        }
    }

    // ---------- Historial y ajustes (se guardan en %APPDATA%\CodeLock) ----------

    [DataContract]
    public class EstadoPregunta
    {
        [DataMember(Name = "id")] public string Id;
        [DataMember(Name = "veces")] public int Veces;
        [DataMember(Name = "fallos")] public int Fallos;
        [DataMember(Name = "ultima")] public long Ultima;   // ms Unix; 0 = nunca
    }

    [DataContract]
    public class Historial
    {
        [DataMember(Name = "preguntas")] public List<EstadoPregunta> Preguntas = new List<EstadoPregunta>();
        [DataMember(Name = "recientes")] public List<string> Recientes = new List<string>();
        [DataMember(Name = "ultimaMostrada")] public long UltimaMostrada;
        [DataMember(Name = "respondidas")] public int Respondidas;
        [DataMember(Name = "aciertos")] public int Aciertos;
        [DataMember(Name = "saltadas")] public int Saltadas;
        [DataMember(Name = "noSe")] public int NoSe;

        public EstadoPregunta Estado(string id)
        {
            if (Preguntas == null) Preguntas = new List<EstadoPregunta>();
            var e = Preguntas.FirstOrDefault(x => x.Id == id);
            if (e == null)
            {
                e = new EstadoPregunta { Id = id };
                Preguntas.Add(e);
            }
            return e;
        }

        /// <summary>Registra que se mostró una pregunta (antes de responder).</summary>
        public void Mostrada(string id, long ahora)
        {
            UltimaMostrada = ahora;
            if (Recientes == null) Recientes = new List<string>();
            Recientes.Remove(id);
            Recientes.Insert(0, id);
            while (Recientes.Count > Selector.VentanaRecientes) Recientes.RemoveAt(Recientes.Count - 1);
        }

        public void Respondida(string id, bool correcta, long ahora)
        {
            var e = Estado(id);
            e.Veces++;
            if (!correcta) e.Fallos++;
            e.Ultima = ahora;
            Respondidas++;
            if (correcta) Aciertos++;
        }

        /// <summary>"No sé": vuelve pronto como una fallada, pero no cuenta en el porcentaje de aciertos.</summary>
        public void NoSabia(string id, long ahora)
        {
            var e = Estado(id);
            e.Veces++;
            e.Fallos++;
            e.Ultima = ahora;
            NoSe++;
        }

        public void Saltada(string id, long ahora)
        {
            var e = Estado(id);
            e.Ultima = ahora;
            Saltadas++;
        }
    }

    [DataContract]
    public class Ajustes
    {
        /// <summary>Minutos entre preguntas; 0 = solo al desbloquear o a pedido.</summary>
        [DataMember(Name = "cadaMinutos")] public int CadaMinutos = 30;
        [DataMember(Name = "alDesbloquear")] public bool AlDesbloquear = true;
        /// <summary>Al desbloquear, solo pregunta si pasaron al menos estos minutos desde la última.</summary>
        [DataMember(Name = "minutosEntreDesbloqueos")] public int MinutosEntreDesbloqueos = 5;
        /// <summary>Pausa hasta este momento (ms Unix); 0 = sin pausa.</summary>
        [DataMember(Name = "pausaHasta")] public long PausaHasta;
    }

    // ---------- Reglas (puras, iguales a las de la app Android) ----------

    public static class Selector
    {
        public const int VentanaRecientes = 10;
        const double DiaMs = 24 * 60 * 60 * 1000.0;

        /// <summary>Peso de una pregunta: nuevas 4; las falladas y las que no ves hace días pesan más.</summary>
        public static double Peso(EstadoPregunta e, long ahora)
        {
            if (e == null || e.Veces == 0) return 4.0;
            double tasaError = (double)e.Fallos / e.Veces;
            double dias = e.Ultima > 0 ? Math.Max(0, ahora - e.Ultima) / DiaMs : 7.0;
            return 1.0 + tasaError * 4.0 + Math.Min(dias, 7.0) / 7.0 * 2.0;
        }

        public static Pregunta Elegir(IList<Pregunta> preguntas, Historial h, long ahora, Random azar)
        {
            if (preguntas == null || preguntas.Count == 0) return null;
            var recientes = new HashSet<string>(h.Recientes ?? new List<string>());
            var grupo = preguntas.Where(p => !recientes.Contains(p.Id)).ToList();
            if (grupo.Count == 0)
            {
                var ultima = (h.Recientes != null && h.Recientes.Count > 0) ? h.Recientes[0] : null;
                grupo = preguntas.Where(p => p.Id != ultima).ToList();
                if (grupo.Count == 0) grupo = preguntas.ToList();
            }
            var pesos = grupo.Select(p => Peso((h.Preguntas ?? new List<EstadoPregunta>()).FirstOrDefault(x => x.Id == p.Id), ahora)).ToList();
            double objetivo = azar.NextDouble() * pesos.Sum();
            for (int i = 0; i < grupo.Count; i++)
            {
                objetivo -= pesos[i];
                if (objetivo <= 0) return grupo[i];
            }
            return grupo[grupo.Count - 1];
        }
    }

    public static class Frecuencia
    {
        public static bool EnPausa(Ajustes a, long ahora)
        {
            return a.PausaHasta > ahora;
        }

        /// <summary>¿Toca pregunta por tiempo?</summary>
        public static bool TocaPorTiempo(Ajustes a, Historial h, long ahora)
        {
            if (EnPausa(a, ahora) || a.CadaMinutos <= 0) return false;
            if (h.UltimaMostrada <= 0) return true;
            return ahora - h.UltimaMostrada >= a.CadaMinutos * 60000L;
        }

        /// <summary>¿Toca pregunta al desbloquear Windows?</summary>
        public static bool TocaAlDesbloquear(Ajustes a, Historial h, long ahora)
        {
            if (EnPausa(a, ahora) || !a.AlDesbloquear) return false;
            if (h.UltimaMostrada <= 0) return true;
            return ahora - h.UltimaMostrada >= Math.Max(0, a.MinutosEntreDesbloqueos) * 60000L;
        }
    }

    // ---------- Utilidades ----------

    public static class Reloj
    {
        static readonly DateTime Epoca = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static long AhoraMs() { return (long)(DateTime.UtcNow - Epoca).TotalMilliseconds; }
    }

    public static class Json
    {
        public static T Leer<T>(string texto)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            if (texto.Length > 0 && texto[0] == '﻿') texto = texto.Substring(1);
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(texto)))
            {
                return (T)ser.ReadObject(ms);
            }
        }

        public static string Escribir<T>(T obj)
        {
            var ser = new DataContractJsonSerializer(typeof(T));
            using (var ms = new MemoryStream())
            {
                ser.WriteObject(ms, obj);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }

    /// <summary>Lee y guarda archivos JSON. Si un archivo falta o está dañado, devuelve uno nuevo.</summary>
    public static class Almacen
    {
        public static T Cargar<T>(string ruta) where T : new()
        {
            try
            {
                if (File.Exists(ruta))
                {
                    var obj = Json.Leer<T>(File.ReadAllText(ruta, Encoding.UTF8));
                    if (obj != null) return obj;
                }
            }
            catch (Exception)
            {
                try { File.Copy(ruta, ruta + ".dañado", true); } catch (Exception) { }
            }
            return new T();
        }

        /// <summary>Escribe primero en un temporal y luego lo reemplaza, para no dejar archivos a medias.</summary>
        public static void Guardar<T>(string ruta, T obj)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta));
            var tmp = ruta + ".tmp";
            File.WriteAllText(tmp, Json.Escribir(obj), new UTF8Encoding(false));
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Move(tmp, ruta);
        }
    }
}
