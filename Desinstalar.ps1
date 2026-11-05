# Cierra CodeLock y quita el arranque automático. Tu historial queda en %APPDATA%\CodeLock
# (bórralo a mano si quieres empezar de cero).

Get-Process -Name "CodeLock" -ErrorAction SilentlyContinue | Stop-Process -Force

$acceso = Join-Path ([Environment]::GetFolderPath("Startup")) "CodeLock.lnk"
if (Test-Path $acceso) {
    Remove-Item $acceso
    Write-Host "Listo: CodeLock ya no arranca con Windows."
} else {
    Write-Host "No estaba en el inicio de Windows."
}

$bin = Join-Path $env:LOCALAPPDATA "CodeLock"
if (Test-Path $bin) {
    Remove-Item $bin -Recurse -Force
    Write-Host "Programa compilado quitado de $bin."
}
