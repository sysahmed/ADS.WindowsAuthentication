#include "pch.h"
#include "LanguageManager.h"
#include <Windows.h>

LanguageManager& LanguageManager::GetInstance()
{
    static LanguageManager instance;
    return instance;
}

LanguageManager::LanguageManager()
    : _currentLanguage(Language::English)
{
    DetectSystemLanguage();
    LoadStrings();
}

void LanguageManager::DetectSystemLanguage()
{
    // Получаване на системния език
    LANGID langId = GetUserDefaultUILanguage();
    WORD primaryLang = PRIMARYLANGID(langId);
    
    switch (primaryLang)
    {
    case LANG_BULGARIAN:
        _currentLanguage = Language::Bulgarian;
        break;
    case LANG_GERMAN:
        _currentLanguage = Language::German;
        break;
    case LANG_ENGLISH:
    default:
        _currentLanguage = Language::English;
        break;
    }
}

void LanguageManager::SetLanguage(Language lang)
{
    _currentLanguage = lang;
    LoadStrings();
}

std::wstring LanguageManager::GetString(const std::string& key)
{
    auto it = _strings.find(key);
    if (it != _strings.end())
    {
        return it->second;
    }
    
    // Fallback на английски ако ключът не е намерен
    return L"[" + std::wstring(key.begin(), key.end()) + L"]";
}

void LanguageManager::LoadStrings()
{
    _strings.clear();
    
    switch (_currentLanguage)
    {
    case Language::Bulgarian:
        LoadBulgarianStrings();
        break;
    case Language::German:
        LoadGermanStrings();
        break;
    case Language::English:
    default:
        LoadEnglishStrings();
        break;
    }
}

void LanguageManager::LoadBulgarianStrings()
{
    // Основни текстове
    _strings["title"] = L"ADS Windows Автентикация";
    _strings["subtitle"] = L"Сканирайте QR кода с мобилното приложение";
    _strings["qr_label"] = L"QR Код за Автентикация";
    
    // Инструкции
    _strings["instruction_1"] = L"1. Отворете ADS мобилното приложение";
    _strings["instruction_2"] = L"2. Сканирайте QR кода";
    _strings["instruction_3"] = L"3. Въведете вашите credentials";
    _strings["instruction_4"] = L"4. Потвърдете автентикацията";
    
    // Статуси
    _strings["status_waiting"] = L"Изчакване на одобрение...";
    _strings["status_approved"] = L"Одобрено! Влизане...";
    _strings["status_rejected"] = L"Отказано! Опитайте отново.";
    _strings["status_expired"] = L"Сесията изтече. Моля, опитайте отново.";
    _strings["status_error"] = L"Грешка при свързване със сървъра.";
    
    // Fallback опции
    _strings["fallback_title"] = L"Други опции за вход";
    _strings["fallback_sms"] = L"Изпрати SMS код";
    _strings["fallback_email"] = L"Изпрати Email линк";
    _strings["fallback_bluetooth"] = L"Bluetooth автентикация";
    
    // Грешки
    _strings["error_network"] = L"Грешка в мрежата. Проверете интернет връзката.";
    _strings["error_timeout"] = L"Времето изтече. Моля, опитайте отново.";
    _strings["error_invalid_qr"] = L"Невалиден QR код.";
    _strings["error_rate_limit"] = L"Твърде много опити. Моля, изчакайте {0} минути.";
    
    // Бутони
    _strings["button_retry"] = L"Опитай отново";
    _strings["button_cancel"] = L"Отказ";
    _strings["button_help"] = L"Помощ";
    
    // Помощ
    _strings["help_title"] = L"Нужда от помощ?";
    _strings["help_text"] = L"Свържете се с IT поддръжката на: support@nursanbulgaria.com";
}

