#include "pch.h"
#include "CredentialProvider.h"
#include "Credential.h"
#include "DebugLogger.h"

CredentialProvider::CredentialProvider() :
    _cRef(1),
    _cpus(CPUS_INVALID),
    _pcpe(NULL),
    _upAdviseContext(0),
    _pCredential(NULL),
    _dwFieldIDCount(0),
    _rgFieldDescriptors(NULL)
{
    DllAddRef();
}

CredentialProvider::~CredentialProvider()
{
    if (_pCredential)
    {
        _pCredential->Release();
        _pCredential = NULL;
    }

    if (_rgFieldDescriptors)
    {
        CoTaskMemFree(_rgFieldDescriptors);
        _rgFieldDescriptors = NULL;
    }

    DllRelease();
}

ULONG CredentialProvider::AddRef()
{
    return InterlockedIncrement(&_cRef);
}

ULONG CredentialProvider::Release()
{
    LONG cRef = InterlockedDecrement(&_cRef);
    if (!cRef)
        delete this;
    return cRef;
}

HRESULT CredentialProvider::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(CredentialProvider, ICredentialProvider),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

HRESULT CredentialProvider::SetUsageScenario(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, DWORD dwFlags)
{
    HRESULT hr = E_INVALIDARG;

    std::wstring scenarioName = (cpus == CPUS_LOGON) ? L"CPUS_LOGON" : (cpus == CPUS_UNLOCK_WORKSTATION) ? L"CPUS_UNLOCK_WORKSTATION" : L"UNKNOWN";
    
    LogCredentialDebug(L"CredentialProvider::SetUsageScenario called - Scenario: " + scenarioName + L", Flags: " + std::to_wstring(dwFlags));

    if ((cpus == CPUS_LOGON) || (cpus == CPUS_UNLOCK_WORKSTATION))
    {
        _cpus = cpus;
        LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Scenario is valid, creating Credential...");
        
        // Спираме старата Credential преди да създадем нова
        if (_pCredential != NULL)
        {
            LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Stopping old Credential polling thread...");
            _pCredential->StopPolling();
            delete _pCredential;
            _pCredential = NULL;
            LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Old Credential deleted");
        }
        
        // Създаваме credential-а по-рано за да можем да проверяваме статуса в GetCredentialCount
        _pCredential = new Credential();
        if (_pCredential)
        {
            LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Credential created successfully!");
            // Initialize ще се извика отново в GetCredentialAt, но тук го създаваме по-рано
            // за да имаме достъп до него в GetCredentialCount
        }
        else
        {
            LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Failed to create Credential!");
        }
        
        hr = S_OK;
        LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Returning S_OK");
    }
    else
    {
        LogCredentialDebug(L"CredentialProvider::SetUsageScenario: Invalid scenario, returning E_INVALIDARG");
    }

    return hr;
}

HRESULT CredentialProvider::SetSerialization(const CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs)
{
    return S_OK;
}

HRESULT CredentialProvider::Advise(ICredentialProviderEvents* pcpe, UINT_PTR upAdviseContext)
{
    if (_pcpe != NULL)
        _pcpe->Release();

    _pcpe = pcpe;
    _upAdviseContext = upAdviseContext;

    if (_pcpe != NULL)
        _pcpe->AddRef();

    return S_OK;
}

HRESULT CredentialProvider::UnAdvise()
{
    if (_pcpe)
    {
        _pcpe->Release();
        _pcpe = NULL;
    }
    _upAdviseContext = 0;
    return S_OK;
}

HRESULT CredentialProvider::GetFieldDescriptorCount(DWORD* pdwCount)
{
    *pdwCount = SFI_NUM_FIELDS;
    return S_OK;
}

HRESULT CredentialProvider::GetFieldDescriptorAt(DWORD dwIndex, CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR** ppcpfd)
{
    HRESULT hr = E_INVALIDARG;

    if ((dwIndex < SFI_NUM_FIELDS) && ppcpfd)
    {
        *ppcpfd = (CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR*)CoTaskMemAlloc(sizeof(CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR));
        if (*ppcpfd)
        {
            (*ppcpfd)->dwFieldID = dwIndex;
            (*ppcpfd)->cpft = CPFT_LARGE_TEXT;
            (*ppcpfd)->pszLabel = NULL;
            (*ppcpfd)->guidFieldType = GUID_NULL;

            switch (dwIndex)
            {
            case SFI_QR_CODE:
                (*ppcpfd)->cpft = CPFT_TILE_IMAGE;
                (*ppcpfd)->pszLabel = NULL;
                (*ppcpfd)->guidFieldType = GUID_NULL; // CPFG_CREDENTIAL_PROVIDER_LOGO не е наличен във всички версии
                break;
            case SFI_TITLE_TEXT:
                (*ppcpfd)->cpft = CPFT_LARGE_TEXT;
                SHStrDupW(L"QR Code Authentication", &((*ppcpfd)->pszLabel));
                (*ppcpfd)->guidFieldType = GUID_NULL; // CPFG_CREDENTIAL_PROVIDER_LABEL не е наличен във всички версии
                break;
            case SFI_SUBTITLE_TEXT:
                (*ppcpfd)->cpft = CPFT_SMALL_TEXT;
                SHStrDupW(L"Сканирайте QR кода с мобилното приложение", &((*ppcpfd)->pszLabel));
                (*ppcpfd)->guidFieldType = GUID_NULL; // CPFG_CREDENTIAL_PROVIDER_LABEL не е наличен във всички версии
                break;
            }

            hr = S_OK;
        }
        else
        {
            hr = E_OUTOFMEMORY;
        }
    }

    return hr;
}

