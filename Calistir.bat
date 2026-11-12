@echo off
chcp 65001 >nul
cd /d "%~dp0"
if exist "LibraryManagement.Desktop\bin\Debug\net8.0-windows\LibraryManagement.Desktop.exe" (
    start "" "LibraryManagement.Desktop\bin\Debug\net8.0-windows\LibraryManagement.Desktop.exe"
) else (
    dotnet run --project "LibraryManagement.Desktop\LibraryManagement.Desktop.csproj"
)
