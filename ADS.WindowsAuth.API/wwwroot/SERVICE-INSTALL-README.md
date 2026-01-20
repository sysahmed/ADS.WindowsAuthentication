# Инсталация на ADS Windows Auth Monitor Service

## Автоматична инсталация

### Вариант 1: Използване на CMD скрипт
1. Стартирай `install-service.cmd` като **Администратор** (десен бутон → Run as administrator)
2. Скриптът автоматично ще:
   - Провери за съществуващ сервиз и ще го премахне ако е нужно
   - Създаде директория `C:\ADS\Monitor` ако не съществува
   - Регистрира сервиза като Windows Service
   - Стартира сервиза автоматично

### Вариант 2: Използване на PowerShell скрипт
Използвай `Install-ADS.ps1` който инсталира всички компоненти включително Monitor Service.

## Ръчна инсталация с команди

### Стъпка 1: Подготовка
```cmd
REM Създаване на директория (ако не съществува)
mkdir C:\ADS\Monitor

REM Копиране на Monitor EXE и всички DLL файлове в C:\ADS\Monitor\
```

### Стъпка 2: Регистрация на сервиза
```cmd
sc.exe create "ADS.WindowsAuth.Monitor" binPath= "C:\ADS\Monitor\ADS.WindowsAuth.Monitor.exe" start= auto DisplayName= "ADS Windows Authentication Monitor"
```

### Стъпка 3: Стартиране на сервиза
```cmd
sc.exe start "ADS.WindowsAuth.Monitor"
```

## Полезни команди

### Проверка на статус
```cmd
sc.exe query "ADS.WindowsAuth.Monitor"
```

### Стартиране на сервиза
```cmd
sc.exe start "ADS.WindowsAuth.Monitor"
```

### Спиране на сервиза
```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
```

### Премахване на сервиза
```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
sc.exe delete "ADS.WindowsAuth.Monitor"
```

## Деинсталация

### Автоматична деинсталация
Стартирай `uninstall-service.cmd` като **Администратор**.

### Ръчна деинсталация
```cmd
sc.exe stop "ADS.WindowsAuth.Monitor"
sc.exe delete "ADS.WindowsAuth.Monitor"
```

## Важни бележки

1. **Всички команди трябва да се изпълняват като Администратор!**
2. **Пътят към EXE файла трябва да е абсолютен и в кавички!**
3. **След регистрация, сервизът ще се стартира автоматично при рестарт на Windows (start= auto)**
4. **Ако има проблеми, провери Event Viewer за грешки**

## Проверка на логове

Логовете на Monitor Service се намират в:
```
C:\ADS\Monitor\LOGS\NURSAN*.LOG
```

## Проблеми и решения

### Сервизът не се стартира
1. Провери дали EXE файлът съществува на правилния път
2. Провери дали всички DLL файлове са копирани в същата папка
3. Провери Event Viewer за грешки
4. Провери дали appsettings.json е наличен в папката на сервиза

### "Access Denied" грешка
- Увери се че изпълняваш командите като Администратор

### "The specified service does not exist"
- Провери дали името на сервиза е точно: `ADS.WindowsAuth.Monitor`
- Използвай: `sc.exe query "ADS.WindowsAuth.Monitor"` за проверка

