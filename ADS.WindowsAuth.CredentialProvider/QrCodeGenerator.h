#pragma once
#include "pch.h"

class QrCodeGenerator
{
public:
    // Генерира QR код като HBITMAP използвайки qrcodegen библиотеката
    static HBITMAP GenerateQrCodeBitmap(const std::wstring& data, int size = 256);
    
    // Генерира "Loading..." placeholder QR код
    static HBITMAP GenerateLoadingQrCode(int size = 256);
};

