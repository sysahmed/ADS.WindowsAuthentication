#include "pch.h"
#include "QrCodeGenerator.h"
#include "qrcodegen.hpp"
#include <vector>
#include <algorithm>
#include <string>

HBITMAP QrCodeGenerator::GenerateQrCodeBitmap(const std::wstring& data, int size)
{
    if (data.empty() || size <= 0)
    {
        return NULL;
    }

    // Конвертиране на wstring към string (UTF-8)
    std::string utf8Data;
    int len = WideCharToMultiByte(CP_UTF8, 0, data.c_str(), -1, NULL, 0, NULL, NULL);
    if (len <= 0)
    {
        return NULL;
    }
    
    utf8Data.resize(len - 1); // -1 защото WideCharToMultiByte включва null terminator
    WideCharToMultiByte(CP_UTF8, 0, data.c_str(), -1, &utf8Data[0], len, NULL, NULL);

    try
    {
        // Генериране на QR код с qrcodegen библиотеката
        // Използваме MEDIUM error correction за баланс между капацитет и надеждност
        qrcodegen::QrCode qr = qrcodegen::QrCode::encodeText(utf8Data.c_str(), qrcodegen::QrCode::Ecc::MEDIUM);
        
        int qrSize = qr.getSize(); // Размер на QR код матрицата (обикновено 21 за Version 1)
        
        if (qrSize <= 0)
        {
            return NULL;
        }

        // Създаване на bitmap
        HDC hdc = GetDC(NULL);
        HDC hMemDC = CreateCompatibleDC(hdc);
        
        BITMAPINFO bmi = { 0 };
        bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bmi.bmiHeader.biWidth = size;
        bmi.bmiHeader.biHeight = -size; // Top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;
        bmi.bmiHeader.biCompression = BI_RGB;

        void* pBits = NULL;
        HBITMAP hBitmap = CreateDIBSection(hMemDC, &bmi, DIB_RGB_COLORS, &pBits, NULL, 0);

        if (!hBitmap)
        {
            DeleteDC(hMemDC);
            ReleaseDC(NULL, hdc);
            return NULL;
        }

        SelectObject(hMemDC, hBitmap);

        // Бял фон
        RECT rect = { 0, 0, size, size };
        HBRUSH hWhiteBrush = CreateSolidBrush(RGB(255, 255, 255));
        FillRect(hMemDC, &rect, hWhiteBrush);
        DeleteObject(hWhiteBrush);

        // Изчисляване на размера на модула с padding (quiet zone)
        // QR кодовете трябва да имат поне 4 модула padding от всяка страна
        int paddingModules = 4;
        int totalModules = qrSize + (paddingModules * 2);
        int moduleSize = size / totalModules;
        
        // Центриране на QR кода
        int padding = (size - (qrSize * moduleSize)) / 2;
        int startX = padding;
        int startY = padding;

        // Рисуване на QR код модули
        HBRUSH hBlackBrush = CreateSolidBrush(RGB(0, 0, 0));
        
        for (int y = 0; y < qrSize; y++)
        {
            for (int x = 0; x < qrSize; x++)
            {
                // qrcodegen използва (x, y) координати, където (0,0) е горен ляв ъгъл
                if (qr.getModule(x, y))
                {
                    int pixelX = startX + x * moduleSize;
                    int pixelY = startY + y * moduleSize;
                    RECT moduleRect = { pixelX, pixelY, pixelX + moduleSize, pixelY + moduleSize };
                    FillRect(hMemDC, &moduleRect, hBlackBrush);
                }
            }
        }

        DeleteObject(hBlackBrush);
        DeleteDC(hMemDC);
        ReleaseDC(NULL, hdc);

        return hBitmap;
    }
    catch (...)
    {
        // Ако има грешка при генериране на QR кода (напр. данните са твърде дълги)
        return NULL;
    }
}

HBITMAP QrCodeGenerator::GenerateLoadingQrCode(int size)
{
    // Генериране на QR код с текста "LOADING..."
    // Това ще покаже временен QR код докато токенът се зарежда
    return GenerateQrCodeBitmap(L"LOADING...", size);
}

