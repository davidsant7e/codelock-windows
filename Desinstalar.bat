@echo off
rem Doble clic: cierra CodeLock y deja de arrancar con Windows.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Desinstalar.ps1"
pause
