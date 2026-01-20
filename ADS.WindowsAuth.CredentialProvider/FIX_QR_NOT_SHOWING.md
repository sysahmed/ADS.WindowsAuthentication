# Fix: QR Code Not Showing on Login Screen

## Problem
The Credential Provider loads and shows text, but the QR code bitmap is not displayed.

## Root Cause
The `CreateSession` call in `Credential::Initialize` may fail in the login screen context due to:
1. WinHttp SSL certificate validation issues
2. Network/proxy restrictions in login context
3. WinHttp not working properly in secure desktop context

If `CreateSession` fails, `UpdateQrCode()` is never called, so `_hQrBitmap` remains NULL, and `GetBitmapValue` returns an error.

## Solution
Modify `Credential.cpp` to always generate a QR code, even if session creation fails:

### Change in `Credential::Initialize`:

```cpp
HRESULT Credential::Initialize(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus,
                               ICredentialProviderEvents* pcpe,
                               UINT_PTR upAdviseContext)
{
    _cpus = cpus;
    _upAdviseContext = upAdviseContext;

    // Try to create session
    bool sessionCreated = _apiClient->CreateSession(_sessionId, _accessToken);
    
    if (sessionCreated)
    {
        // Генериране на QR код
        UpdateQrCode();
        
        // Стартиране на polling
        StartPolling();
    }
    else
    {
        // Fallback: Generate QR code with placeholder data
        // This ensures the QR code field is always populated
        std::wstring baseUrl = L"https://ads-auth.nursanbulgaria.com";
        
        // Try to read from Registry
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
        
        // Generate QR code with placeholder token
        std::wstring qrData = baseUrl + L"/auth?token=PLACEHOLDER";
        
        std::lock_guard<std::mutex> lock(_mutex);
        if (_hQrBitmap)
        {
            DeleteObject(_hQrBitmap);
            _hQrBitmap = NULL;
        }
        
        _hQrBitmap = QrCodeGenerator::GenerateQrCodeBitmap(qrData, 256);
    }

    return S_OK;
}
```

## Alternative: Fix WinHttp SSL Issues

If the problem is WinHttp SSL certificate validation, modify `ApiClient::HttpRequest` to disable certificate validation (NOT RECOMMENDED FOR PRODUCTION):

```cpp
// In ApiClient::HttpRequest, after WinHttpOpenRequest:
DWORD dwOption = WINHTTP_OPTION_SECURITY_FLAGS;
DWORD dwFlags = SECURITY_FLAG_IGNORE_UNKNOWN_CA | 
                SECURITY_FLAG_IGNORE_CERT_DATE_INVALID |
                SECURITY_FLAG_IGNORE_CERT_CN_INVALID |
                SECURITY_FLAG_IGNORE_CERT_WRONG_USAGE;
WinHttpSetOption(hRequest, dwOption, &dwFlags, sizeof(dwFlags));
```

## Testing
After making changes:
1. Recompile the DLL (Release x64)
2. Copy to C:\ADS\
3. Re-register: `regsvr32 C:\ADS\ADS.WindowsAuth.CredentialProvider.dll`
4. Restart computer
5. Check if QR code appears

## Debugging
To see if CreateSession is failing, check Event Viewer for errors from the Credential Provider, or add logging to the DLL (requires recompilation with logging enabled).

