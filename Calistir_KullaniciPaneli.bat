@echo off
chcp 65001 > nul
echo ========================================================
echo   Kütüphane Kullanıcı Paneli (3D Ekran) Başlatılıyor...
echo ========================================================
start "" ".\LibraryManagement.Desktop\bin\Debug\net8.0-windows\LibraryManagement.Desktop.exe" --customer-3d
exit
