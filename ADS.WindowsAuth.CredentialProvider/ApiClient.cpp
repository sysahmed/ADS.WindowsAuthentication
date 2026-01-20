#include "pch.h"
#include "ApiClient.h"
#include <sstream>
#include <iomanip>
#include <fstream>
#include <shlobj.h>

ApiClient::ApiClient(const std::wstring& baseUrl) : _baseUrl(baseUrl)
{
    // Четене на URL от environment variable или registry ако е празен
    if (_baseUrl.empty())
    {
        DWORD size = 0;
        HKEY hKey;
        if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\ADS\\WindowsAuth", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
        {
            if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, NULL, NULL, &size) == ERROR_SUCCESS)
            {
                std::vector<wchar_t> buffer(size / sizeof(wchar_t));
                if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, NULL, (LPBYTE)buffer.data(), &size) == ERROR_SUCCESS)
                {
                    _baseUrl = buffer.data();
                }
            }
            RegCloseKey(hKey);
        }
        
        // Fallback към environment variable
        if (_baseUrl.empty())
        {
            size_t len = 0;
            _wgetenv_s(&len, NULL, 0, L"ADS_API_URL");
            if (len > 0)
            {
                std::vector<wchar_t> envBuffer(len);
                _wgetenv_s(&len, envBuffer.data(), len, L"ADS_API_URL");
                _baseUrl = envBuffer.data();
            }
        }
        
        // Default fallback - production URL
        if (_baseUrl.empty())
        {
            _baseUrl = L"https://ads-auth.nursanbulgaria.com";
        }
    }
}

ApiClient::~ApiClient()
{
}

// Helper function to log debug messages (same as in Credential.cpp)
void LogApiClientDebug(const std::wstring& message)
{
    // Използваме същия път като LogCredentialDebug: C:\ADS\Logs\ADS_CredentialProvider_QR.log
    wchar_t systemDrive[MAX_PATH] = { 0 };
    DWORD len = GetEnvironmentVariableW(L"SystemDrive", systemDrive, MAX_PATH);
    
    std::wstring root = (len > 0) ? std::wstring(systemDrive) : std::wstring(L"C:");
    std::wstring adsDir = root + L"\\ADS";
    std::wstring logsDir = adsDir + L"\\Logs";
    
    // Създаване на директории (ако ги няма) - игнорираме грешките
    CreateDirectoryW(adsDir.c_str(), NULL);
    CreateDirectoryW(logsDir.c_str(), NULL);
    
    std::wstring logPath = logsDir + L"\\ADS_CredentialProvider_QR.log";
    
    std::wofstream logFile(logPath, std::ios::app);
    if (!logFile.is_open())
    {
        return;
    }
    
    SYSTEMTIME st;
    GetLocalTime(&st);
    
    logFile << L"["
        << st.wYear << L"-"
        << st.wMonth << L"-"
        << st.wDay << L" "
        << st.wHour << L":"
        << st.wMinute << L":"
        << st.wSecond << L"."
        << st.wMilliseconds
        << L"] [ApiClient] " << message << std::endl;
}

