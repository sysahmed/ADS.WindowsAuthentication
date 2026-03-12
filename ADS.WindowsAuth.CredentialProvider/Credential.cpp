#include "pch.h"
#include "Credential.h"
#include "DebugLogger.h"
#include <string>
#include <sstream>
#include <fstream>

// Дефиниция на глобалната helper функция за debug логове от Credential Provider
void LogCredentialDebug(const std::wstring& msg)
{
    // Опит за използване на системния диск, напр. C:\ADS\Logs
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
        << L"] " << msg << std::endl;
}

// Helper за форматиране на HRESULT/NTSTATUS стойности като hex (за диагностика)
static std::wstring FormatHex(DWORD value)
{
    wchar_t buf[12];
    swprintf_s(buf, 12, L"%08X", value);
    return std::wstring(buf);
}

Credential::Credential() :
    _cRef(1),
    _cpus(CPUS_INVALID),
    _pcpe(NULL),
    _pcpce(NULL),
    _upAdviseContext(0),
    _hQrBitmap(NULL),
    _stopPolling(false),
    _cachedSessionStatus(-1) // Unknown статус по подразбиране
{
    _apiClient = std::make_unique<ApiClient>();
}

Credential::~Credential()
{
    StopPolling();
    
    // Stop retry thread
    if (_retryThread.joinable())
    {
        _retryThread.join();
    }
    
    if (_hQrBitmap)
    {
        DeleteObject(_hQrBitmap);
        _hQrBitmap = NULL;
    }

    if (_pcpe)
    {
        _pcpe->Release();
        _pcpe = NULL;
    }

    if (_pcpce)
    {
        _pcpce->Release();
        _pcpce = NULL;
    }
}

HRESULT Credential::Initialize(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus,
                               ICredentialProviderEvents* pcpe,
                               UINT_PTR upAdviseContext)
{
    // Проверка дали вече е инициализиран - ако да, не правим нищо
    if (!_sessionId.empty() && _cpus == cpus)
    {
        LogCredentialDebug(L"Initialize: Already initialized with sessionId: " + _sessionId.substr(0, 8) + L"..., skipping re-initialization");
        return S_OK;
    }

    _cpus = cpus;
    _upAdviseContext = upAdviseContext;
    
    // Запазваме ICredentialProviderEvents за да можем да тригерираме CredentialsChanged
    if (_pcpe != NULL)
        _pcpe->Release();
    
    _pcpe = pcpe;
    if (_pcpe != NULL)
        _pcpe->AddRef();

    std::wstring scenarioName = (cpus == CPUS_LOGON) ? L"LOGON" : (cpus == CPUS_UNLOCK_WORKSTATION) ? L"UNLOCK" : L"UNKNOWN";
    LogCredentialDebug(L"Initialize called - Scenario: " + scenarioName);

    // Създаване на сесия
    LogCredentialDebug(L"Calling _apiClient->CreateSession...");
    bool sessionCreated = _apiClient->CreateSession(_sessionId, _accessToken);
    LogCredentialDebug(L"_apiClient->CreateSession returned: " + std::wstring(sessionCreated ? L"true" : L"false"));
    LogCredentialDebug(L"SessionId after CreateSession: " + (_sessionId.empty() ? L"EMPTY" : _sessionId.substr(0, 8) + L"..."));
    LogCredentialDebug(L"AccessToken after CreateSession: " + (_accessToken.empty() ? L"EMPTY" : _accessToken.substr(0, 8) + L"..."));
    
    if (sessionCreated)
    {
        LogCredentialDebug(L"Session created successfully");
    }
    else
    {
        LogCredentialDebug(L"Session creation FAILED in Initialize");
    }
    
    // Винаги генерираме QR код - дори и когато сесията не е създадена (Loading QR)
    UpdateQrCode();
    
    if (sessionCreated)
    {
        // Стартиране на polling
        StartPolling();
    }
    else
    {
        // Fallback: Try to create session again after a delay (SSL might need time)
        // Also generate initial QR code
        std::wstring baseUrl = L"https://ads-auth.nursanbulgaria.com";
        
        // Try to read from Registry (override default)
        HKEY hKey = NULL;
        if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\ADS\\WindowsAuth", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
        {
            DWORD size = 0;
            if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, NULL, NULL, &size) == ERROR_SUCCESS && size > 0)
            {
                std::vector<wchar_t> buffer(size / sizeof(wchar_t) + 1);
                DWORD type = 0;
                if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, &type, (LPBYTE)buffer.data(), &size) == ERROR_SUCCESS)
                {
                    if (type == REG_SZ)
                    {
                        baseUrl = buffer.data();
                    }
                }
            }
            RegCloseKey(hKey);
        }
        
        // FIX 3: Увеличен synchronous wait - SSL/TLS може да се инициализира бавно
        LogCredentialDebug(L"Initial session creation failed, retrying after delay...");
        std::this_thread::sleep_for(std::chrono::milliseconds(1500));
        LogCredentialDebug(L"Retrying _apiClient->CreateSession after delay...");
        bool retrySuccess = _apiClient->CreateSession(_sessionId, _accessToken);
        LogCredentialDebug(L"Retry CreateSession returned: " + std::wstring(retrySuccess ? L"true" : L"false"));
        LogCredentialDebug(L"SessionId after retry: " + (_sessionId.empty() ? L"EMPTY" : _sessionId.substr(0, 8) + L"..."));
        LogCredentialDebug(L"AccessToken after retry: " + (_accessToken.empty() ? L"EMPTY" : _accessToken.substr(0, 8) + L"..."));
        
        if (retrySuccess)
        {
            LogCredentialDebug(L"Retry session creation SUCCESS");
            // Success on retry - generate QR code with real token
            UpdateQrCode();
            StartPolling();
            
            // FIX 2: Trigger UI update да покаже новия QR код
            if (_pcpce)
            {
                _pcpce->SetFieldState(this, SFI_QR_CODE, CPFS_DISPLAY_IN_SELECTED_TILE);
            }
        }
        else
        {
            // Still failed - генерираме Loading QR код докато retry thread-ът опитва
            LogCredentialDebug(L"Retry session creation still failed. Generating Loading QR code and starting background retry thread.");
            UpdateQrCode(); // Това ще генерира Loading QR код защото _accessToken е празен
            
            // Start background thread to retry session creation periodically
            _retryThread = std::thread([this]() {
                for (int i = 0; i < 15; i++) // Try 15 times (30 seconds total)
                {
                    std::this_thread::sleep_for(std::chrono::seconds(2));
                    
                    LogCredentialDebug(L"Background retry attempt " + std::to_wstring(i + 1) + L"/15");
                    if (_apiClient->CreateSession(_sessionId, _accessToken))
                    {
                        // Success! Update QR code with real token
                        LogCredentialDebug(L"Background retry session creation SUCCESS");
                        UpdateQrCode();
                        StartPolling();
                        
                        // FIX 2: Trigger UI update след успешен retry
                        if (_pcpce)
                        {
                            _pcpce->SetFieldState(this, SFI_QR_CODE, CPFS_DISPLAY_IN_SELECTED_TILE);
                        }
                        break;
                    }
                    else
                    {
                        LogCredentialDebug(L"Background retry attempt " + std::to_wstring(i + 1) + L" failed");
                    }
                }

                LogCredentialDebug(L"Background retry thread finished without success");
            });
        }
    }

    return S_OK;
}

