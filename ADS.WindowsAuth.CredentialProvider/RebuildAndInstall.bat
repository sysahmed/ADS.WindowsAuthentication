@echo off
REM Batch файл за автоматичен rebuild и install на Credential Provider DLL
REM Използване: RebuildAndInstall.bat

powershell.exe -ExecutionPolicy Bypass -File "%~dp0RebuildAndInstall.ps1" %*

