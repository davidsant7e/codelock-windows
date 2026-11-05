// Pruebas del núcleo de CodeLock para Windows (sin interfaz).
// En Windows:  ver README (sección "Pruebas").  En Linux: mcs + mono.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeLock;

static class CoreTests
{
    static int ok, fallos;

    static void Check(bool cond, string nombre)
    {
        if (cond) { ok++; }
        else { fallos++; Console.WriteLine("FALLA: " + nombre); }
    }

    static string Banco1()
    {
        return "{\"version\":1,\"categories\":[{\"name\":\"Kotlin\",\"questions\":[" +
               "{\"statement\":\"¿Qué es val?\",\"options\":[\"Inmutable\",\"Mutable\"],\"correct\":0,\"explanation\":\"No se reasigna.\"}," +
               "{\"statement\":\"Sin opciones\",\"options\":[\"a\"],\"correct\":0}," +
               "{\"statement\":\"Índice malo\",\"options\":[\"a\",\"b\"],\"correct\":5}," +
               "{\"statement\":\"\",\"options\":[\"a\",\"b\"],\"correct\":0}" +
               "]},{\"name\":\"\",\"questions\":[]}]}";
    }

    static int Main(string[] args)
    {
        // --- carga y validación ---
        var errores = new List<string>();
        var ps = Banco.CargarTexto(Banco1(), "prueba.json", errores);
        Check(ps.Count == 1, "solo una pregunta válida");
        Check(errores.Count == 4, "cuatro errores (2 opciones, índice, enunciado, tema sin nombre): " + errores.Count);
        Check(ps[0].Categoria == "Kotlin" && ps[0].Opciones.Count == 2 && ps[0].Correcta == 0, "campos leídos");
        Check(ps[0].Id == Banco.Huella("Kotlin", "¿Qué es val?"), "huella estable");
        Check(Banco.Huella("kotlin", "x") == Banco.Huella("KOTLIN", "x"), "huella ignora mayúsculas del tema");

        errores.Clear();
        Banco.CargarTexto("{no es json", "roto.json", errores);
        Check(errores.Count == 1 && errores[0].Contains("no es un JSON válido"), "JSON inválido informado");

        errores.Clear();
        Banco.CargarTexto("﻿{\"version\":1}", "sin.json", errores);
        Check(errores.Count == 1 && errores[0].Contains("categories"), "falta categories (con BOM)");

        // --- banco real copiado de la app Android ---
        string carpeta = args.Length > 0 ? args[0] : "preguntas";
        errores.Clear();
        var reales = Banco.CargarCarpeta(carpeta, errores);
        Check(reales.Count == 32, "el banco real tiene 32 preguntas: " + reales.Count);
        Check(errores.Count == 0, "el banco real no tiene errores: " + string.Join(" | ", errores));
        Check(reales.Select(p => p.Categoria).Distinct().Count() == 6, "6 temas");

        // duplicados entre archivos se ignoran
        var tmp = Path.Combine(Path.GetTempPath(), "codelock-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        File.WriteAllText(Path.Combine(tmp, "a.json"), Banco1());
        File.WriteAllText(Path.Combine(tmp, "b.json"), Banco1());
        errores.Clear();
        Check(Banco.CargarCarpeta(tmp, errores).Count == 1, "duplicados entre archivos se ignoran");

        // --- pesos (mismas reglas que Android) ---
        long ahora = 1000L * 60 * 60 * 24 * 365 * 50;
        long dia = 24L * 60 * 60 * 1000;
        Check(Selector.Peso(null, ahora) == 4.0, "nunca preguntada pesa 4");
        Check(Math.Abs(Selector.Peso(new EstadoPregunta { Veces = 2, Fallos = 0, Ultima = ahora }, ahora) - 1.0) < 1e-9, "acertada hoy pesa 1");
        Check(Math.Abs(Selector.Peso(new EstadoPregunta { Veces = 2, Fallos = 2, Ultima = ahora }, ahora) - 5.0) < 1e-9, "fallada hoy pesa 5");
        Check(Math.Abs(Selector.Peso(new EstadoPregunta { Veces = 1, Fallos = 0, Ultima = ahora - 30 * dia }, ahora) - 3.0) < 1e-9, "acertada hace un mes pesa 3");

        // --- selección ---
        var h = new Historial();
        var azar = new Random(1);
        var conteo = new Dictionary<string, int>();
        foreach (var p in reales) conteo[p.Id] = 0;
        for (int i = 0; i < 200; i++)
        {
            var p = Selector.Elegir(reales, h, ahora, azar);
            Check(!h.Recientes.Take(Selector.VentanaRecientes).Contains(p.Id), "no repite recientes");
            h.Mostrada(p.Id, ahora);
            h.Respondida(p.Id, true, ahora);
            conteo[p.Id]++;
        }
        Check(conteo.Values.All(c => c > 0), "en 200 rondas salen todas las preguntas");
        Check(h.Recientes.Count == Selector.VentanaRecientes, "recientes limitado a 10");

        // fallada sale más que acertada
        var dos = reales.Take(2).ToList();
        var h2 = new Historial();
        h2.Respondida(dos[0].Id, false, ahora);
        h2.Respondida(dos[1].Id, true, ahora);
        int fallada = 0;
        for (int i = 0; i < 2000; i++) if (Selector.Elegir(dos, h2, ahora, azar).Id == dos[0].Id) fallada++;
        Check(fallada > 1500, "la fallada sale ~5 de cada 6 veces: " + fallada);

        // con una sola pregunta y reciente, igual devuelve algo
        var una = reales.Take(1).ToList();
        var h3 = new Historial();
        h3.Mostrada(una[0].Id, ahora);
        Check(Selector.Elegir(una, h3, ahora, azar) != null, "con una sola pregunta no se queda sin nada");
        Check(Selector.Elegir(new List<Pregunta>(), h3, ahora, azar) == null, "sin preguntas devuelve null");

        // --- frecuencia ---
        var a = new Ajustes();
        var hf = new Historial();
        Check(Frecuencia.TocaPorTiempo(a, hf, ahora), "primera vez toca");
        hf.UltimaMostrada = ahora - 29 * 60000L;
        Check(!Frecuencia.TocaPorTiempo(a, hf, ahora), "a los 29 min no toca");
        hf.UltimaMostrada = ahora - 30 * 60000L;
        Check(Frecuencia.TocaPorTiempo(a, hf, ahora), "a los 30 min toca");
        a.CadaMinutos = 0;
        Check(!Frecuencia.TocaPorTiempo(a, hf, ahora), "0 = nunca por tiempo");
        hf.UltimaMostrada = ahora - 4 * 60000L;
        Check(!Frecuencia.TocaAlDesbloquear(a, hf, ahora), "desbloqueo a los 4 min no toca");
        hf.UltimaMostrada = ahora - 5 * 60000L;
        Check(Frecuencia.TocaAlDesbloquear(a, hf, ahora), "desbloqueo a los 5 min toca");
        a.PausaHasta = ahora + 1;
        Check(!Frecuencia.TocaAlDesbloquear(a, hf, ahora) && Frecuencia.EnPausa(a, ahora), "en pausa no toca");
        a.PausaHasta = ahora;
        Check(!Frecuencia.EnPausa(a, ahora), "la pausa termina a la hora indicada");

        // --- historial y almacén ---
        var ruta = Path.Combine(tmp, "sub", "historial.json");
        h2.Mostrada(dos[0].Id, ahora);
        h2.Saltada(dos[1].Id, ahora);
        Almacen.Guardar(ruta, h2);
        var leido = Almacen.Cargar<Historial>(ruta);
        Check(leido.Respondidas == 2 && leido.Aciertos == 1 && leido.Saltadas == 1, "contadores guardados");
        Check(leido.Estado(dos[0].Id).Fallos == 1 && leido.UltimaMostrada == ahora, "estado por pregunta guardado");
        Check(leido.Recientes.Count == 1 && leido.Recientes[0] == dos[0].Id, "recientes guardados");

        File.WriteAllText(ruta, "{dañado");
        var nuevo = Almacen.Cargar<Historial>(ruta);
        Check(nuevo.Respondidas == 0 && File.Exists(ruta + ".dañado"), "archivo dañado: empieza de cero y guarda copia");
        Check(Almacen.Cargar<Ajustes>(Path.Combine(tmp, "no-existe.json")).CadaMinutos == 30, "ajustes por defecto: cada 30 min");

        Directory.Delete(tmp, true);
        Console.WriteLine("ok=" + ok + " fallos=" + fallos);
        return fallos == 0 ? 0 : 1;
    }
}
