#include "pch.h"
#include "ClassFactory.h"
#include "DebugLogger.h"
#include <strsafe.h>

HMODULE g_hModule = NULL;
LONG g_cRef = 0;

// GUID за Credential Provider
// Уникален GUID генериран на: 2025-12-07
// {3E879088-249C-4C83-85B6-834A3A9C6D12}
static const CLSID CLSID_CredentialProvider = 
{ 0x3E879088, 0x249C, 0x4C83, { 0x85, 0xB6, 0x83, 0x4A, 0x3A, 0x9C, 0x6D, 0x12 } };

void DllAddRef()
{
    InterlockedIncrement(&g_cRef);
}

void DllRelease()
{
    InterlockedDecrement(&g_cRef);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD dwReason, LPVOID lpReserved)
{
    switch (dwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        LogCredentialDebug(L"[DLL] DllMain: DLL_PROCESS_ATTACH - DLL зареден!");
        break;
    case DLL_PROCESS_DETACH:
        LogCredentialDebug(L"[DLL] DllMain: DLL_PROCESS_DETACH - DLL разтоварен!");
        break;
    }
    return TRUE;
}

STDAPI DllCanUnloadNow()
{
    return (g_cRef == 0) ? S_OK : S_FALSE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    HRESULT hr = CLASS_E_CLASSNOTAVAILABLE;

    WCHAR szCLSID[MAX_PATH];
    StringFromGUID2(rclsid, szCLSID, ARRAYSIZE(szCLSID));
    LogCredentialDebug(L"[DLL] DllGetClassObject called with CLSID: " + std::wstring(szCLSID));

    if (IsEqualCLSID(rclsid, CLSID_CredentialProvider))
    {
        LogCredentialDebug(L"[DLL] DllGetClassObject: CLSID matches! Creating ClassFactory...");
        hr = E_OUTOFMEMORY;

        ClassFactory* pFactory = new ClassFactory();
        if (pFactory)
        {
            hr = pFactory->QueryInterface(riid, ppv);
            pFactory->Release();
            if (SUCCEEDED(hr))
            {
                LogCredentialDebug(L"[DLL] DllGetClassObject: ClassFactory created successfully!");
            }
            else
            {
                LogCredentialDebug(L"[DLL] DllGetClassObject: QueryInterface failed with HRESULT: " + std::to_wstring(hr));
            }
        }
        else
        {
            LogCredentialDebug(L"[DLL] DllGetClassObject: Failed to create ClassFactory - out of memory!");
        }
    }
    else
    {
        LogCredentialDebug(L"[DLL] DllGetClassObject: CLSID does NOT match!");
    }

    return hr;
}

// Регистрация на Credential Provider в Windows Registry
STDAPI DllRegisterServer()
{
    HRESULT hr = S_OK;
    WCHAR szCLSID[MAX_PATH];
    WCHAR szSubKey[MAX_PATH];
    
    // Конвертиране на GUID в string
    StringFromGUID2(CLSID_CredentialProvider, szCLSID, ARRAYSIZE(szCLSID));
    
    // Регистрация на CLSID
    StringCchPrintf(szSubKey, ARRAYSIZE(szSubKey), L"CLSID\\%s", szCLSID);
    
    HKEY hKey;
    LONG lResult = RegCreateKeyEx(HKEY_CLASSES_ROOT, szSubKey, 0, NULL, 
        REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL);
    
    if (lResult == ERROR_SUCCESS)
    {
        RegSetValueEx(hKey, NULL, 0, REG_SZ, 
            (BYTE*)L"ADS Windows Auth Credential Provider", 
            (DWORD)((wcslen(L"ADS Windows Auth Credential Provider") + 1) * sizeof(WCHAR)));
        RegCloseKey(hKey);
    }
    else
    {
        hr = HRESULT_FROM_WIN32(lResult);
    }
    
    // Регистрация на InprocServer32
    if (SUCCEEDED(hr))
    {
        StringCchPrintf(szSubKey, ARRAYSIZE(szSubKey), L"CLSID\\%s\\InprocServer32", szCLSID);
        
        lResult = RegCreateKeyEx(HKEY_CLASSES_ROOT, szSubKey, 0, NULL, 
            REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL);
        
        if (lResult == ERROR_SUCCESS)
        {
            WCHAR szModule[MAX_PATH];
            GetModuleFileName(g_hModule, szModule, ARRAYSIZE(szModule));
            
            RegSetValueEx(hKey, NULL, 0, REG_SZ, 
                (BYTE*)szModule, 
                (DWORD)((wcslen(szModule) + 1) * sizeof(WCHAR)));
            
            RegSetValueEx(hKey, L"ThreadingModel", 0, REG_SZ, 
                (BYTE*)L"Apartment", 
                (DWORD)((wcslen(L"Apartment") + 1) * sizeof(WCHAR)));
            
            RegCloseKey(hKey);
        }
        else
        {
            hr = HRESULT_FROM_WIN32(lResult);
        }
    }
    
    // Регистрация като Credential Provider
    if (SUCCEEDED(hr))
    {
        StringCchPrintf(szSubKey, ARRAYSIZE(szSubKey), 
            L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Authentication\\Credential Providers\\%s", 
            szCLSID);
        
        lResult = RegCreateKeyEx(HKEY_LOCAL_MACHINE, szSubKey, 0, NULL, 
            REG_OPTION_NON_VOLATILE, KEY_WRITE, NULL, &hKey, NULL);
        
        if (lResult == ERROR_SUCCESS)
        {
            RegSetValueEx(hKey, NULL, 0, REG_SZ, 
                (BYTE*)L"ADS Windows Auth Credential Provider", 
                (DWORD)((wcslen(L"ADS Windows Auth Credential Provider") + 1) * sizeof(WCHAR)));
            RegCloseKey(hKey);
        }
        else
        {
            hr = HRESULT_FROM_WIN32(lResult);
        }
    }
    
    return hr;
}

// Деинсталация на Credential Provider от Windows Registry
STDAPI DllUnregisterServer()
{
    HRESULT hr = S_OK;
    WCHAR szCLSID[MAX_PATH];
    WCHAR szSubKey[MAX_PATH];
    
    // Конвертиране на GUID в string
    StringFromGUID2(CLSID_CredentialProvider, szCLSID, ARRAYSIZE(szCLSID));
    
    // Изтриване на Credential Provider регистрация
    StringCchPrintf(szSubKey, ARRAYSIZE(szSubKey), 
        L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Authentication\\Credential Providers\\%s", 
        szCLSID);
    
    LONG lResult = RegDeleteTree(HKEY_LOCAL_MACHINE, szSubKey);
    if (lResult != ERROR_SUCCESS && lResult != ERROR_FILE_NOT_FOUND)
    {
        hr = HRESULT_FROM_WIN32(lResult);
    }
    
    // Изтриване на CLSID регистрация
    StringCchPrintf(szSubKey, ARRAYSIZE(szSubKey), L"CLSID\\%s", szCLSID);
    lResult = RegDeleteTree(HKEY_CLASSES_ROOT, szSubKey);
    if (lResult != ERROR_SUCCESS && lResult != ERROR_FILE_NOT_FOUND)
    {
        hr = HRESULT_FROM_WIN32(lResult);
    }
    
    return hr;
}