IFACEMETHODIMP_(ULONG) Credential::AddRef()
{
    return InterlockedIncrement(&_cRef);
}

IFACEMETHODIMP_(ULONG) Credential::Release()
{
    LONG cRef = InterlockedDecrement(&_cRef);
    if (!cRef)
        delete this;
    return cRef;
}

IFACEMETHODIMP Credential::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(Credential, ICredentialProviderCredential),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP Credential::Advise(ICredentialProviderCredentialEvents* pcpce)
{
    if (_pcpce != NULL)
        _pcpce->Release();

    _pcpce = pcpce;

    if (_pcpce != NULL)
        _pcpce->AddRef();

    return S_OK;
}

IFACEMETHODIMP Credential::UnAdvise()
{
    if (_pcpce)
    {
        _pcpce->Release();
        _pcpce = NULL;
    }
    return S_OK;
}

IFACEMETHODIMP Credential::SetSelected(BOOL* pbAutoLogon)
{
    if (!pbAutoLogon)
        return E_INVALIDARG;
    
    // Проверяваме дали сесията е одобрена - ако да, активираме автоматичен login
    // Но само при CPUS_LOGON (рестарт/logout), не при CPUS_UNLOCK_WORKSTATION
    if (_cpus == CPUS_LOGON && !_sessionId.empty())
    {
        int status = _apiClient->GetSessionStatus(_sessionId);
        if (status == 1) // Approved
        {
            LogCredentialDebug(L"SetSelected: Session is approved, enabling auto login");
            *pbAutoLogon = TRUE;
            return S_OK;
        }
    }
    
    *pbAutoLogon = FALSE;
    return S_OK;
}

IFACEMETHODIMP Credential::SetDeselected()
{
    return S_OK;
}

