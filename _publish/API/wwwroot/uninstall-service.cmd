@echo off
REM ============================================
REM Команди за премахване на ADS Windows Auth Monitor Service
REM ============================================
REM
REM ИЗИСКВАНИЯ:
REM - Трябва да се изпълнява като АДМИНИСТРАТОР
REM
REM ============================================

echo.
echo ============================================
echo ADS Windows Auth Monitor Service - Деинсталация
echo ============================================
echo.

REM Проверка за администраторски права
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ГРЕШКА] Скриптът трябва да се изпълнява като АДМИНИСТРАТОР!
    echo Моля, стартирай отново с "Run as administrator"
    pause
    exit /b 1
)

set SERVICE_NAME=ADS.WindowsAuth.Monitor

echo [1/2] Проверка за съществуващ сервиз...
sc.exe query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% neq 0 (
    echo Сервизът не е намерен. Няма какво да се премахва.
    pause
    exit /b 0
)

echo [2/2] Спиране и премахване на сервиза...
sc.exe stop "%SERVICE_NAME%" >nul 2>&1
timeout /t 2 /nobreak >nul
sc.exe delete "%SERVICE_NAME%"
if %errorLevel% neq 0 (
    echo [ГРЕШКА] Неуспешно премахване на сервиза!
    pause
    exit /b 1
)

echo Сервизът е премахнат успешно!
echo.
pause