std::wstring ApiClient::HttpRequest(const std::wstring& method, const std::wstring& path, const std::wstring& body)
{
    HINTERNET hSession = NULL;
    HINTERNET hConnect = NULL;
    HINTERNET hRequest = NULL;
    std::wstring result;

    // Проверка дали _baseUrl е валиден
    if (_baseUrl.empty())
    {
        LogApiClientDebug(L"HttpRequest: _baseUrl is empty! Cannot make request.");
        return result;
    }

    std::wstring fullUrl = _baseUrl + path;
    LogApiClientDebug(L"HttpRequest called: " + method + L" " + fullUrl);
    LogApiClientDebug(L"BaseUrl: " + _baseUrl);

    try
    {
        // Използваме WINHTTP_ACCESS_TYPE_NO_PROXY за да избегнем проблеми с proxy
        // и WINHTTP_NO_PROXY_BYPASS за да не използваме proxy изобщо
        hSession = WinHttpOpen(L"ADS.WindowsAuth.CredentialProvider/1.0",
            WINHTTP_ACCESS_TYPE_NO_PROXY,
            WINHTTP_NO_PROXY_NAME,
            WINHTTP_NO_PROXY_BYPASS, 0);

        if (!hSession)
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpOpen failed with error: " << error;
            LogApiClientDebug(ss.str());
            
            // Retry с WINHTTP_ACCESS_TYPE_DEFAULT_PROXY ако първият опит не успее
            hSession = WinHttpOpen(L"ADS.WindowsAuth.CredentialProvider/1.0",
                WINHTTP_ACCESS_TYPE_DEFAULT_PROXY,
                WINHTTP_NO_PROXY_NAME,
                WINHTTP_NO_PROXY_BYPASS, 0);
            
            if (!hSession)
            {
                DWORD error2 = GetLastError();
                std::wstringstream ss2;
                ss2 << L"WinHttpOpen retry also failed with error: " << error2;
                LogApiClientDebug(ss2.str());
                return result;
            }
            else
            {
                LogApiClientDebug(L"WinHttpOpen succeeded on retry with DEFAULT_PROXY");
            }
        }
        else
        {
            LogApiClientDebug(L"WinHttpOpen succeeded");
        }

        URL_COMPONENTS urlComp = { 0 };
        urlComp.dwStructSize = sizeof(urlComp);
        urlComp.dwSchemeLength = (DWORD)-1;
        urlComp.dwHostNameLength = (DWORD)-1;
        urlComp.dwUrlPathLength = (DWORD)-1;

        
        std::wstring hostName, urlPath;

        if (WinHttpCrackUrl(fullUrl.c_str(), (DWORD)fullUrl.length(), 0, &urlComp))
        {
            hostName.assign(urlComp.lpszHostName, urlComp.dwHostNameLength);
            urlPath.assign(urlComp.lpszUrlPath, urlComp.dwUrlPathLength);
            LogApiClientDebug(L"WinHttpCrackUrl succeeded - Host: " + hostName + L", Path: " + urlPath + L", Port: " + std::to_wstring(urlComp.nPort));
        }
        else
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpCrackUrl failed with error: " << error << L" for URL: " << fullUrl;
            LogApiClientDebug(ss.str());
            WinHttpCloseHandle(hSession);
            return result;
        }

        hConnect = WinHttpConnect(hSession, hostName.c_str(), urlComp.nPort, 0);
        if (!hConnect)
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpConnect failed with error: " << error << L" for host: " << hostName << L":" << urlComp.nPort;
            LogApiClientDebug(ss.str());
            WinHttpCloseHandle(hSession);
            return result;
        }
        LogApiClientDebug(L"WinHttpConnect succeeded");

        DWORD dwFlags = WINHTTP_FLAG_REFRESH;
        if (urlComp.nScheme == INTERNET_SCHEME_HTTPS)
            dwFlags |= WINHTTP_FLAG_SECURE;

        hRequest = WinHttpOpenRequest(hConnect, method.c_str(), urlPath.c_str(),
            NULL, WINHTTP_NO_REFERER, WINHTTP_DEFAULT_ACCEPT_TYPES, dwFlags);

        if (!hRequest)
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpOpenRequest failed with error: " << error;
            LogApiClientDebug(ss.str());
            WinHttpCloseHandle(hConnect);
            WinHttpCloseHandle(hSession);
            return result;
        }
        LogApiClientDebug(L"WinHttpOpenRequest succeeded");

        // Disable SSL certificate validation for HTTPS (needed in login context)
        if (urlComp.nScheme == INTERNET_SCHEME_HTTPS)
        {
            DWORD dwOption = WINHTTP_OPTION_SECURITY_FLAGS;
            DWORD dwFlagsOption = SECURITY_FLAG_IGNORE_UNKNOWN_CA |
                                 SECURITY_FLAG_IGNORE_CERT_DATE_INVALID |
                                 SECURITY_FLAG_IGNORE_CERT_CN_INVALID |
                                 SECURITY_FLAG_IGNORE_CERT_WRONG_USAGE;
            WinHttpSetOption(hRequest, dwOption, &dwFlagsOption, sizeof(dwFlagsOption));
        }

        if (method == L"POST" && !body.empty())
        {
            std::wstring headers = L"Content-Type: application/json\r\n";
            WinHttpAddRequestHeaders(hRequest, headers.c_str(), (DWORD)headers.length(), WINHTTP_ADDREQ_FLAG_ADD);
        }

        if (!WinHttpSendRequest(hRequest, WINHTTP_NO_ADDITIONAL_HEADERS, 0,
            (LPVOID)(body.empty() ? NULL : body.c_str()),
            body.empty() ? 0 : (DWORD)body.length() * sizeof(WCHAR),
            body.empty() ? 0 : (DWORD)body.length() * sizeof(WCHAR), 0))
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpSendRequest failed with error: " << error;
            LogApiClientDebug(ss.str());
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            WinHttpCloseHandle(hSession);
            return result;
        }
        LogApiClientDebug(L"WinHttpSendRequest succeeded");

        if (!WinHttpReceiveResponse(hRequest, NULL))
        {
            DWORD error = GetLastError();
            std::wstringstream ss;
            ss << L"WinHttpReceiveResponse failed with error: " << error;
            LogApiClientDebug(ss.str());
            WinHttpCloseHandle(hRequest);
            WinHttpCloseHandle(hConnect);
            WinHttpCloseHandle(hSession);
            return result;
        }
        LogApiClientDebug(L"WinHttpReceiveResponse succeeded");

        DWORD dwStatusCode = 0;
        DWORD dwStatusCodeSize = sizeof(dwStatusCode);
        WinHttpQueryHeaders(hRequest, WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
            WINHTTP_HEADER_NAME_BY_INDEX, &dwStatusCode, &dwStatusCodeSize, WINHTTP_NO_HEADER_INDEX);

        std::wstringstream statusSs;
        statusSs << L"HTTP Status Code: " << dwStatusCode;
        LogApiClientDebug(statusSs.str());

        if (dwStatusCode == 200)
        {
            // Четене на всички данни (може да са на части)
            std::vector<BYTE> allData;
            DWORD dwTotalDownloaded = 0;
            
            do
            {
                DWORD dwSize = 0;
                if (!WinHttpQueryDataAvailable(hRequest, &dwSize))
                {
                    break;
                }
                
                if (dwSize == 0)
                {
                    break;
                }
                
                std::vector<BYTE> buffer(dwSize);
                DWORD dwDownloaded = 0;
                
                if (WinHttpReadData(hRequest, buffer.data(), dwSize, &dwDownloaded))
                {
                    if (dwDownloaded > 0)
                    {
                        allData.insert(allData.end(), buffer.begin(), buffer.begin() + dwDownloaded);
                        dwTotalDownloaded += dwDownloaded;
                    }
                }
                else
                {
                    DWORD error = GetLastError();
                    if (error != ERROR_IO_PENDING && error != 0)
                    {
                        std::wstringstream ss;
                        ss << L"WinHttpReadData failed with error: " << error;
                        LogApiClientDebug(ss.str());
                    }
                    break;
                }
            } while (true);
            
            // Конвертиране от UTF-8 към UTF-16
            if (dwTotalDownloaded > 0)
            {
                int wideLen = MultiByteToWideChar(CP_UTF8, 0, (LPCCH)allData.data(), dwTotalDownloaded, NULL, 0);
                if (wideLen > 0)
                {
                    std::vector<WCHAR> wideBuffer(wideLen);
                    MultiByteToWideChar(CP_UTF8, 0, (LPCCH)allData.data(), dwTotalDownloaded, wideBuffer.data(), wideLen);
                    result.assign(wideBuffer.data(), wideLen);
                    
                    std::wstringstream ss;
                    ss << L"Response received: " << dwTotalDownloaded << L" bytes, " << result.length() << L" wide characters";
                    LogApiClientDebug(ss.str());
                    
                    // Маскиране на паролите преди логване
                    std::wstring sanitizedResult = result;
                    size_t passwordPos = sanitizedResult.find(L"\"password\":\"");
                    if (passwordPos != std::wstring::npos)
                    {
                        size_t passwordStart = passwordPos + 12; // "password":" = 12 символа
                        size_t passwordEnd = sanitizedResult.find(L"\"", passwordStart);
                        if (passwordEnd != std::wstring::npos)
                        {
                            // Заменяме паролата с "***"
                            sanitizedResult.replace(passwordStart, passwordEnd - passwordStart, L"***");
                        }
                    }
                    
                    size_t previewLen = sanitizedResult.length() > 200 ? 200 : sanitizedResult.length();
                    LogApiClientDebug(L"Response preview: " + sanitizedResult.substr(0, previewLen));
                }
                else
                {
                    LogApiClientDebug(L"Failed to convert UTF-8 to UTF-16");
                }
            }
            else
            {
                LogApiClientDebug(L"No data available in response");
            }
        }
        else
        {
            std::wstringstream ss;
            ss << L"HTTP Status Code is not 200: " << dwStatusCode;
            LogApiClientDebug(ss.str());
        }

        WinHttpCloseHandle(hRequest);
        WinHttpCloseHandle(hConnect);
        WinHttpCloseHandle(hSession);
    }
    catch (...)
    {
        if (hRequest) WinHttpCloseHandle(hRequest);
        if (hConnect) WinHttpCloseHandle(hConnect);
        if (hSession) WinHttpCloseHandle(hSession);
    }

    return result;
}