IFACEMETHODIMP Credential::GetFieldState(DWORD dwFieldID, CREDENTIAL_PROVIDER_FIELD_STATE* pcpfs, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* pcpfis)
{
    if (!pcpfs || !pcpfis)
    {
        return E_INVALIDARG;
    }
    
    *pcpfs = CPFS_DISPLAY_IN_SELECTED_TILE;
    *pcpfis = CPFIS_NONE;
    return S_OK;
}

IFACEMETHODIMP Credential::GetStringValue(DWORD dwFieldID, LPWSTR* ppsz)
{
    HRESULT hr = E_INVALIDARG;

    if (ppsz)
    {
        *ppsz = NULL;

        switch (dwFieldID)
        {
        case SFI_TITLE_TEXT:
            hr = SHStrDupW(L"QR Code Authentication", ppsz);
            break;
        case SFI_SUBTITLE_TEXT:
            hr = SHStrDupW(L"Сканирайте QR кода с мобилното приложение", ppsz);
            break;
        }
    }

    return hr;
}

IFACEMETHODIMP Credential::GetBitmapValue(DWORD dwFieldID, HBITMAP* phbmp)
{
    HRESULT hr = E_INVALIDARG;

    if (phbmp)
    {
        *phbmp = NULL;

        if (dwFieldID == SFI_QR_CODE)
        {
            std::lock_guard<std::mutex> lock(_mutex);
            if (_hQrBitmap)
            {
                // Windows ще вземе ownership на bitmap-а и ще го изтрие
                // Затова трябва да върнем копие, не оригинала
                BITMAP bm;
                if (GetObject(_hQrBitmap, sizeof(BITMAP), &bm))
                {
                    HDC hdcScreen = GetDC(NULL);
                    HDC hdcMem = CreateCompatibleDC(hdcScreen);
                    HBITMAP hbmpCopy = CreateCompatibleBitmap(hdcScreen, bm.bmWidth, bm.bmHeight);
                    
                    if (hbmpCopy)
                    {
                        HBITMAP hbmpOld = (HBITMAP)SelectObject(hdcMem, hbmpCopy);
                        
                        HDC hdcSrc = CreateCompatibleDC(hdcScreen);
                        HBITMAP hbmpOldSrc = (HBITMAP)SelectObject(hdcSrc, _hQrBitmap);
                        
                        BitBlt(hdcMem, 0, 0, bm.bmWidth, bm.bmHeight, hdcSrc, 0, 0, SRCCOPY);
                        
                        SelectObject(hdcSrc, hbmpOldSrc);
                        DeleteDC(hdcSrc);
                        
                        SelectObject(hdcMem, hbmpOld);
                        
                        *phbmp = hbmpCopy;
                        hr = S_OK;
                    }
                    
                    DeleteDC(hdcMem);
                    ReleaseDC(NULL, hdcScreen);
                }
            }
        }
    }

    return hr;
}

IFACEMETHODIMP Credential::GetCheckboxValue(DWORD dwFieldID, BOOL* pbChecked, LPWSTR* ppszLabel)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::GetSubmitButtonValue(DWORD dwFieldID, DWORD* pdwAdjacentTo)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::SetStringValue(DWORD dwFieldID, LPCWSTR psz)
{
    return S_OK;
}

IFACEMETHODIMP Credential::SetCheckboxValue(DWORD dwFieldID, BOOL bChecked)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::GetComboBoxValueCount(DWORD dwFieldID, DWORD* pcItems, DWORD* pdwSelectedItem)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::GetComboBoxValueAt(DWORD dwFieldID, DWORD dwItem, LPWSTR* ppszItem)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::SetComboBoxSelectedValue(DWORD dwFieldID, DWORD dwSelectedItem)
{
    return E_NOTIMPL;
}