HRESULT CredentialProvider::GetCredentialCount(DWORD* pdwCount, DWORD* pdwDefault, BOOL* pbAutoLogonWithDefault)
{
    *pdwCount = 1;
    *pdwDefault = 0;
    *pbAutoLogonWithDefault = FALSE;
    
    LogCredentialDebug(L"CredentialProvider::GetCredentialCount called - _pCredential: " + std::wstring(_pCredential ? L"exists" : L"NULL") + L", _cpus: " + std::to_wstring(_cpus));
    
    // Проверяваме дали credential-ът вече е създаден и дали сесията е одобрена
    // Ако да, активираме автоматичен login за CPUS_LOGON и CPUS_UNLOCK_WORKSTATION
    if (_pCredential != NULL && (_cpus == CPUS_LOGON || _cpus == CPUS_UNLOCK_WORKSTATION))
    {
        // Проверяваме дали сесията е одобрена
        bool isApproved = _pCredential->IsSessionApproved();
        LogCredentialDebug(L"CredentialProvider::GetCredentialCount: IsSessionApproved = " + std::wstring(isApproved ? L"true" : L"false"));
        
        if (isApproved)
        {
            *pbAutoLogonWithDefault = TRUE; // Активираме автоматичен login
            LogCredentialDebug(L"CredentialProvider::GetCredentialCount: Setting pbAutoLogonWithDefault = TRUE for scenario " + std::to_wstring(_cpus));
        }
        else
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialCount: Session not approved yet, pbAutoLogonWithDefault = FALSE");
        }
    }
    else
    {
        if (_pCredential == NULL)
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialCount: _pCredential is NULL");
        }
        if (_cpus != CPUS_LOGON && _cpus != CPUS_UNLOCK_WORKSTATION)
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialCount: _cpus is not CPUS_LOGON or CPUS_UNLOCK_WORKSTATION (value: " + std::to_wstring(_cpus) + L")");
        }
    }
    
    return S_OK;
}

HRESULT CredentialProvider::GetCredentialAt(DWORD dwIndex, ICredentialProviderCredential** ppcpc)
{
    HRESULT hr = E_INVALIDARG;

    LogCredentialDebug(L"CredentialProvider::GetCredentialAt called - Index: " + std::to_wstring(dwIndex));

    if ((dwIndex == 0) && ppcpc)
    {
        if (_pCredential == NULL)
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Creating new Credential...");
            _pCredential = new Credential();
            if (!_pCredential)
            {
                LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Failed to create Credential - out of memory!");
                return E_OUTOFMEMORY;
            }
        }

        // ВАЖНО: Initialize трябва да се извиква ВИНАГИ, дори и когато credential-ът вече съществува
        // защото той създава сесията и генерира QR кода
        LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Calling Initialize (credential may already exist but needs initialization)...");
        hr = _pCredential->Initialize(_cpus, _pcpe, _upAdviseContext);
        if (FAILED(hr))
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Initialize failed with HRESULT: " + std::to_wstring(hr));
            // Не изтриваме credential-а тук, защото може да е създаден в SetUsageScenario
            // Просто връщаме грешката
        }
        else
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Initialize succeeded!");
        }

        if (SUCCEEDED(hr))
        {
            LogCredentialDebug(L"CredentialProvider::GetCredentialAt: QueryInterface for ICredentialProviderCredential...");
            hr = _pCredential->QueryInterface(IID_PPV_ARGS(ppcpc));
            if (SUCCEEDED(hr))
            {
                LogCredentialDebug(L"CredentialProvider::GetCredentialAt: QueryInterface succeeded!");
            }
            else
            {
                LogCredentialDebug(L"CredentialProvider::GetCredentialAt: QueryInterface failed with HRESULT: " + std::to_wstring(hr));
            }
        }
    }
    else
    {
        LogCredentialDebug(L"CredentialProvider::GetCredentialAt: Invalid parameters - dwIndex: " + std::to_wstring(dwIndex) + L", ppcpc: " + (ppcpc ? L"valid" : L"NULL"));
    }

    return hr;
}

