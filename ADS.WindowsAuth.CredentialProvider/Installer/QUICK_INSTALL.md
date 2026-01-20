# Бърза инсталация на WiX Toolset

## Стъпка 1: Сваляне на WiX Toolset

**Вариант A: Официален сайт**
1. Отвори: https://wixtoolset.org/releases/
2. Свали **WiX Toolset v3.11.2** (или по-нова версия)
3. Файл: `wix311.exe` или `wix3112rtm.exe`

**Вариант B: Директна връзка**
- https://github.com/wixtoolset/wix3/releases/download/wix3112rtm/wix311.exe

## Стъпка 2: Инсталация

1. Стартирай `wix311.exe`
2. Избери **WiX Toolset Build Tools**
3. Инсталирай
4. Рестартирай PowerShell

## Стъпка 3: Проверка

```powershell
# Проверка дали WiX е инсталиран
Test-Path "C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe"
```

## Стъпка 4: Създаване на MSI

```powershell
cd ADS.WindowsAuth.CredentialProvider\Installer
.\INSTALL_MSI.ps1
```

## Алтернатива: Ръчно компилиране

Ако скриптът не работи, използвай WiX командния ред:

```powershell
# Намери пътя до WiX
$wixPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"
cd ADS.WindowsAuth.CredentialProvider\Installer

# Компилиране
& "$wixPath\candle.exe" SIMPLE_MSI.wxs
& "$wixPath\light.exe" SIMPLE_MSI.wixobj

# MSI ще е готов!
```

