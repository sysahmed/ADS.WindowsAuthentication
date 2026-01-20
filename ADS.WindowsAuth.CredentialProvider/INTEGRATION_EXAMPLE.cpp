// Пример за интеграция на новите features в Credential.cpp
// Този файл показва как да използваш новите класове

#include "pch.h"
#include "Credential.h"
#include "LanguageManager.h"
#include "RateLimiter.h"
#include "AnimatedQrCode.h"

// В конструктора на Credential:
Credential::Credential()
{
    // ... existing code ...
    
    // Инициализация на Language Manager
    LanguageManager::GetInstance().DetectSystemLanguage();
    
    // Инициализация на Animated QR Code
    _animatedQr = std::make_unique<AnimatedQrCode>();
    _animatedQr->SetFrameRate(30);
    _animatedQr->SetAnimationStyle(AnimationStyle::Combined);
}

// В GetStringValue метода:
IFACEMETHODIMP Credential::GetStringValue(DWORD dwFieldID, LPWSTR* ppsz)
{
    HRESULT hr = E_INVALIDARG;
    
    if (ppsz)
    {
        switch (dwFieldID)
        {
        case SFI_TITLE_TEXT:
            {
                // Използване на multi-language support
                std::wstring title = LanguageManager::GetInstance().GetString("title");
                hr = SHStrDupW(title.c_str(), ppsz);
            }
            break;
            
        case SFI_SUBTITLE_TEXT:
            {
                // Проверка за rate limiting
                std::wstring machineId = GetMachineId();
                RateLimiter& rateLimiter = RateLimiter::GetInstance();
                
                if (!rateLimiter.IsAllowed(machineId))
                {
                    int remainingTime = rateLimiter.GetRemainingBlockTime(machineId);
                    int minutes = remainingTime / 60;
                    
                    std::wstring errorMsg = LanguageManager::GetInstance().GetString("error_rate_limit");
                    // Replace {0} with minutes
                    size_t pos = errorMsg.find(L"{0}");
                    if (pos != std::wstring::npos)
                    {
                        errorMsg.replace(pos, 3, std::to_wstring(minutes));
                    }
                    
                    hr = SHStrDupW(errorMsg.c_str(), ppsz);
                }
                else
                {
                    std::wstring subtitle = LanguageManager::GetInstance().GetString("subtitle");
                    hr = SHStrDupW(subtitle.c_str(), ppsz);
                }
            }
            break;
        }
    }
    
    return hr;
}

// В GetBitmapValue метода:
IFACEMETHODIMP Credential::GetBitmapValue(DWORD dwFieldID, HBITMAP* phbmp)
{
    HRESULT hr = E_INVALIDARG;
    
    if (phbmp && dwFieldID == SFI_QR_CODE)
    {
        // Използване на animated QR code
        if (_animatedQr)
        {
            HBITMAP currentFrame = _animatedQr->GetCurrentFrame();
            if (currentFrame)
            {
                *phbmp = currentFrame;
                hr = S_OK;
            }
        }
    }
    
    return hr;
}

// В SetSelected метода (когато tile е избран):
IFACEMETHODIMP Credential::SetSelected(BOOL* pbAutoLogon)
{
    *pbAutoLogon = FALSE;
    
    // Проверка за rate limiting
    std::wstring machineId = GetMachineId();
    RateLimiter& rateLimiter = RateLimiter::GetInstance();
    
    if (!rateLimiter.IsAllowed(machineId))
    {
        // Блокиран - не позволяваме опит
        return S_OK;
    }
    
    // Създаване на сесия
    if (_apiClient->CreateSession(_sessionId, _accessToken))
    {
        // Генериране на QR код URL
        std::wstring qrData = L"ads-auth://login?session=" + _sessionId;
        
        // Генериране на animated frames
        _animatedQr->GenerateFrames(qrData, 256, AnimationStyle::Combined);
        
        // Стартиране на анимацията
        _animatedQr->StartAnimation();
        
        // Callback за обновяване на UI при промяна на frame
        _animatedQr->SetFrameChangedCallback([this](HBITMAP newFrame) {
            if (_pcpce)
            {
                _pcpce->SetFieldBitmap(this, SFI_QR_CODE, newFrame);
            }
        });
        
        // Стартиране на polling
        StartPolling();
    }
    else
    {
        // Грешка при създаване на сесия
        rateLimiter.RecordAttempt(machineId, false);
    }
    
    return S_OK;
}

// В PollingThreadProc метода:
void Credential::PollingThreadProc()
{
    std::wstring machineId = GetMachineId();
    RateLimiter& rateLimiter = RateLimiter::GetInstance();
    
    while (!_stopPolling)
    {
        int status = _apiClient->GetSessionStatus(_sessionId);
        
        switch (status)
        {
        case 1: // Approved
            {
                // Успешна автентикация
                rateLimiter.RecordAttempt(machineId, true);
                
                // Спиране на анимацията
                _animatedQr->StopAnimation();
                
                // Обновяване на текста
                if (_pcpce)
                {
                    std::wstring successMsg = LanguageManager::GetInstance().GetString("status_approved");
                    _pcpce->SetFieldString(this, SFI_SUBTITLE_TEXT, successMsg.c_str());
                }
                
                // Trigger login
                if (_pcpce)
                {
                    _pcpce->OnCreatingWindow(NULL);
                }
                
                _stopPolling = true;
            }
            break;
            
        case 2: // Rejected
            {
                // Неуспешна автентикация
                rateLimiter.RecordAttempt(machineId, false);
                
                // Спиране на анимацията
                _animatedQr->StopAnimation();
                
                // Обновяване на текста
                if (_pcpce)
                {
                    std::wstring errorMsg = LanguageManager::GetInstance().GetString("status_rejected");
                    _pcpce->SetFieldString(this, SFI_SUBTITLE_TEXT, errorMsg.c_str());
                }
                
                _stopPolling = true;
            }
            break;
            
        case 3: // Expired
            {
                // Изтекла сесия
                rateLimiter.RecordAttempt(machineId, false);
                
                // Спиране на анимацията
                _animatedQr->StopAnimation();
                
                // Обновяване на текста
                if (_pcpce)
                {
                    std::wstring expiredMsg = LanguageManager::GetInstance().GetString("status_expired");
                    _pcpce->SetFieldString(this, SFI_SUBTITLE_TEXT, expiredMsg.c_str());
                }
                
                _stopPolling = true;
            }
            break;
            
        case 0: // Pending
        default:
            // Продължаваме да чакаме
            std::this_thread::sleep_for(std::chrono::seconds(2));
            break;
        }
    }
}

// Helper функция за получаване на Machine ID
std::wstring Credential::GetMachineId()
{
    WCHAR computerName[MAX_COMPUTERNAME_LENGTH + 1];
    DWORD size = sizeof(computerName) / sizeof(computerName[0]);
    
    if (GetComputerName(computerName, &size))
    {
        return std::wstring(computerName);
    }
    
    return L"UNKNOWN";
}

// В SetDeselected метода:
IFACEMETHODIMP Credential::SetDeselected()
{
    // Спиране на анимацията
    if (_animatedQr)
    {
        _animatedQr->StopAnimation();
    }
    
    // Спиране на polling
    StopPolling();
    
    return S_OK;
}
