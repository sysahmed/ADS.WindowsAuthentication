#pragma once
#include "pch.h"
#include "ApiClient.h"
#include "QrCodeGenerator.h"

// Field IDs
#define SFI_QR_CODE           0
#define SFI_TITLE_TEXT        1
#define SFI_SUBTITLE_TEXT     2
#define SFI_NUM_FIELDS        3

class Credential : public ICredentialProviderCredential
{
public:
    Credential();
    virtual ~Credential();

    HRESULT Initialize(CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, 
                     ICredentialProviderEvents* pcpe, 
                     UINT_PTR upAdviseContext);

    // --- IUnknown методи ---
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // --- ICredentialProviderCredential методи ---
    IFACEMETHODIMP Advise(ICredentialProviderCredentialEvents* pcpce) override;
    IFACEMETHODIMP UnAdvise() override;
    IFACEMETHODIMP SetSelected(BOOL* pbAutoLogon) override;
    IFACEMETHODIMP SetDeselected() override;
    IFACEMETHODIMP GetFieldState(DWORD dwFieldID, CREDENTIAL_PROVIDER_FIELD_STATE* pcpfs, CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE* pcpfis) override;
    IFACEMETHODIMP GetStringValue(DWORD dwFieldID, LPWSTR* ppsz) override;
    IFACEMETHODIMP GetBitmapValue(DWORD dwFieldID, HBITMAP* phbmp) override;
    IFACEMETHODIMP GetCheckboxValue(DWORD dwFieldID, BOOL* pbChecked, LPWSTR* ppszLabel) override;
    IFACEMETHODIMP GetSubmitButtonValue(DWORD dwFieldID, DWORD* pdwAdjacentTo) override;
    IFACEMETHODIMP GetComboBoxValueCount(DWORD dwFieldID, DWORD* pcItems, DWORD* pdwSelectedItem) override;
    IFACEMETHODIMP GetComboBoxValueAt(DWORD dwFieldID, DWORD dwItem, LPWSTR* ppszItem) override;
    IFACEMETHODIMP SetStringValue(DWORD dwFieldID, LPCWSTR psz) override;
    IFACEMETHODIMP SetCheckboxValue(DWORD dwFieldID, BOOL bChecked) override;
    IFACEMETHODIMP SetComboBoxSelectedValue(DWORD dwFieldID, DWORD dwSelectedItem) override;
    IFACEMETHODIMP CommandLinkClicked(DWORD dwFieldID) override;

    // Правилната сигнатура за ICredentialProviderCredential
    IFACEMETHODIMP GetSerialization(
        CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE* pcpgsr,
        CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION* pcpcs,
        LPWSTR* ppszOptionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon
    ) override;

    IFACEMETHODIMP ReportResult(
        NTSTATUS ntsStatus,
        NTSTATUS ntsSubstatus,
        LPWSTR* ppszOptionalStatusText,
        CREDENTIAL_PROVIDER_STATUS_ICON* pcpsiOptionalStatusIcon
    ) override;

    // Публичен метод за проверка дали сесията е одобрена (използва се от CredentialProvider)
    bool IsSessionApproved() const;
    
    // Публичен метод за спиране на polling (използва се при изтриване на Credential)
    void StopPolling();

private:
    LONG _cRef;
    CREDENTIAL_PROVIDER_USAGE_SCENARIO _cpus;
    ICredentialProviderEvents* _pcpe; // Credential Provider Events (за тригериране на CredentialsChanged)
    ICredentialProviderCredentialEvents* _pcpce; // Credential Events (за UI updates)
    UINT_PTR _upAdviseContext;
    
    std::unique_ptr<ApiClient> _apiClient;
    std::wstring _sessionId;
    std::wstring _accessToken;
    HBITMAP _hQrBitmap;
    
    std::thread _pollingThread;
    std::thread _retryThread;
    std::atomic<bool> _stopPolling;
    std::atomic<int> _cachedSessionStatus; // Кеширан статус на сесията (-1=Unknown, 0=Pending, 1=Approved, 2=Rejected, 3=Expired)
    std::mutex _mutex;
    
    void StartPolling();
    void PollingThreadProc();
    void UpdateQrCode();
};
