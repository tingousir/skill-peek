@echo off
setlocal
if not exist "%~dp0dist\SkillPeek.exe" (
  echo First run: building ...
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
  )
)
start "" "%~dp0dist\SkillPeek.exe"
