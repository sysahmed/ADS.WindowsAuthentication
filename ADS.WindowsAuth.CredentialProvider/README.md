# Windows Credential Provider за QR Code Authentication

## Описание

Това е C++ Credential Provider, който показва QR код на Windows lock screen за аутентикация през мобилно приложение.

## Компилиране

1. Отворете проекта в Visual Studio
2. Изберете конфигурация: Debug x64 или Release x64
3. Build Solution (Ctrl+Shift+B)

## Регистрация

След компилиране, DLL-ът трябва да се регистрира в Windows:

### Регистрация (трябват администраторски права):

```cmd
regsvr32 "C:\Path\To\ADS.WindowsAuth.CredentialProvider.dll"
```

### Отмяна на регистрация:

```cmd
regsvr32 /u "C:\Path\To\ADS.WindowsAuth.CredentialProvider.dll"
```

## Важни бележки

1. **GUID**: Променете GUID-а в `dllmain.cpp` на уникален GUID преди регистрация
2. **QR Code библиотека**: Текущата реализация е опростена. За production използвайте библиотека като:
   - qrcodegen (C++)
   - libqrencode
   - ZXing C++
3. **API URL**: Променете базовия URL в `ApiClient.cpp` ако API-то не е на localhost:5000
4. **Тестване**: За тестване на Credential Provider използвайте Remote Desktop или виртуална машина

## Структура

- `CredentialProvider.cpp/h` - Основен ICredentialProvider интерфейс
- `Credential.cpp/h` - ICredentialProviderCredential реализация
- `QrCodeGenerator.cpp/h` - Генериране на QR код като HBITMAP
- `ApiClient.cpp/h` - HTTP клиент за комуникация с C# API
- `ClassFactory.cpp/h` - COM Class Factory
- `dllmain.cpp` - DLL entry point и COM регистрация

## Как работи

1. При lock screen, Windows зарежда Credential Provider-а
2. Provider-ът създава сесия чрез C# API (`POST /api/auth/session`)
3. Генерира QR код като HBITMAP с данни: `{ServiceUrl}/auth?token={accessToken}`
4. Показва QR кода в login tile чрез `GetBitmapValue(SFI_QR_CODE)`
5. Polling thread проверява статуса на сесията всяка 2 секунди (`GET /api/auth/session/{sessionId}/status`)
6. При одобрение (status = Approved), показва съобщение за успех
7. При отхвърляне (status = Rejected), показва съобщение за грешка
8. При изтичане (status = Expired), автоматично създава нова сесия и обновява QR кода

## Конфигурация

### Registry (препоръчително)
```
HKLM\SOFTWARE\ADS\WindowsAuth
  ServiceUrl = "https://ads-auth.nursanbulgaria.com"
```

### Environment Variable (алтернатива)
```
ADS_API_URL = "https://ads-auth.nursanbulgaria.com"
```

### Default
Ако нито Registry, нито Environment Variable са зададени, използва: `http://localhost:5000`

