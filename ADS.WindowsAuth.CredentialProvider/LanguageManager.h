#pragma once
#include "pch.h"
#include <string>
#include <map>

enum class Language
{
    Bulgarian,
    English,
    German
};

class LanguageManager
{
public:
    static LanguageManager& GetInstance();
    
    // Автоматично detection на системния език
    void DetectSystemLanguage();
    
    // Задаване на език ръчно
    void SetLanguage(Language lang);
    
    // Получаване на текст по ключ
    std::wstring GetString(const std::string& key);
    
    // Получаване на текущия език
    Language GetCurrentLanguage() const { return _currentLanguage; }

private:
    LanguageManager();
    ~LanguageManager() = default;
    
    // Забрана на копиране
    LanguageManager(const LanguageManager&) = delete;
    LanguageManager& operator=(const LanguageManager&) = delete;
    
    void LoadStrings();
    void LoadBulgarianStrings();
    void LoadEnglishStrings();
    void LoadGermanStrings();
    
    Language _currentLanguage;
    std::map<std::string, std::wstring> _strings;
};
