@echo off
rem Compila (si hace falta) y abre CodeLock mostrando los mensajes, para ver errores.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0CodeLock.ps1" -Consola
pause
