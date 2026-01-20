#pragma once
#include "pch.h"
#include "CredentialProvider.h"

class ClassFactory : public IClassFactory
{
public:
    ClassFactory();
    virtual ~ClassFactory();

    // IUnknown
    IFACEMETHODIMP_(ULONG) AddRef();
    IFACEMETHODIMP_(ULONG) Release();
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv);

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv);
    IFACEMETHODIMP LockServer(BOOL fLock);

private:
    LONG _cRef;
};