bool ApiClient::CreateSession(std::wstring& sessionId, std::wstring& accessToken)
{
    LogApiClientDebug(L"CreateSession called");
    std::wstring response = HttpRequest(L"POST", L"/api/Auth/session");
    
    if (response.empty())
    {
        LogApiClientDebug(L"CreateSession: Response is empty");
        return false;
    }
    
    LogApiClientDebug(L"CreateSession: Response received, length: " + std::to_wstring(response.length()));

    // Парсване на JSON отговор (опростено)
    size_t pos = response.find(L"\"sessionId\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 13; // "sessionId":" = 13 символа
        size_t end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            sessionId = response.substr(pos, end - pos);
            LogApiClientDebug(L"Parsed sessionId: " + sessionId);
        }
        else
        {
            LogApiClientDebug(L"Failed to find end quote for sessionId");
        }
    }
    else
    {
        LogApiClientDebug(L"Failed to find \"sessionId\":\" in response");
    }

    pos = response.find(L"\"accessToken\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 15; // "accessToken":" = 15 символа
        size_t end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            accessToken = response.substr(pos, end - pos);
            LogApiClientDebug(L"Parsed accessToken: " + accessToken.substr(0, 8) + L"...");
        }
        else
        {
            LogApiClientDebug(L"Failed to find end quote for accessToken");
        }
    }
    else
    {
        LogApiClientDebug(L"Failed to find \"accessToken\":\" in response");
    }

    bool success = !sessionId.empty() && !accessToken.empty();
    if (success)
    {
        LogApiClientDebug(L"CreateSession: SUCCESS - SessionId: " + sessionId + L", AccessToken: " + accessToken.substr(0, 8) + L"...");
    }
    else
    {
        LogApiClientDebug(L"CreateSession: FAILED - SessionId empty: " + std::to_wstring(sessionId.empty()) + L", AccessToken empty: " + std::to_wstring(accessToken.empty()));
        size_t previewLen = response.length() > 500 ? 500 : response.length();
        LogApiClientDebug(L"CreateSession: Response was: " + response.substr(0, previewLen));
    }
    return success;
}

