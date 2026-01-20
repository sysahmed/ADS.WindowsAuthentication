#pragma once
#include "pch.h"
#include <string>
#include <map>
#include <vector>
#include <chrono>
#include <mutex>

class RateLimiter
{
public:
    static RateLimiter& GetInstance();
    
    // Проверка дали е позволен нов опит
    bool IsAllowed(const std::wstring& identifier);
    
    // Записване на опит
    void RecordAttempt(const std::wstring& identifier, bool success = false);
    
    // Получаване на оставащо време за блокиране (в секунди)
    int GetRemainingBlockTime(const std::wstring& identifier);
    
    // Изчистване на историята за идентификатор
    void ClearHistory(const std::wstring& identifier);
    
    // Конфигурация
    void SetMaxAttempts(int maxAttempts) { _maxAttempts = maxAttempts; }
    void SetTimeWindow(int seconds) { _timeWindowSeconds = seconds; }
    void SetBlockDuration(int seconds) { _blockDurationSeconds = seconds; }

private:
    RateLimiter();
    ~RateLimiter() = default;
    
    // Забрана на копиране
    RateLimiter(const RateLimiter&) = delete;
    RateLimiter& operator=(const RateLimiter&) = delete;
    
    struct AttemptRecord
    {
        std::chrono::system_clock::time_point timestamp;
        bool success;
    };
    
    struct BlockRecord
    {
        std::chrono::system_clock::time_point blockedUntil;
        int attemptCount;
    };
    
    void CleanupOldAttempts(const std::wstring& identifier);
    void LogSecurityEvent(const std::wstring& identifier, const std::string& event);
    
    std::map<std::wstring, std::vector<AttemptRecord>> _attempts;
    std::map<std::wstring, BlockRecord> _blockedIdentifiers;
    std::mutex _mutex;
    
    // Конфигурация (по подразбиране)
    int _maxAttempts = 5;              // Максимум 5 опита
    int _timeWindowSeconds = 300;      // За 5 минути (300 секунди)
    int _blockDurationSeconds = 900;   // Блокиране за 15 минути (900 секунди)
};