IFACEMETHODIMP Credential::GetSerialization(
    CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* pcpgsr,
    CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs,
    LPWSTR* ppszOptionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
{
    // Проверка за NULL pointers - всички задължителни параметри
    if (!pcpgsr || !pcpcs)
    {
        LogCredentialDebug(L"GetSerialization: NULL pointer detected - pcpgsr or pcpcs is NULL");
        return E_INVALIDARG;
    }
    
    // Инициализиране на структурата преди използване
    ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));

    std::wstring scenarioName = (_cpus == CPUS_LOGON) ? L"LOGON" : (_cpus == CPUS_UNLOCK_WORKSTATION) ? L"UNLOCK" : L"UNKNOWN";
    LogCredentialDebug(L"GetSerialization called - Scenario: " + scenarioName + L", SessionId: " + (_sessionId.empty() ? L"EMPTY" : _sessionId.substr(0, 8) + L"..."));

    // Проверка дали сесията е одобрена
    int status = _apiClient->GetSessionStatus(_sessionId);
    LogCredentialDebug(L"GetSerialization: Session status = " + std::to_wstring(status));
    
    if (status == 1) // Approved
    {
        // Автоматичен login при CPUS_LOGON (рестарт, logout/login) И при CPUS_UNLOCK_WORKSTATION (unlock)
        LogCredentialDebug(L"Session approved for " + scenarioName + L" scenario - attempting automatic login");
        
        // Сесията е одобрена - получаваме username, domain и password от одобрената сесия
        std::wstring approvedUsername, approvedDomain, approvedPassword;
        if (_apiClient->GetApprovedSessionInfo(_sessionId, approvedUsername, approvedDomain, approvedPassword))
        {
            // Използваме домейна от API-то (каквото е въведено в уеб формата)
            // НЕ презаписваме approvedDomain - може да е "nursan" (domain акаунт) или machine name (локален акаунт)

            LogCredentialDebug(L"Session approved for: " + approvedUsername + L"@" + approvedDomain + L" with password (length: " + std::to_wstring(approvedPassword.length()) + L")");
            
            // Проверка за празни стойности
            if (approvedUsername.empty() || approvedPassword.empty())
            {
                LogCredentialDebug(L"ERROR: Username or password is empty! Username empty: " + std::to_wstring(approvedUsername.empty()) + L", Password empty: " + std::to_wstring(approvedPassword.empty()));
                *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
                return S_OK;
            }
            
            // Използваме CredPackAuthenticationBufferW за да създадем authentication buffer
            // с username и password за автоматичен login
            ULONG authBufferSize = 0;
            BYTE* authBuffer = NULL;
            BOOL result = FALSE;
            DWORD lastError = 0;
            
            // За домейн логин форматираме username като "domain\username"
            std::wstring domainUsername = approvedDomain + L"\\" + approvedUsername;
            LogCredentialDebug(L"Username: " + approvedUsername + L", Domain: " + approvedDomain + L", DomainUsername: " + domainUsername + L", Password length: " + std::to_wstring(approvedPassword.length()));
            
            // Създаваме копия на низовете в променяеми буфери (CredPackAuthenticationBufferW изисква non-const)
            std::vector<WCHAR> usernameBuffer(domainUsername.begin(), domainUsername.end());
            usernameBuffer.push_back(L'\0');
            
            std::vector<WCHAR> passwordBuffer(approvedPassword.begin(), approvedPassword.end());
            passwordBuffer.push_back(L'\0');
            
            LogCredentialDebug(L"Calling CredPackAuthenticationBufferW with CRED_PACK_PROTECTED_CREDENTIALS to get buffer size...");

            // Първо получаваме размера на буфера
            // ВАЖНО: За домейн логин използваме CRED_PACK_PROTECTED_CREDENTIALS (Kerberos/NTLM формат)
            // CRED_PACK_GENERIC_CREDENTIALS дава "The parameter is incorrect" при Windows domain logon
            result = CredPackAuthenticationBufferW(CRED_PACK_PROTECTED_CREDENTIALS,
                usernameBuffer.data(), passwordBuffer.data(), NULL, &authBufferSize);
            
            lastError = GetLastError();
            LogCredentialDebug(L"CredPackAuthenticationBufferW (get size) returned: " + std::to_wstring(result) + L", buffer size: " + std::to_wstring(authBufferSize) + L", last error: " + std::to_wstring(lastError));
            
            // ПРОБЛЕМ: ERROR_INSUFFICIENT_BUFFER (122) е нормално поведение при първото извикване
            // но кодът не прави правилна проверка
            if (lastError == ERROR_INSUFFICIENT_BUFFER && authBufferSize > 0)
            {
                // Запазваме оригиналния размер преди второто извикване
                ULONG originalBufferSize = authBufferSize;
                
                // Заделяме памет за буфера
                authBuffer = (BYTE*)CoTaskMemAlloc(authBufferSize);
                if (authBuffer)
                {
                    LogCredentialDebug(L"Allocated buffer of size: " + std::to_wstring(authBufferSize));
                    
                    // Възстановяваме размера преди второто извикване (функцията може да го промени)
                    authBufferSize = originalBufferSize;
                    
                    // Създаваме authentication buffer
                    // ВАЖНО: За домейн логин използваме CRED_PACK_PROTECTED_CREDENTIALS
                    ULONG actualBufferSize = authBufferSize; // Запазваме размера преди извикването
                    if (CredPackAuthenticationBufferW(CRED_PACK_PROTECTED_CREDENTIALS,
                        usernameBuffer.data(), passwordBuffer.data(), authBuffer, &actualBufferSize))
                    {
                        // Успешно създаден authentication buffer
                        // КРИТИЧНО: Трябва да вземем правилния LSA authentication package ID!
                        // Ако ulAuthenticationPackage е 0 (по подразбиране от ZeroMemory),
                        // Windows ВИНАГИ дава "потребителят не е намерен" / STATUS_NO_SUCH_USER!
                        ULONG ulAuthPackage = 0;
                        {
                            HANDLE hLsa = NULL;
                            // LsaConnectUntrusted работи без специални привилегии - подходящо за Credential Providers
                            NTSTATUS lsaStatus = LsaConnectUntrusted(&hLsa);
                            LogCredentialDebug(L"LsaConnectUntrusted status: 0x" + FormatHex((DWORD)lsaStatus));

                            if (lsaStatus == 0 && hLsa != NULL) // STATUS_SUCCESS = 0
                            {
                                // ВАЖНО: CRED_PACK_PROTECTED_CREDENTIALS е проектиран за Negotiate (SPNEGO), НЕ за директен Kerberos!
                                // Kerberos пакет НЕ разбира CredPack формата → STATUS_INTERNAL_ERROR (0xC00000E5)!
                                // Negotiate автоматично избира между Kerberos (domain акаунти) и NTLM (локални акаунти).
                                CHAR szNegotiate[] = "Negotiate";
                                LSA_STRING lsaPkg;
                                lsaPkg.Buffer = szNegotiate;
                                lsaPkg.Length = (USHORT)strlen(szNegotiate);
                                lsaPkg.MaximumLength = lsaPkg.Length + 1;

                                NTSTATUS pkgStatus = LsaLookupAuthenticationPackage(hLsa, &lsaPkg, &ulAuthPackage);
                                LogCredentialDebug(L"LsaLookupAuthenticationPackage (Negotiate) status: 0x" + FormatHex((DWORD)pkgStatus) + L", packageId: " + std::to_wstring(ulAuthPackage));

                                if (pkgStatus != 0) // Fallback към MSV1_0 (ако Negotiate не е наличен)
                                {
                                    CHAR szMsv[] = "MSV1_0";
                                    lsaPkg.Buffer = szMsv;
                                    lsaPkg.Length = (USHORT)strlen(szMsv);
                                    lsaPkg.MaximumLength = lsaPkg.Length + 1;

                                    pkgStatus = LsaLookupAuthenticationPackage(hLsa, &lsaPkg, &ulAuthPackage);
                                    LogCredentialDebug(L"LsaLookupAuthenticationPackage (MSV1_0) status: 0x" + FormatHex((DWORD)pkgStatus) + L", packageId: " + std::to_wstring(ulAuthPackage));
                                }

                                // Ако packageId все още е 0 след всички опити, логваме предупреждение
                                if (ulAuthPackage == 0)
                                {
                                    LogCredentialDebug(L"WARNING: All packages returned packageId=0! Authentication may fail.");
                                }

                                LsaDeregisterLogonProcess(hLsa);
                            }
                            else
                            {
                                LogCredentialDebug(L"LsaConnectUntrusted FAILED - ulAuthPackage ще е 0! Логинът вероятно ще се провали.");
                            }
                        }
                        LogCredentialDebug(L"Finalized ulAuthenticationPackage: " + std::to_wstring(ulAuthPackage));

                        // Инициализираме структурата правилно
                        ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));

                        // ЗАБЕЛЕЖКА: clsidCredentialProvider се оставя като GUID_NULL (от ZeroMemory)
                        // Не задаваме Windows Password CP CLSID - следваме Microsoft SampleCredentialProvider
                        // КРИТИЧНА ПОПРАВКА: Задаваме правилния auth package - без това "потребителят не е намерен"!
                        pcpcs->ulAuthenticationPackage = ulAuthPackage;
                        pcpcs->rgbSerialization = authBuffer;
                        pcpcs->cbSerialization = actualBufferSize; // Действителният размер от CredPackAuthenticationBufferW

                        *pcpgsr = CPGSR_RETURN_CREDENTIAL_FINISHED;
                        
                        if (ppszOptionalStatusText)
                        {
                            std::wstring message = L"Влизане като " + approvedUsername + L"@nursan...";
                            SHStrDupW(message.c_str(), ppszOptionalStatusText);
                        }
                        
                        if (pcpsiOptionalStatusIcon)
                        {
                            *pcpsiOptionalStatusIcon = CPSI_SUCCESS;
                        }
                        
                        LogCredentialDebug(L"Authentication buffer created successfully, attempting automatic login...");
                        return S_OK;
                    }
                    else
                    {
                        DWORD secondCallError = GetLastError();
                        LogCredentialDebug(L"CredPackAuthenticationBufferW failed on second call, error: " + std::to_wstring(secondCallError));
                        
                        CoTaskMemFree(authBuffer);
                        authBuffer = NULL;
                        
                        // Fallback към ръчно въвеждане на парола
                        LogCredentialDebug(L"Second CredPackAuthenticationBufferW failed, falling back to manual password entry");
                    }
                }
                else
                {
                    LogCredentialDebug(L"Failed to allocate memory for authentication buffer");
                }
            }
            else
            {
                LogCredentialDebug(L"CredPackAuthenticationBufferW failed to get buffer size. Result: " + std::to_wstring(result) + L", Error: " + std::to_wstring(lastError));
            }
            
            // Fallback: Ако CredPackAuthenticationBuffer не работи
            LogCredentialDebug(L"CredPackAuthenticationBufferW failed, showing manual login prompt");
            
            *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
            ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
            
            if (ppszOptionalStatusText)
            {
                std::wstring message = L"Сесията е одобрена за " + approvedUsername + L"@nursan. Моля, въведете паролата.";
                SHStrDupW(message.c_str(), ppszOptionalStatusText);
            }
            
            if (pcpsiOptionalStatusIcon)
            {
                *pcpsiOptionalStatusIcon = CPSI_SUCCESS;
            }
            
            return S_OK;
        }
        else
        {
            LogCredentialDebug(L"Failed to get approved session info");
            *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
            ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
            
            if (ppszOptionalStatusText)
            {
                SHStrDupW(L"Сесията е одобрена, но не мога да получа информация за потребителя.", ppszOptionalStatusText);
            }
            
            if (pcpsiOptionalStatusIcon)
            {
                *pcpsiOptionalStatusIcon = CPSI_WARNING;
            }
            
            return S_OK;
        }
    }
    else if (status == 2) // Rejected
    {
        *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
        ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
        
        if (ppszOptionalStatusText)
        {
            SHStrDupW(L"Аутентикацията е отхвърлена.", ppszOptionalStatusText);
        }
        
        if (pcpsiOptionalStatusIcon)
        {
            *pcpsiOptionalStatusIcon = CPSI_ERROR;
        }
        
        return S_OK;
    }
    else if (status == 3) // Expired
    {
        *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
        ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
        
        if (ppszOptionalStatusText)
        {
            SHStrDupW(L"Сесията е изтекла. Моля, обновете QR кода.", ppszOptionalStatusText);
        }
        
        if (pcpsiOptionalStatusIcon)
        {
            *pcpsiOptionalStatusIcon = CPSI_WARNING;
        }
        
        return S_OK;
    }

    // Все още очаква
    *pcpgsr = CPGSR_NO_CREDENTIAL_NOT_FINISHED;
    ZeroMemory(pcpcs, sizeof(CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION));
    return S_OK;
}