int ApiClient::GetSessionStatus(const std::wstring& sessionId)
{
    std::wstring path = L"/api/Auth/session/" + sessionId + L"/status";
    std::wstring response = HttpRequest(L"GET", path);

    if (response.empty())
        return 0; // Pending

    // Парсване на статус
    size_t pos = response.find(L"\"status\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 10;
        size_t end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            std::wstring status = response.substr(pos, end - pos);
            if (status == L"Approved") return 1;
            if (status == L"Rejected") return 2;
            if (status == L"Expired") return 3;
        }
    }

    return 0; // Pending
}

bool ApiClient::GetApprovedSessionInfo(const std::wstring& sessionId, std::wstring& username, std::wstring& domain, std::wstring& password)
{
    std::wstring path = L"/api/Auth/session/" + sessionId + L"/status";
    std::wstring response = HttpRequest(L"GET", path);

    if (response.empty())
        return false;

    // Парсване на статус
    size_t pos = response.find(L"\"status\":\"");
    if (pos == std::wstring::npos)
        return false;
    
    pos += 10;
    size_t end = response.find(L"\"", pos);
    if (end == std::wstring::npos)
        return false;
    
    std::wstring status = response.substr(pos, end - pos);
    if (status != L"Approved")
        return false;

    // Парсване на username
    pos = response.find(L"\"username\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 12; // "username":" = 12 символа
        end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            username = response.substr(pos, end - pos);
        }
    }

    // Парсване на domain
    pos = response.find(L"\"domain\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 10; // "domain":" = 10 символа
        end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            domain = response.substr(pos, end - pos);
        }
    }

    // Парсване на password
    pos = response.find(L"\"password\":\"");
    if (pos != std::wstring::npos)
    {
        pos += 12; // "password":" = 12 символа
        end = response.find(L"\"", pos);
        if (end != std::wstring::npos)
        {
            password = response.substr(pos, end - pos);
        }
    }

    return !username.empty() && !domain.empty() && !password.empty();
}

bool ApiClient::Authenticate(const std::wstring& accessToken, const std::wstring& username,
                            const std::wstring& password, const std::wstring& domain)
{
    std::wstringstream json;
    json << L"{"
         << L"\"accessToken\":\"" << accessToken << L"\","
         << L"\"username\":\"" << username << L"\","
         << L"\"password\":\"" << password << L"\","
         << L"\"domain\":\"" << domain << L"\""
         << L"}";

    std::wstring response = HttpRequest(L"POST", L"/api/Auth/authenticate", json.str());
    
    if (response.empty())
        return false;

    // Проверка за success
    return response.find(L"\"success\":true") != std::wstring::npos;
}

