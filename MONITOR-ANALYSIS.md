# Анализ на ADS.WindowsAuth.Monitor

## Архитектура

Monitor работи като **Windows Service** под акаунт **LocalSystem** в **Session 0**.

### Session 0 изолация

| Аспект | Session 0 (Monitor) | Session 1+ (потребител) |
|--------|---------------------|-------------------------|
| **GetForegroundWindow()** | Връща NULL/0 – няма активен прозорец | Връща handle на активния прозорец |
| **SetWindowsHookEx (keyboard/mouse)** | Hooks се инсталират, но **не получават** събития от потребителската сесия | Получава всички събития |
| **CopyFromScreen / GetSystemMetrics** | Често връща 0 или грешка | Работи нормално |
| **Process.GetProcesses()** | Работи | Работи |
| **WMI / Event Log** | Работи (с подходящи права) | Работи |

---

## Задачи (Tasks) и реално поведение

### ✅ Работи в Session 0

| Задача | Endpoint | Описание |
|--------|----------|----------|
| **MonitorProcesses** | `/api/activity/application/start`, `/stop`, `/file-open` | Enumeration на процеси, Office file-open via WMI command line |
| **MonitorScreenTime** | `/api/activity/screentime/update` | Време от старт на сесията |
| **MonitorUserSessions** | `/api/activity/login` | Windows Security Event Log 4624 (login събития) |
| **MonitorNetworkActivity** | `/api/activity/network` | Мрежови интерфейси |
| **MonitorSystemInfo** | `/api/activity/system` | OS, CPU, RAM |
| **MonitorUsbDevices** | `/api/activity/usb` | USB устройства (WMI) |
| **MonitorFileActivity** | `/api/activity/files` | Последни файлове в Documents/Desktop |
| **MonitorAndFilterWebsites** | hosts + firewall | Блокиране на домейни – **работи** |
| **MonitorVpnSoftware** | `/api/logs/upload` | Детекция на VPN процеси |
| **MonitorDnsChanges** | `/api/logs/upload` | Промени в DNS |
| **SendMachineSnapshot** | `/api/machines/snapshot` | Процеси + инсталирани програми |
| **LoggerService** | `/api/logs/upload` | Системни логове (LogEntries) |
| **CheckPolicies** | Policy API + SyncOfflineDataAsync | Политики, offline buffer sync |

### ❌ НЕ работи в Session 0

| Задача | Причина |
|--------|---------|
| **InputCapture** (клавиши, кликове) | `SetWindowsHookEx(WH_KEYBOARD_LL, WH_MOUSE_LL)` не получава събития от потребителската сесия. `GetForegroundWindow()` връща 0. Опашката остава празна. |
| **MonitorActiveWindow** (focus time) | `GetForegroundWindow()` връща 0 → няма активен прозорец → `appName` винаги празен, няма натрупване на време. |
| **MonitorOutlookActivity** | `MainWindowTitle` и `GetWindowText` върху прозорци от друга сесия обикновено връщат празно. |
| **MonitorScreenshots** | `GetSystemMetrics` може да върне 0. `CopyFromScreen` не работи от Session 0. |

### ⚠️ Не е имплементирано

| Задача | Описание |
|--------|----------|
| **MonitorBrowserTabs** | **Празен цикъл** – само итерира по `chrome`, `msedge`, `firefox` и т.н., но **нищо не прави** с тях. Няма извличане на URL, няма изпращане към `/api/activity/website`. Коментар в кода: „Засега само логваме ако браузърът е отворен“ – дори логване няма. |

---

## Обобщение: какво се записва реално

| Тип данни | Източник | Работи? |
|-----------|----------|---------|
| Системни логове (LogEntries) | LoggerService | ✅ Да |
| Старт/стоп приложения | MonitorProcesses | ✅ Да |
| Office файл отворен | MonitorProcesses + WMI | ✅ Да |
| Screen time | MonitorScreenTime | ✅ Да |
| Login events | MonitorUserSessions | ✅ Да (при права за Event Log) |
| Мрежа, система, USB, файлове | Съответните задачи | ✅ Да |
| VPN/DNS алерти | MonitorVpnSoftware, MonitorDnsChanges | ✅ Да |
| Machine snapshot | SendMachineSnapshot | ✅ Да |
| **Клавиши и кликове** | InputCapture | ❌ Не |
| **Focus time по приложение** | MonitorActiveWindow | ❌ Не |
| **Browsing / посетени сайтове** | MonitorBrowserTabs | ❌ Не имплементирано |
| Outlook имейл активност | MonitorOutlookActivity | ❌ Почти не |
| Скрийншоти | MonitorScreenshots | ❌ Не |

---

## Причини за липсващи данни

1. **Клавиши, кликове** – Monitor е в Session 0; hooks не получават consumer input. Решение: **Client** в потребителска сесия с InputCapture.
2. **Browsing** – `MonitorBrowserTabs` е празен. Няма browser extension или друг източник за URL-и.
3. **Focus time** – `GetForegroundWindow()` не работи в Session 0.
4. **Outlook / Screenshots** – зависят от потребителска сесия; от Session 0 не работят.

---

## Препоръки

1. **InputCapture** – да остане в Monitor за „бъдещи“ сценарии (напр. Interactive Service), но основно разчитане на **Client** за реални данни.
2. **MonitorBrowserTabs** – или да се имплементира (browser extension + изпращане към `/api/activity/website`), или да се премахне/отбележи като неизползван.
3. **MonitorActiveWindow, MonitorOutlookActivity, MonitorScreenshots** – да се документира ясно, че не работят в Session 0 и изискват Interactive Service или друг подход.