void LanguageManager::LoadEnglishStrings()
{
    // Basic texts
    _strings["title"] = L"ADS Windows Authentication";
    _strings["subtitle"] = L"Scan the QR code with the mobile app";
    _strings["qr_label"] = L"QR Code Authentication";
    
    // Instructions
    _strings["instruction_1"] = L"1. Open the ADS mobile app";
    _strings["instruction_2"] = L"2. Scan the QR code";
    _strings["instruction_3"] = L"3. Enter your credentials";
    _strings["instruction_4"] = L"4. Confirm authentication";
    
    // Statuses
    _strings["status_waiting"] = L"Waiting for approval...";
    _strings["status_approved"] = L"Approved! Logging in...";
    _strings["status_rejected"] = L"Rejected! Please try again.";
    _strings["status_expired"] = L"Session expired. Please try again.";
    _strings["status_error"] = L"Error connecting to server.";
    
    // Fallback options
    _strings["fallback_title"] = L"Other login options";
    _strings["fallback_sms"] = L"Send SMS code";
    _strings["fallback_email"] = L"Send Email link";
    _strings["fallback_bluetooth"] = L"Bluetooth authentication";
    
    // Errors
    _strings["error_network"] = L"Network error. Check your internet connection.";
    _strings["error_timeout"] = L"Timeout. Please try again.";
    _strings["error_invalid_qr"] = L"Invalid QR code.";
    _strings["error_rate_limit"] = L"Too many attempts. Please wait {0} minutes.";
    
    // Buttons
    _strings["button_retry"] = L"Retry";
    _strings["button_cancel"] = L"Cancel";
    _strings["button_help"] = L"Help";
    
    // Help
    _strings["help_title"] = L"Need help?";
    _strings["help_text"] = L"Contact IT support at: support@nursanbulgaria.com";
}

void LanguageManager::LoadGermanStrings()
{
    // Grundlegende Texte
    _strings["title"] = L"ADS Windows Authentifizierung";
    _strings["subtitle"] = L"Scannen Sie den QR-Code mit der mobilen App";
    _strings["qr_label"] = L"QR-Code-Authentifizierung";
    
    // Anweisungen
    _strings["instruction_1"] = L"1. Öffnen Sie die ADS Mobile App";
    _strings["instruction_2"] = L"2. Scannen Sie den QR-Code";
    _strings["instruction_3"] = L"3. Geben Sie Ihre Anmeldedaten ein";
    _strings["instruction_4"] = L"4. Bestätigen Sie die Authentifizierung";
    
    // Status
    _strings["status_waiting"] = L"Warten auf Genehmigung...";
    _strings["status_approved"] = L"Genehmigt! Anmeldung...";
    _strings["status_rejected"] = L"Abgelehnt! Bitte versuchen Sie es erneut.";
    _strings["status_expired"] = L"Sitzung abgelaufen. Bitte versuchen Sie es erneut.";
    _strings["status_error"] = L"Fehler beim Verbinden mit dem Server.";
    
    // Fallback-Optionen
    _strings["fallback_title"] = L"Andere Anmeldeoptionen";
    _strings["fallback_sms"] = L"SMS-Code senden";
    _strings["fallback_email"] = L"E-Mail-Link senden";
    _strings["fallback_bluetooth"] = L"Bluetooth-Authentifizierung";
    
    // Fehler
    _strings["error_network"] = L"Netzwerkfehler. Überprüfen Sie Ihre Internetverbindung.";
    _strings["error_timeout"] = L"Zeitüberschreitung. Bitte versuchen Sie es erneut.";
    _strings["error_invalid_qr"] = L"Ungültiger QR-Code.";
    _strings["error_rate_limit"] = L"Zu viele Versuche. Bitte warten Sie {0} Minuten.";
    
    // Schaltflächen
    _strings["button_retry"] = L"Wiederholen";
    _strings["button_cancel"] = L"Abbrechen";
    _strings["button_help"] = L"Hilfe";
    
    // Hilfe
    _strings["help_title"] = L"Brauchen Sie Hilfe?";
    _strings["help_text"] = L"Kontaktieren Sie den IT-Support unter: support@nursanbulgaria.com";
}