IFACEMETHODIMP Credential::ReportResult(
    NTSTATUS ntsStatus,
    NTSTATUS ntsSubstatus,
    LPWSTR* ppszOptionalStatusText,
    CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon)
{
    // Логваме точния NTSTATUS за диагностика на проблеми с логина
    LogCredentialDebug(L"ReportResult: ntsStatus=0x" + FormatHex((DWORD)ntsStatus) +
                       L", ntsSubstatus=0x" + FormatHex((DWORD)ntsSubstatus));

    // Известни NTSTATUS кодове (помагат за диагностика):
    // 0x00000000 = STATUS_SUCCESS              - логинът е успешен
    // 0xC000006D = STATUS_LOGON_FAILURE        - грешна парола или потребител (общ)
    // 0xC0000064 = STATUS_NO_SUCH_USER         - потребителят не съществува в домейна
    // 0xC000006A = STATUS_WRONG_PASSWORD       - паролата е грешна
    // 0xC0000022 = STATUS_ACCESS_DENIED        - нямате достъп
    // 0xC000015B = STATUS_LOGON_TYPE_NOT_GRANTED - типът на логина не е разрешен
    // 0xC000006C = STATUS_PASSWORD_EXPIRED     - паролата е изтекла
    // 0xC0000234 = STATUS_ACCOUNT_LOCKED_OUT   - акаунтът е заключен
    // 0xC0000193 = STATUS_ACCOUNT_EXPIRED      - акаунтът е изтекъл

    if (ntsStatus == 0)
        LogCredentialDebug(L"ReportResult: Логинът е УСПЕШЕН! (STATUS_SUCCESS)");
    else if (ntsStatus == (NTSTATUS)0xC000006D)
        LogCredentialDebug(L"ReportResult: STATUS_LOGON_FAILURE - неуспешен логин");
    else if (ntsStatus == (NTSTATUS)0xC0000064)
        LogCredentialDebug(L"ReportResult: STATUS_NO_SUCH_USER - потребителят не е намерен в домейна!");
    else if (ntsStatus == (NTSTATUS)0xC000006A)
        LogCredentialDebug(L"ReportResult: STATUS_WRONG_PASSWORD - грешна парола");
    else if (ntsStatus == (NTSTATUS)0xC000015B)
        LogCredentialDebug(L"ReportResult: STATUS_LOGON_TYPE_NOT_GRANTED - типът логин не е разрешен");
    else if (ntsStatus == (NTSTATUS)0xC000006C)
        LogCredentialDebug(L"ReportResult: STATUS_PASSWORD_EXPIRED - паролата е изтекла");
    else if (ntsStatus == (NTSTATUS)0xC0000234)
        LogCredentialDebug(L"ReportResult: STATUS_ACCOUNT_LOCKED_OUT - акаунтът е заключен");
    else
        LogCredentialDebug(L"ReportResult: Непознат NTSTATUS статус");

    return S_OK;
}

