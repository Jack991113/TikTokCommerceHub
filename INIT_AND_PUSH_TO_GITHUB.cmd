@echo off
setlocal
cd /d "%~dp0"

set /p REMOTE_URL=Please paste your GitHub repository URL (https://github.com/your-name/repo.git): 
if "%REMOTE_URL%"=="" (
  echo No repository URL entered. Cancelled.
  pause
  exit /b 1
)

git init
git branch -M main
git add .
git commit -m "Initial clean source export"
git remote remove origin >nul 2>nul
git remote add origin %REMOTE_URL%
git push -u origin main

echo.
echo If push succeeded, the clean source has been uploaded to GitHub.
pause
