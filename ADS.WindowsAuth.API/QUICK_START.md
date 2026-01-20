# Бърз старт за локално тестване

## Проблем: Timeout при свързване с API

Ако виждаш timeout грешки, това означава че приложенията се опитват да се свържат с production URL (`https://ads-auth.nursanbulgaria.com`) вместо локалния API.

## Решение: Конфигурирай всички приложения за локално тестване

### 1. Стартирай API-то локално

```bash
dotnet run --project ADS.WindowsAuth.API
```

API-то ще стартира на `http://localhost:5000`

### 2. Обнови Client приложението

Файл: `ADS.WindowsAuth.Client/appsettings.json`

```json
{
  "ApiConfiguration": {
    "BaseUrl": "http://localhost:5000"
  }
}
```

### 3. Обнови Monitor Service

Файл: `ADS.WindowsAuth.Monitor/appsettings.Development.json` (вече е обновен)

Или за production: `ADS.WindowsAuth.Monitor/appsettings.json`

```json
{
  "ServiceConfiguration": {
    "ServiceUrl": "http://localhost:5000",
    ...
  }
}
```

### 4. Рестартирай приложенията

След промяна на конфигурацията, рестартирай:
- Client приложението
- Monitor Service (ако е инсталиран като Windows Service)

### 5. Проверка

След рестарт, логовете трябва да показват:
```
Зареден API URL от конфигурация: http://localhost:5000
ApiClient инициализиран с BaseUrl: http://localhost:5000
```

## Важно

- За локално тестване: използвай `http://localhost:5000`
- За production: използвай `https://ads-auth.nursanbulgaria.com`
- Уверете се че API-то е стартирано преди другите приложения