IFACEMETHODIMP Credential::CommandLinkClicked(DWORD dwFieldID)
{
    return E_NOTIMPL;
}

void Credential::StartPolling()
{
    _stopPolling = false;
    _pollingThread = std::thread(&Credential::PollingThreadProc, this);
}

void Credential::StopPolling()
{
    _stopPolling = true;
    if (_pollingThread.joinable())
    {
        _pollingThread.join();
    }
}

void Credential::PollingThreadProc()
{
    LogCredentialDebug(L"PollingThreadProc: Started polling for sessionId: " + (_sessionId.empty() ? L"EMPTY" : _sessionId.substr(0, 8) + L"..."));
    
    int pendingCheckCount = 0; // Брояч за да не логваме всеки път при Pending статус
    
    while (!_stopPolling)
    {
        // Увеличен интервал от 2 на 3 секунди за да намалим натоварването
        std::this_thread::sleep_for(std::chrono::seconds(3));

        if (_stopPolling)
        {
            LogCredentialDebug(L"PollingThreadProc: Stop polling requested, exiting...");
            break;
        }

        if (_sessionId.empty())
        {
            LogCredentialDebug(L"PollingThreadProc: SessionId is empty, skipping status check");
            continue;
        }

        // Логваме само на всеки 10-ти път при Pending статус (на всеки ~30 секунди)
        bool shouldLog = (pendingCheckCount % 10 == 0);
        if (shouldLog)
        {
            LogCredentialDebug(L"PollingThreadProc: Checking status for sessionId: " + _sessionId.substr(0, 8) + L"...");
        }
        
        int status = _apiClient->GetSessionStatus(_sessionId);
        
        // Кешираме статуса за бърз достъп в IsSessionApproved()
        _cachedSessionStatus.store(status);
        
        if (status == 1) // Approved
        {
            // Сесията е одобрена - спираме polling веднага
            LogCredentialDebug(L"PollingThreadProc: Session approved - triggering CredentialsChanged for auto login");
            _stopPolling = true; // Спираме polling веднага след одобрение
            
            // Тригерираме CredentialsChanged чрез ICredentialProviderEvents
            // Това ще накара Windows да извика GetCredentialCount отново и ще види че сесията е одобрена
            if (_pcpe)
            {
                LogCredentialDebug(L"PollingThreadProc: Calling CredentialsChanged on ICredentialProviderEvents");
                _pcpe->CredentialsChanged(_upAdviseContext);
                LogCredentialDebug(L"PollingThreadProc: CredentialsChanged called successfully");
            }
            else
            {
                LogCredentialDebug(L"PollingThreadProc: _pcpe is NULL, cannot trigger CredentialsChanged");
            }
            
            // Също така тригерираме UI update чрез CredentialEvents за да обновим QR кода
            if (_pcpce)
            {
                _pcpce->SetFieldState(this, SFI_TITLE_TEXT, CPFS_DISPLAY_IN_SELECTED_TILE);
            }
            
            break; // Излизаме от цикъла веднага
        }
        else if (status == 2) // Rejected
        {
            // Сесията е отхвърлена - спираме polling
            LogCredentialDebug(L"PollingThreadProc: Session rejected - stopping polling");
            _stopPolling = true;
            break;
        }
        else if (status == 0) // Pending
        {
            // Сесията все още чака одобрение - логваме само на всеки 10-ти път
            pendingCheckCount++;
            if (shouldLog)
            {
                LogCredentialDebug(L"PollingThreadProc: Status = Pending (check #" + std::to_wstring(pendingCheckCount) + L")");
            }
        }
        else if (status == 3) // Expired
        {
            LogCredentialDebug(L"PollingThreadProc: Session expired - stopping polling");
            _stopPolling = true;
            break;
        }
        else
        {
            // Непознат статус или грешка (вероятно -1 при грешка)
            if (shouldLog)
            {
                LogCredentialDebug(L"PollingThreadProc: Status = " + std::to_wstring(status) + L" (непознат или грешка)");
            }
        }
    }
    
    LogCredentialDebug(L"PollingThreadProc: Exiting polling thread");
}

