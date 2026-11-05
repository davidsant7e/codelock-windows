# Deja CodeLock arrancando con Windows (acceso directo en la carpeta Inicio) y lo abre ahora.
# Para quitarlo: Desinstalar.bat.

$ErrorActionPreference = "Stop"

$script = Join-Path $PSScriptRoot "CodeLock.ps1"
$acceso = Join-Path ([Environment]::GetFolderPath("Startup")) "CodeLock.lnk"
$argumentos = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$script`""

$shell = New-Object -ComObject WScript.Shell
$lnk = $shell.CreateShortcut($acceso)
$lnk.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$lnk.Arguments = $argumentos
$lnk.WorkingDirectory = $PSScriptRoot
$lnk.WindowStyle = 7
$lnk.Description = "CodeLock: preguntas de programación en Windows"
$lnk.IconLocation = (Join-Path $PSScriptRoot "assets\codelock.ico")
$lnk.Save()

Write-Host "Acceso directo creado en: $acceso"
Write-Host "CodeLock arrancará solo cada vez que inicies sesión."

& $script -Consola
