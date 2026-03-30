@echo off
setlocal
powershell -ExecutionPolicy Bypass -File "%~dp0package.ps1" %*
