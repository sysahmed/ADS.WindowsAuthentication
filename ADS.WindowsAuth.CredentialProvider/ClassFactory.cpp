#include "pch.h"
#include "ClassFactory.h"

ClassFactory::ClassFactory() : _cRef(1)
{
    DllAddRef();
}

ClassFactory::~ClassFactory()
{
    DllRelease();
}

ULONG ClassFactory::AddRef()
{
    return InterlockedIncrement(&_cRef);
}

ULONG ClassFactory::Release()
{
    LONG cRef = InterlockedDecrement(&_cRef);
    if (!cRef)
        delete this;
    return cRef;
}

HRESULT ClassFactory::QueryInterface(REFIID riid, void** ppv)
{
    static const QITAB qit[] =
    {
        QITABENT(ClassFactory, IClassFactory),
        { 0 },
    };
    return QISearch(this, qit, riid, ppv);
}

HRESULT ClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv)
{
    HRESULT hr = CLASS_E_NOAGGREGATION;

    if (pUnkOuter == NULL)
    {
        hr = E_OUTOFMEMORY;

        CredentialProvider* pProvider = new CredentialProvider();
        if (pProvider)
        {
            hr = pProvider->QueryInterface(riid, ppv);
            pProvider->Release();
        }
    }

    return hr;
}

HRESULT ClassFactory::LockServer(BOOL fLock)
{
    if (fLock)
        DllAddRef();
    else
        DllRelease();
    return S_OK;
}

