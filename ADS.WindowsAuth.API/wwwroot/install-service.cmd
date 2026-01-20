@echo off
REM ============================================
REM Команди за регистрация на ADS Windows Auth Monitor Service
REM ============================================
REM
REM ИЗИСКВАНИЯ:
REM - Трябва да се изпълнява като АДМИНИСТРАТОР
REM - Monitor EXE файлът трябва да е в C:\ADS\Monitor\ADS.WindowsAuth.Monitor.exe
REM
REM ============================================

echo.
echo ============================================
echo ADS Windows Auth Monitor Service - Инсталация
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

REM Път към Monitor EXE
set SERVICE_NAME=ADS.WindowsAuth.Monitor
set EXE_PATH=C:\ADS\Monitor\ADS.WindowsAuth.Monitor.exe
set DISPLAY_NAME=ADS Windows Authentication Monitor

REM Проверка дали EXE файлът съществува
if not exist "%EXE_PATH%" (
    echo [ГРЕШКА] Monitor EXE не е намерен на: %EXE_PATH%
    echo Моля, копирай ADS.WindowsAuth.Monitor.exe в C:\ADS\Monitor\
    pause
    exit /b 1
)

echo [1/4] Проверка за съществуващ сервиз...
sc.exe query "%SERVICE_NAME%" >nul 2>&1
if %errorLevel% equ 0 (
    echo Сервизът вече съществува. Премахване...
    sc.exe stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 2 /nobreak >nul
    sc.exe delete "%SERVICE_NAME%" >nul 2>&1
    timeout /t 2 /nobreak >nul
    echo Старият сервиз е премахнат.
)

echo [2/4] Създаване на директория...
if not exist "C:\ADS\Monitor" (
    mkdir "C:\ADS\Monitor"
    echo Директория създадена.
)

echo [3/4] Регистрация на Windows Service...
sc.exe create "%SERVICE_NAME%" binPath= "\"%EXE_PATH%\"" start= auto DisplayName= "%DISPLAY_NAME%"
if %errorLevel% neq 0 (
    echo [ГРЕШКА] Неуспешна регистрация на сервиза!
    pause
    exit /b 1
)
echo Сервизът е регистриран успешно!

echo [4/4] Стартиране на сервиза...
sc.exe start "%SERVICE_NAME%"
if %errorLevel% neq 0 (
    echo [ПРЕДУПРЕЖДЕНИЕ] Сервизът е регистриран, но не може да се стартира автоматично.
    echo Стартирай го ръчно от Services (services.msc)
) else (
    echo Сервизът е стартиран успешно!
)

echo.
echo ============================================
echo Инсталацията завърши!
echo ============================================
echo.
echo Полезни команди:
echo   - Проверка на статус: sc.exe query "%SERVICE_NAME%"
echo   - Стартиране: sc.exe start "%SERVICE_NAME%"
echo   - Спиране: sc.exe stop "%SERVICE_NAME%"
echo   - Премахване: sc.exe delete "%SERVICE_NAME%"
echo.
pause

