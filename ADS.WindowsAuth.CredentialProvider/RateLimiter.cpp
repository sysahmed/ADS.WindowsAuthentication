#include "pch.h"
#include "RateLimiter.h"
#include <fstream>
#include <sstream>
#include <iomanip>

RateLimiter& RateLimiter::GetInstance()
{
    static RateLimiter instance;
    return instance;
}

RateLimiter::RateLimiter()
{
    // Зареждане на конфигурация от Registry (ако има)
    HKEY hKey;
    if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\ADS\\WindowsAuth\\RateLimit", 
                     0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD value;
        DWORD size = sizeof(DWORD);
        
        if (RegQueryValueEx(hKey, L"MaxAttempts", NULL, NULL, 
                           (LPBYTE)&value, &size) == ERROR_SUCCESS)
        {
            _maxAttempts = value;
        }
        
        if (RegQueryValueEx(hKey, L"TimeWindowSeconds", NULL, NULL, 
                           (LPBYTE)&value, &size) == ERROR_SUCCESS)
        {
            _timeWindowSeconds = value;
        }
        
        if (RegQueryValueEx(hKey, L"BlockDurationSeconds", NULL, NULL, 
                           (LPBYTE)&value, &size) == ERROR_SUCCESS)
        {
            _blockDurationSeconds = value;
        }
        
        RegCloseKey(hKey);
    }
}

bool RateLimiter::IsAllowed(const std::wstring& identifier)
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    auto now = std::chrono::system_clock::now();
    
    // Проверка дали е блокиран
    auto blockIt = _blockedIdentifiers.find(identifier);
    if (blockIt != _blockedIdentifiers.end())
    {
        if (now < blockIt->second.blockedUntil)
        {
            // Все още е блокиран
            LogSecurityEvent(identifier, "BLOCKED_ATTEMPT");
            return false;
        }
        else
        {
            // Блокирането е изтекло
            _blockedIdentifiers.erase(blockIt);
            _attempts.erase(identifier);
            LogSecurityEvent(identifier, "BLOCK_EXPIRED");
        }
    }
    
    // Изчистване на стари опити
    CleanupOldAttempts(identifier);
    
    // Проверка на броя опити
    auto attemptsIt = _attempts.find(identifier);
    if (attemptsIt != _attempts.end())
    {
        int failedAttempts = 0;
        for (const auto& attempt : attemptsIt->second)
        {
            if (!attempt.success)
            {
                failedAttempts++;
            }
        }
        
        if (failedAttempts >= _maxAttempts)
        {
            // Блокиране
            BlockRecord block;
            block.blockedUntil = now + std::chrono::seconds(_blockDurationSeconds);
            block.attemptCount = failedAttempts;
            _blockedIdentifiers[identifier] = block;
            
            LogSecurityEvent(identifier, "RATE_LIMIT_EXCEEDED");
            return false;
        }
    }
    
    return true;
}

void RateLimiter::RecordAttempt(const std::wstring& identifier, bool success)
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    AttemptRecord record;
    record.timestamp = std::chrono::system_clock::now();
    record.success = success;
    
    _attempts[identifier].push_back(record);
    
    if (success)
    {
        // При успешен опит, изчистваме историята
        _attempts.erase(identifier);
        _blockedIdentifiers.erase(identifier);
        LogSecurityEvent(identifier, "AUTH_SUCCESS");
    }
    else
    {
        LogSecurityEvent(identifier, "AUTH_FAILED");
    }
}

int RateLimiter::GetRemainingBlockTime(const std::wstring& identifier)
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    auto blockIt = _blockedIdentifiers.find(identifier);
    if (blockIt != _blockedIdentifiers.end())
    {
        auto now = std::chrono::system_clock::now();
        if (now < blockIt->second.blockedUntil)
        {
            auto remaining = std::chrono::duration_cast<std::chrono::seconds>(
                blockIt->second.blockedUntil - now);
            return static_cast<int>(remaining.count());
        }
    }
    
    return 0;
}

void RateLimiter::ClearHistory(const std::wstring& identifier)
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    _attempts.erase(identifier);
    _blockedIdentifiers.erase(identifier);
    
    LogSecurityEvent(identifier, "HISTORY_CLEARED");
}

void RateLimiter::CleanupOldAttempts(const std::wstring& identifier)
{
    auto attemptsIt = _attempts.find(identifier);
    if (attemptsIt == _attempts.end())
    {
        return;
    }
    
    auto now = std::chrono::system_clock::now();
    auto cutoff = now - std::chrono::seconds(_timeWindowSeconds);
    
    // Премахване на стари опити
    auto& attempts = attemptsIt->second;
    attempts.erase(
        std::remove_if(attempts.begin(), attempts.end(),
            [cutoff](const AttemptRecord& record) {
                return record.timestamp < cutoff;
            }),
        attempts.end()
    );
    
    // Ако няма повече опити, премахваме записа
    if (attempts.empty())
    {
        _attempts.erase(attemptsIt);
    }
}

void RateLimiter::LogSecurityEvent(const std::wstring& identifier, const std::string& event)
{
    // Логване в файл
    try
    {
        WCHAR modulePath[MAX_PATH];
        GetModuleFileName(NULL, modulePath, MAX_PATH);
        std::wstring moduleDir = modulePath;
        size_t lastSlash = moduleDir.find_last_of(L"\\/");
        if (lastSlash != std::wstring::npos)
        {
            moduleDir = moduleDir.substr(0, lastSlash);
        }
        
        std::wstring logPath = moduleDir + L"\\LOGS";
        CreateDirectory(logPath.c_str(), NULL);
        
        auto now = std::chrono::system_clock::now();
        auto time_t_now = std::chrono::system_clock::to_time_t(now);
        
        std::tm tm;
        localtime_s(&tm, &time_t_now);
        
        std::wstringstream filename;
        filename << logPath << L"\\SECURITY_"
                 << std::put_time(&tm, L"%Y%m%d") << L".LOG";
        
        std::wofstream logFile(filename.str(), std::ios::app);
        if (logFile.is_open())
        {
            logFile << L"[" << std::put_time(&tm, L"%Y-%m-%d %H:%M:%S") << L"] "
                    << L"[" << std::wstring(event.begin(), event.end()) << L"] "
                    << L"Identifier: " << identifier << std::endl;
            logFile.close();
        }
    }
    catch (...)
    {
        // Игнорираме грешки при логване
    }
    
    // Логване в Event Viewer
    HANDLE hEventLog = RegisterEventSource(NULL, L"ADS.WindowsAuth.CredentialProvider");
    if (hEventLog)
    {
        std::wstring message = L"Rate Limiter Event: " + 
                              std::wstring(event.begin(), event.end()) + 
                              L" for " + identifier;
        
        const WCHAR* strings[] = { message.c_str() };
        
        ReportEvent(hEventLog, 
                   EVENTLOG_WARNING_TYPE,
                   0, 
                   0, 
                   NULL, 
                   1, 
                   0, 
                   strings, 
                   NULL);
        
        DeregisterEventSource(hEventLog);
    }
}
