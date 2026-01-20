#pragma once
#include "pch.h"
#include <string>

class ApiClient
{
public:
    ApiClient(const std::wstring& baseUrl = L"https://ads-auth.nursanbulgaria.com");
    ~ApiClient();

    // Създава нова сесия
    bool CreateSession(std::wstring& sessionId, std::wstring& accessToken);

    // Проверява статуса на сесия
    int GetSessionStatus(const std::wstring& sessionId); // 0=Pending, 1=Approved, 2=Rejected, 3=Expired
    
    // Получава пълната информация за одобрена сесия (username, domain и password)
    bool GetApprovedSessionInfo(const std::wstring& sessionId, std::wstring& username, std::wstring& domain, std::wstring& password);

    // Аутентикира потребител
    bool Authenticate(const std::wstring& accessToken, const std::wstring& username, 
                     const std::wstring& password, const std::wstring& domain);

private:
    std::wstring _baseUrl;
    std::wstring HttpRequest(const std::wstring& method, const std::wstring& path, 
                             const std::wstring& body = L"");
};

