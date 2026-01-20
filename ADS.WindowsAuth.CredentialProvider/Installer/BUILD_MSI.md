# Създаване на MSI инсталатор за Credential Provider

## Изисквания

1. **WiX Toolset v3.11 или по-нова версия**
   - Свали от: https://wixtoolset.org/releases/
   - Инсталирай WiX Toolset Build Tools

2. **Visual Studio 2022** (за компилиране на Custom Actions)

## Стъпка 1: Инсталиране на WiX Toolset

1. Свали WiX Toolset от: https://wixtoolset.org/releases/
2. Инсталирай WiX Toolset Build Tools
3. Рестартирай Visual Studio

## Стъпка 2: Компилиране на Custom Actions

Custom Actions DLL-ите за регистрация на COM компонента трябва да се компилират първо.

**В Visual Studio:**

1. Създай нов проект: **Visual C++ → Windows → Dynamic Link Library (DLL)**
2. Име: `RegisterDLL`
3. Добави `RegisterDLL.cpp` в проекта
4. Добави `msi.lib` в Linker → Input → Additional Dependencies
5. Build проекта

Повтори за `UnregisterDLL`.

## Стъпка 3: Компилиране на MSI

**В Visual Studio:**

1. Отвори `ADS.WindowsAuth.CredentialProvider.wixproj`
2. Build Solution (Ctrl+Shift+B)
3. MSI файлът ще се намери в: `Installer\bin\Release\ADS.WindowsAuth.CredentialProvider.msi`

## Стъпка 4: Инсталация

**От администраторски PowerShell или двойно кликване:**

```powershell
# От PowerShell
Start-Process msiexec.exe -ArgumentList "/i `"ADS.WindowsAuth.CredentialProvider.msi`" /qn" -Verb RunAs

# Или двойно кликване на MSI файла
```

## Стъпка 5: Деинсталация

```powershell
# От PowerShell
Start-Process msiexec.exe -ArgumentList "/x `"ADS.WindowsAuth.CredentialProvider.msi`" /qn" -Verb RunAs

# Или от Control Panel → Programs and Features
```

## Алтернатива: По-прост подход без Custom Actions

Ако не искаш да използваш Custom Actions, можеш да използваш PowerShell скрипт в MSI:

1. Добави `REGISTER.ps1` като файл в MSI
2. Използвай Custom Action който изпълнява PowerShell скрипта

## Важни бележки

- MSI инсталаторът изисква администраторски права
- DLL-ът се копира в `C:\Program Files\ADS\CredentialProvider\`
- Registry ключът се създава автоматично
- При деинсталация, DLL-ът се отменя от регистрация

