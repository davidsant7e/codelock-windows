# CodeLock para Windows: cada cierto tiempo (y al desbloquear Windows) aparece una pregunta.
#
# Este script compila el programa la primera vez (o cuando cambia el código) con el
# compilador de C# que ya trae Windows, y lo abre. No hay que instalar nada.
#
#   -Consola   muestra los mensajes de compilación (lo usa Probar.bat)

param([switch]$Consola)

$ErrorActionPreference = "Stop"

$fuentes = @("Core.cs", "Ventana.cs", "App.cs", "Program.cs") | ForEach-Object { Join-Path $PSScriptRoot "src\$_" }
$preguntas = Join-Path $PSScriptRoot "preguntas"
$bin = Join-Path $env:LOCALAPPDATA "CodeLock"
$exe = Join-Path $bin "CodeLock.exe"

function Escribir($texto) { if ($Consola) { Write-Host $texto } }

# ¿Hay que compilar? (no existe el .exe o algún archivo .cs es más nuevo)
$compilar = -not (Test-Path $exe)
if (-not $compilar) {
    $fechaExe = (Get-Item $exe).LastWriteTime
    $compilar = @($fuentes | Where-Object { (Get-Item $_).LastWriteTime -gt $fechaExe }).Count -gt 0
}

if ($compilar) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe" }
    if (-not (Test-Path $csc)) { throw "No se encontró el compilador de C# de .NET Framework 4 ($csc)." }

    # Si había un CodeLock abierto, se cierra para poder reemplazar el .exe.
    Get-Process -Name "CodeLock" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500

    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    Escribir "Compilando CodeLock..."
    $salida = & $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$exe" `
        /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Runtime.Serialization.dll `
        $fuentes 2>&1
    if ($LASTEXITCODE -ne 0) {
        $texto = ($salida | Out-String)
        if ($Consola) { Write-Host $texto -ForegroundColor Red }
        else {
            Add-Type -AssemblyName System.Windows.Forms
            [System.Windows.Forms.MessageBox]::Show("No se pudo compilar CodeLock:`n`n$texto", "CodeLock") | Out-Null
        }
        exit 1
    }
    Escribir "Listo: $exe"
}

if (Get-Process -Name "CodeLock" -ErrorAction SilentlyContinue) {
    Escribir "CodeLock ya está abierto (ícono junto al reloj)."
    exit 0
}

Start-Process -FilePath $exe -ArgumentList "`"$preguntas`""
Escribir "CodeLock abierto: busca el ícono de las llaves junto al reloj (puede estar en la flecha ^)."
