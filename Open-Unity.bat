@echo off
rem Opens the WinjayXR Unity project directly in Unity 6000.0.42f1.
rem Double-click this file. Unity Hub is not required for opening.
set UNITY_EXE=D:\Unity\6000.0.42f1\Editor\Unity.exe
if not exist "%UNITY_EXE%" (
  echo Unity editor not found at %UNITY_EXE%
  echo Check Docs\UNITY_SETUP.md
  pause
  exit /b 1
)
echo Opening Unity project:
echo   %~dp0Unity\WinjayXR
echo First open downloads packages - it can take about 10 minutes.
start "" "%UNITY_EXE%" -projectPath "%~dp0Unity\WinjayXR"
