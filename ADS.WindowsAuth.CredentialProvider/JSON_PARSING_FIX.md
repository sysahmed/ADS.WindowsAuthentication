# 🔧 Оправка на JSON Parsing - "Parameter is Incorrect" Грешка

## ✅ ОПРАВЕНО - Без Компилационни Грешки!

### 🔴 Проблемът

След QR аутентикация на Windows login екрана, системата давала грешка **"parameter is incorrect"** при опит за автоматичен login.

### Причина

JSON парсирането в `ApiClient.cpp` не разпаковаваше JSON-escaped символи в паролата:

```
API връща:     {"password":"Pass\\\"word"}  (JSON-escaped)
Parser извлича: Pass\\\"word                (с буквални escape символи)
Windows получава: Невалидна парола ❌
```

Когато паролата съдържа специални символи (кавички, backslash и т.н.), те се JSON-escapeat:
- `"` → `\"`
- `\` → `\\`
- Нови редове → `\n`
- Табулации → `\t`
- И т.н.

Старият код просто извличаше текста между кавичките без разпаковане, което водеше до невалидни credentials.

## ✅ Решението - Имплементирано

Добавени са две нови helper функции в `ApiClient.cpp` **ПРЕДИ** методите които ги използват:

### 1. `UnescapeJsonString()` - Разпаковане на JSON escape последователности

```cpp
std::wstring UnescapeJsonString(const std::wstring& jsonString)
{
    // Преобразува JSON-escaped символи в реални символи:
    // \" → "
    // \\ → \
    // \n → нов ред
    // \t → табулация
    // \uXXXX → Unicode символ
    // И т.н.
}
```

**Поддържани escape последователности:**
- `\"` → `"` (кавичка)
- `\\` → `\` (backslash)
- `\/` → `/` (наклонена черта)
- `\b` → `\b` (backspace)
- `\f` → `\f` (form feed)
- `\n` → `\n` (нов ред)
- `\r` → `\r` (carriage return)
- `\t` → `\t` (табулация)
- `\uXXXX` → Unicode символ

### 2. `ExtractJsonStringValue()` - Безопасно извличане на JSON стойности

```cpp
bool ExtractJsonStringValue(const std::wstring& json, 
                           const std::wstring& fieldName, 
                           std::wstring& value)
{
    // Намира JSON поле по име
    // Коректно обработва escaped кавички
    // Разпаковава резултата
    // Връща true ако успешно
}
```

## 📝 Обновени методи

### ✅ `GetApprovedSessionInfo()` - ОПРАВЕН
- Сега използва `ExtractJsonStringValue()` за всички полета
- Паролата се разпаковава правилно
- Логва успешното извличане на credentials

### ✅ `CreateSession()` - ОПРАВЕН
- Използва новата безопасна функция за парсиране
- Коректно обработва sessionId и accessToken

### ✅ `GetSessionStatus()` - ОПРАВЕН
- Използва новата безопасна функция за парсиране
- Коректно обработва статуса

## 🧪 Тестване

След компилиране на DLL-то, QR аутентикацията трябва да работи правилно:

1. **Сканирай QR код** с мобилното приложение
2. **Въведи credentials** в мобилното приложение
3. **Одобри** аутентикацията
4. **Windows login** трябва да се случи **автоматично** без "parameter is incorrect" грешка

## 📋 Файлове, които са променени

- ✅ `ADS.WindowsAuth.CredentialProvider/ApiClient.cpp`
  - Добавени: `UnescapeJsonString()` функция (линия ~91)
  - Добавени: `ExtractJsonStringValue()` функция (линия ~164)
  - Обновени: `GetApprovedSessionInfo()`, `CreateSession()`, `GetSessionStatus()`
  - Премахнати: Дублирани дефиниции

- ✅ `ADS.WindowsAuth.CredentialProvider/ApiClient.h`
  - Без промени (функциите са static/inline)

## 🔍 Логване

Всички операции се логват в: `C:\ADS\Logs\ADS_CredentialProvider_QR.log`

Можеш да видиш:
- Какви credentials са извлечени
- Дължина на паролата
- Успешност на парсирането
- Всички грешки при обработка

## 🚀 Следващи стъпки

1. ✅ **Компилирай** проекта: `Release x64` конфигурация
   - Без компилационни грешки!
   
2. **Регистрирай** DLL-то с `REGISTER_DLL_FIXED.ps1`

3. **Рестартирай** компютъра

4. **Тествай** QR аутентикацията на login екрана
   - Трябва да работи без "parameter is incorrect" грешка!

## 📊 Статус

| Компонент | Статус | Детайли |
|-----------|--------|---------|
| JSON Parsing | ✅ ОПРАВЕНО | Разпаковане на escape символи |
| CreateSession | ✅ ОПРАВЕНО | Безопасно парсиране |
| GetSessionStatus | ✅ ОПРАВЕНО | Безопасно парсиране |
| GetApprovedSessionInfo | ✅ ОПРАВЕНО | Разпаковане на парола |
| Компилация | ✅ БЕЗ ГРЕШКИ | Готово за build |
