@echo off
setlocal
cd /d "%~dp0"

start "CommerceHub Store1" /D "%~dp0apps\Store1" dotnet run --project "%~dp0apps\Store1\TikTokOrderPrinter.Store1.csproj" --urls http://localhost:5038
start "CommerceHub Store2" /D "%~dp0apps\Store2" dotnet run --project "%~dp0apps\Store2\TikTokOrderPrinter.Store2.csproj" --urls http://localhost:5039
start "CommerceHub Dashboard" /D "%~dp0apps\Dashboard" dotnet run --project "%~dp0apps\Dashboard\TikTokSalesStats.csproj" --urls http://localhost:5048

timeout /t 4 /nobreak >nul
start "" "http://localhost:5038"
start "" "http://localhost:5039"
start "" "http://localhost:5048"
