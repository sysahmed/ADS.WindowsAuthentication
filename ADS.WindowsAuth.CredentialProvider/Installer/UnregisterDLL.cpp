// Custom Action за отмяна на регистрация при деинсталация
#include <windows.h>
#include <msiquery.h>
#include <string>

#pragma comment(lib, "msi.lib")

extern "C" __declspec(dllexport) UINT __stdcall UnregisterDLL(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    
    hr = WcaInitialize(hInstall, "UnregisterDLL");
    ExitOnFailure(hr, "Failed to initialize");
    
    // Получаване на пътя до DLL-а
    TCHAR szDLLPath[MAX_PATH] = { 0 };
    DWORD dwSize = MAX_PATH;
    
    MsiGetProperty(hInstall, TEXT("INSTALLDIR"), szDLLPath, &dwSize);
    
    std::wstring dllPath = std::wstring(szDLLPath) + TEXT("ADS.WindowsAuth.CredentialProvider.dll");
    
    // Отмяна на регистрация с regsvr32 /u
    SHELLEXECUTEINFO sei = { 0 };
    sei.cbSize = sizeof(SHELLEXECUTEINFO);
    sei.fMask = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = TEXT("runas");
    sei.lpFile = TEXT("regsvr32.exe");
    sei.lpParameters = (L"/u \"" + dllPath + L"\"").c_str();
    sei.nShow = SW_HIDE;
    
    if (ShellExecuteEx(&sei))
    {
        WaitForSingleObject(sei.hProcess, INFINITE);
        CloseHandle(sei.hProcess);
    }
    
LExit:
    er = SUCCEEDED(hr) ? ERROR_SUCCESS : ERROR_INSTALL_FAILURE;
    return WcaFinalize(er);
}

