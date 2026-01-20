// Custom Action за регистрация на DLL при инсталация
#include <windows.h>
#include <msiquery.h>
#include <string>

#pragma comment(lib, "msi.lib")

extern "C" __declspec(dllexport) UINT __stdcall RegisterDLL(MSIHANDLE hInstall)
{
    HRESULT hr = S_OK;
    UINT er = ERROR_SUCCESS;
    
    hr = WcaInitialize(hInstall, "RegisterDLL");
    ExitOnFailure(hr, "Failed to initialize");
    
    // Получаване на пътя до DLL-а
    TCHAR szDLLPath[MAX_PATH] = { 0 };
    DWORD dwSize = MAX_PATH;
    
    // Пътът е в INSTALLDIR
    MsiGetProperty(hInstall, TEXT("INSTALLDIR"), szDLLPath, &dwSize);
    
    // Добавяне на името на DLL-а
    std::wstring dllPath = std::wstring(szDLLPath) + TEXT("ADS.WindowsAuth.CredentialProvider.dll");
    
    // Регистрация с regsvr32
    SHELLEXECUTEINFO sei = { 0 };
    sei.cbSize = sizeof(SHELLEXECUTEINFO);
    sei.fMask = SEE_MASK_NOCLOSEPROCESS;
    sei.lpVerb = TEXT("runas"); // Администраторски права
    sei.lpFile = TEXT("regsvr32.exe");
    sei.lpParameters = (L"\"" + dllPath + L"\"").c_str();
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

