@echo off
rem Сборка РусификаторZCode.exe (компилятор .NET Framework входит в Windows)
cd /d "%~dp0"
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe -nologo -target:winexe -out:"РусификаторZCode.exe" InstallRu.cs -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll
if errorlevel 1 (
  echo.
  echo Сборка не удалась.
  pause
  exit /b 1
)
echo.
echo Готово: РусификаторZCode.exe
pause
