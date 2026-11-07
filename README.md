# CodeLock para Windows

<p align="center"><img src="docs/carta.svg" alt="V · El Hierofante (rama Windows)" width="180"></p>

App hermana de [CodeLock para Android](https://github.com/davidsant7e/codelock-android): cada cierto tiempo, y al desbloquear Windows, aparece una pregunta de programación. Usa el **mismo banco de preguntas** y las **mismas reglas** que la app del celular.

## Qué hace

- **Pregunta cada 30 minutos** (se cambia a 15 min, 1 h, 2 h o "solo al desbloquear").
- **Pregunta al desbloquear Windows** (Win+L y volver), si pasaron al menos 5 minutos desde la última.
- **No molesta**: no pregunta si estás en pantalla completa (juego, video, presentación), con "No molestar" activo, con la sesión bloqueada o si llevas más de 5 minutos sin tocar el PC.
- **Elige bien la pregunta**: primero las que nunca viste, repite más las que fallaste, no repite las últimas 10.
- **Responde con el teclado**: `1`–`6` (o `A`–`F`) para elegir, `0` para **No sé**, `Esc` para cerrar o saltar, `Enter` para cerrar, `N` para otra, `D` para ver la documentación.
- **No sé**: muestra la respuesta y la explicación sin bajar tu porcentaje de aciertos; la pregunta vuelve pronto, como una fallada.
- **De dónde sale**: después de responder, la fuente de la pregunta (y de qué archivo `.json` salió) con el botón **Ver documentación**, que la abre en tu navegador. Si la pregunta no trae enlace, el botón es **Buscar en la web**.
- Ícono del arcano **V · El Hierofante** (dos llaves cruzadas) junto al reloj, con menú: *Pregunta ahora* (o doble clic), *Frecuencia*, *Preguntar al desbloquear*, *Pausar 1 hora*, *Estadísticas*, *Abrir carpeta de preguntas*, *Recargar preguntas*, *Salir*.

No instala nada: lo compila el compilador de C# que ya trae Windows (.NET Framework 4) a un `.exe` pequeño en `%LOCALAPPDATA%\CodeLock`. Queda abierto solo ese `.exe` (unos pocos MB de RAM), no PowerShell.

## Instalar

1. Descomprime en `C:\Git\codelock-windows`.
2. Doble clic en **`Probar.bat`**: compila, abre CodeLock y muestra si hubo errores. Doble clic en el ícono de las llaves para ver una pregunta.
3. Si todo va bien, doble clic en **`Instalar.bat`**: desde ahí arranca solo al iniciar sesión.

Para quitarlo: **`Desinstalar.bat`** (cierra el programa y quita el arranque automático; tu historial se queda).

## Tus preguntas

Están en la carpeta `preguntas\`. Cada archivo `.json` es un banco en el **mismo formato que la app Android**, así que puedes copiar el mismo archivo a los dos:

```json
{
  "version": 1,
  "categories": [
    { "name": "Kotlin",
      "questions": [
        { "statement": "¿Qué palabra declara una variable que no se puede reasignar?",
          "options": ["var", "val", "let", "const"],
          "correct": 1,
          "explanation": "val es de solo lectura; var se puede reasignar.",
          "difficulty": 1,
          "source": "Documentación de Kotlin · Variables",
          "sourceUrl": "https://kotlinlang.org/docs/basic-syntax.html#variables" }
      ] }
  ]
}
```

- `correct` es la posición de la opción correcta, empezando en 0.
- Entre 2 y 6 opciones.
- `source` y `sourceUrl` son opcionales: de dónde sale la pregunta y el enlace a la documentación.
- Agrega archivos nuevos (por ejemplo `ingles.json`) y usa *Recargar preguntas* en el menú. Las preguntas repetidas se ignoran.

### Banco «Programación desde cero»

`preguntas\programacion-desde-cero.json`: 120 preguntas del libro *Programación desde cero*
(5 por cada uno de los 24 capítulos), en tres temas: **Nivel 1 Fundamentos**, **Nivel 2
Desarrollo práctico** y **Nivel 3 Profundización**. «De dónde sale» indica el capítulo
(«Programación desde cero · Cap. 13 Persistencia y SQL seguro») y, cuando aplica, enlaza la
documentación oficial de Python o Git. Es el mismo archivo que trae CodeLock para Android.

## Dónde guarda las cosas

| Qué | Dónde |
|---|---|
| Ajustes (frecuencia, pausa) | `%APPDATA%\CodeLock\ajustes.json` |
| Historial (aciertos, fallos, recientes) | `%APPDATA%\CodeLock\historial.json` |
| Programa compilado | `%LOCALAPPDATA%\CodeLock\CodeLock.exe` |

Si cambias el código en `src\`, la próxima vez que abras CodeLock se vuelve a compilar solo.

## Código

| Archivo | Qué hace |
|---|---|
| `src/Core.cs` | Banco de preguntas (lectura y validación), selección con pesos, frecuencia, historial. Sin interfaz. |
| `src/Ventana.cs` | La ventana de la pregunta (duotono azul noche + durazno). |
| `src/App.cs` | Ícono y menú, temporizador, aviso de desbloqueo, "no molestar", guardado. |
| `src/Program.cs` | Punto de entrada del `.exe`. |
| `tests/CoreTests.cs` | Pruebas del núcleo (233 comprobaciones). |
| `CodeLock.ps1` | Compila si hace falta y abre el programa. |
| `assets/codelock.ico` | Ícono del arcano V · El Hierofante (programa, bandeja, ventana y acceso directo). |

Pruebas en Windows (desde la carpeta del proyecto, en PowerShell):

```powershell
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /out:$env:TEMP\CoreTests.exe /r:System.Runtime.Serialization.dll /r:System.Core.dll src\Core.cs tests\CoreTests.cs
& $env:TEMP\CoreTests.exe preguntas
```

## Estado

Primera versión (4 nov 2026). Probada en Windows 11 (5 nov 2026): compila, pregunta por tiempo y al desbloquear, guarda el historial y arranca con Windows. En Linux con Mono: compila como C# 5 y las 247 pruebas del núcleo pasan (incluye el banco del libro).