void Credential::UpdateQrCode()
{
    LogCredentialDebug(L"UpdateQrCode called");

    // Конструиране на URL за мобилното приложение
    std::wstring baseUrl = L"https://ads-auth.nursanbulgaria.com";
    
    // Четене на URL от Registry/Environment (override default)
    HKEY hKey = NULL;
    if (RegOpenKeyEx(HKEY_LOCAL_MACHINE, L"SOFTWARE\\ADS\\WindowsAuth", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
    {
        DWORD size = 0;
        if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, NULL, NULL, &size) == ERROR_SUCCESS && size > 0)
        {
            std::vector<wchar_t> buffer(size / sizeof(wchar_t) + 1);
            DWORD type = 0;
            if (RegQueryValueEx(hKey, L"ServiceUrl", NULL, &type, (LPBYTE)buffer.data(), &size) == ERROR_SUCCESS)
            {
                if (type == REG_SZ)
                {
                    baseUrl = buffer.data();
                    LogCredentialDebug(L"BaseUrl from registry: " + baseUrl);
                }
            }
        }
        RegCloseKey(hKey);
    }
    
    // Fallback към Environment Variable
    if (baseUrl.empty())
    {
        size_t len = 0;
        _wgetenv_s(&len, NULL, 0, L"ADS_API_URL");
        if (len > 0)
        {
            std::vector<wchar_t> envBuffer(len);
            _wgetenv_s(&len, envBuffer.data(), len, L"ADS_API_URL");
            baseUrl = envBuffer.data();
            LogCredentialDebug(L"BaseUrl from ADS_API_URL env: " + baseUrl);
        }
    }
    
    // Ако все още е празен (не трябва да се случи, но за всеки случай), използваме default
    if (baseUrl.empty())
    {
        baseUrl = L"https://ads-auth.nursanbulgaria.com";
        LogCredentialDebug(L"BaseUrl fallback to default: " + baseUrl);
    }
    
    // Проверка дали токенът е готов
    if (_accessToken.empty())
    {
        LogCredentialDebug(L"AccessToken is empty - generating Loading QR");
        // FIX 1: Показваме "Loading..." QR код вместо празно място
        _hQrBitmap = QrCodeGenerator::GenerateLoadingQrCode(256);
        return;
    }
    
    // Премахване на trailing slash от baseUrl ако има
    if (!baseUrl.empty() && baseUrl.back() == L'/')
    {
        baseUrl.pop_back();
    }
    
    std::wstring qrData = baseUrl + L"/auth?token=" + _accessToken;
    LogCredentialDebug(L"Generating QR with data: " + qrData);
    
    std::lock_guard<std::mutex> lock(_mutex);
    
    if (_hQrBitmap)
    {
        DeleteObject(_hQrBitmap);
        _hQrBitmap = NULL;
    }

    _hQrBitmap = QrCodeGenerator::GenerateQrCodeBitmap(qrData, 256);

    if (_hQrBitmap)
    {
        LogCredentialDebug(L"QR bitmap generated successfully");
    }
    else
    {
        LogCredentialDebug(L"Failed to generate QR bitmap");
    }

    // Забележка: OnFieldStateChanged не е наличен във всички версии на Windows SDK
    // QR кодът ще се покаже при следващо извикване на GetBitmapValue
}

bool Credential::IsSessionApproved() const
{
    if (_sessionId.empty() || _apiClient == nullptr)
        return false;
    // И при логин, и при отключване (unlock) трябва да признаем одобрена сесия
    if (_cpus != CPUS_LOGON && _cpus != CPUS_UNLOCK_WORKSTATION)
        return false;

    // Използваме кеширания статус ако е наличен (одобрен или отхвърлен)
    int cachedStatus = _cachedSessionStatus.load();
    if (cachedStatus == 1) // Approved
    {
        return true;
    }
    if (cachedStatus == 2 || cachedStatus == 3) // Rejected или Expired
    {
        return false;
    }
    
    // Ако кешираният статус е Unknown (-1) или Pending (0), правим HTTP заявка
    int status = _apiClient->GetSessionStatus(_sessionId);
    return (status == 1); // Approved
}

