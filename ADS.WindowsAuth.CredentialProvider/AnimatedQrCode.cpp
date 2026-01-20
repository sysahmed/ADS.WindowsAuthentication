#include "pch.h"
#include "AnimatedQrCode.h"
#include "QrCodeGenerator.h"
#include <cmath>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

AnimatedQrCode::AnimatedQrCode()
    : _currentFrameIndex(0)
    , _isAnimating(false)
    , _fps(30)
    , _style(AnimationStyle::Pulse)
    , _frameChangedCallback(nullptr)
{
}

AnimatedQrCode::~AnimatedQrCode()
{
    StopAnimation();
    CleanupFrames();
}

void AnimatedQrCode::GenerateFrames(const std::wstring& data, int size, AnimationStyle style)
{
    StopAnimation();
    CleanupFrames();
    
    _style = style;
    
    switch (style)
    {
    case AnimationStyle::Pulse:
        GeneratePulseFrames(data, size);
        break;
    case AnimationStyle::FadeBorder:
        GenerateFadeBorderFrames(data, size);
        break;
    case AnimationStyle::Rotate:
        GenerateRotateFrames(data, size);
        break;
    case AnimationStyle::Combined:
        GenerateCombinedFrames(data, size);
        break;
    case AnimationStyle::None:
    default:
        // Само един frame без анимация
        _frames.push_back(QrCodeGenerator::GenerateQrCodeBitmap(data, size));
        break;
    }
}

void AnimatedQrCode::StartAnimation()
{
    if (_isAnimating || _frames.empty())
    {
        return;
    }
    
    _isAnimating = true;
    _animationThread = std::thread(&AnimatedQrCode::AnimationLoop, this);
}

void AnimatedQrCode::StopAnimation()
{
    if (!_isAnimating)
    {
        return;
    }
    
    _isAnimating = false;
    
    if (_animationThread.joinable())
    {
        _animationThread.join();
    }
}

HBITMAP AnimatedQrCode::GetCurrentFrame()
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    if (_frames.empty())
    {
        return NULL;
    }
    
    return _frames[_currentFrameIndex];
}

void AnimatedQrCode::SetFrameChangedCallback(std::function<void(HBITMAP)> callback)
{
    _frameChangedCallback = callback;
}

void AnimatedQrCode::GeneratePulseFrames(const std::wstring& data, int size)
{
    // Генериране на base QR код
    HBITMAP baseQr = QrCodeGenerator::GenerateQrCodeBitmap(data, size);
    
    // Генериране на 30 frames за плавна анимация (1 секунда при 30 fps)
    const int frameCount = 30;
    const float minScale = 0.90f;  // 90% от оригиналния размер
    const float maxScale = 1.00f;  // 100% от оригиналния размер
    
    for (int i = 0; i < frameCount; i++)
    {
        // Sine wave за плавно пулсиране
        float t = static_cast<float>(i) / frameCount;
        float scale = minScale + (maxScale - minScale) * 
                     (0.5f + 0.5f * std::sin(2.0f * M_PI * t));
        
        HBITMAP scaledFrame = CreateScaledBitmap(baseQr, scale);
        _frames.push_back(scaledFrame);
    }
    
    DeleteObject(baseQr);
}

void AnimatedQrCode::GenerateFadeBorderFrames(const std::wstring& data, int size)
{
    // Генериране на base QR код
    HBITMAP baseQr = QrCodeGenerator::GenerateQrCodeBitmap(data, size);
    
    const int frameCount = 30;
    const int borderWidth = 8;
    const COLORREF borderColor = RGB(0, 120, 215); // Windows blue
    
    for (int i = 0; i < frameCount; i++)
    {
        float t = static_cast<float>(i) / frameCount;
        int alpha = static_cast<int>(128 + 127 * std::sin(2.0f * M_PI * t));
        
        HBITMAP frameWithBorder = CreateBitmapWithBorder(baseQr, borderWidth, borderColor, alpha);
        _frames.push_back(frameWithBorder);
    }
    
    DeleteObject(baseQr);
}

void AnimatedQrCode::GenerateRotateFrames(const std::wstring& data, int size)
{
    // Генериране на base QR код
    HBITMAP baseQr = QrCodeGenerator::GenerateQrCodeBitmap(data, size);
    
    const int frameCount = 60; // 2 секунди при 30 fps
    
    for (int i = 0; i < frameCount; i++)
    {
        float angle = (360.0f * i) / frameCount;
        HBITMAP frameWithIndicator = CreateBitmapWithIndicator(baseQr, angle);
        _frames.push_back(frameWithIndicator);
    }
    
    DeleteObject(baseQr);
}

void AnimatedQrCode::GenerateCombinedFrames(const std::wstring& data, int size)
{
    // Комбинация от pulse и fade border
    HBITMAP baseQr = QrCodeGenerator::GenerateQrCodeBitmap(data, size);
    
    const int frameCount = 30;
    const float minScale = 0.95f;
    const float maxScale = 1.00f;
    const int borderWidth = 6;
    const COLORREF borderColor = RGB(0, 120, 215);
    
    for (int i = 0; i < frameCount; i++)
    {
        float t = static_cast<float>(i) / frameCount;
        
        // Scale
        float scale = minScale + (maxScale - minScale) * 
                     (0.5f + 0.5f * std::sin(2.0f * M_PI * t));
        
        // Alpha
        int alpha = static_cast<int>(128 + 127 * std::sin(2.0f * M_PI * t));
        
        HBITMAP scaledQr = CreateScaledBitmap(baseQr, scale);
        HBITMAP finalFrame = CreateBitmapWithBorder(scaledQr, borderWidth, borderColor, alpha);
        
        _frames.push_back(finalFrame);
        DeleteObject(scaledQr);
    }
    
    DeleteObject(baseQr);
}

void AnimatedQrCode::AnimationLoop()
{
    const int frameDelay = 1000 / _fps; // milliseconds
    
    while (_isAnimating)
    {
        {
            std::lock_guard<std::mutex> lock(_mutex);
            _currentFrameIndex = (_currentFrameIndex + 1) % _frames.size();
            
            if (_frameChangedCallback)
            {
                _frameChangedCallback(_frames[_currentFrameIndex]);
            }
        }
        
        std::this_thread::sleep_for(std::chrono::milliseconds(frameDelay));
    }
}

void AnimatedQrCode::CleanupFrames()
{
    std::lock_guard<std::mutex> lock(_mutex);
    
    for (HBITMAP frame : _frames)
    {
        if (frame)
        {
            DeleteObject(frame);
        }
    }
    
    _frames.clear();
    _currentFrameIndex = 0;
}

HBITMAP AnimatedQrCode::CreateScaledBitmap(HBITMAP source, float scale)
{
    if (!source || scale <= 0.0f)
    {
        return NULL;
    }
    
    BITMAP bm;
    GetObject(source, sizeof(BITMAP), &bm);
    
    int newWidth = static_cast<int>(bm.bmWidth * scale);
    int newHeight = static_cast<int>(bm.bmHeight * scale);
    
    HDC hdcScreen = GetDC(NULL);
    HDC hdcSource = CreateCompatibleDC(hdcScreen);
    HDC hdcDest = CreateCompatibleDC(hdcScreen);
    
    HBITMAP hbmDest = CreateCompatibleBitmap(hdcScreen, bm.bmWidth, bm.bmHeight);
    
    HBITMAP hbmOldSource = (HBITMAP)SelectObject(hdcSource, source);
    HBITMAP hbmOldDest = (HBITMAP)SelectObject(hdcDest, hbmDest);
    
    // Бял background
    RECT rect = { 0, 0, bm.bmWidth, bm.bmHeight };
    FillRect(hdcDest, &rect, (HBRUSH)GetStockObject(WHITE_BRUSH));
    
    // Центриране на scaled изображението
    int offsetX = (bm.bmWidth - newWidth) / 2;
    int offsetY = (bm.bmHeight - newHeight) / 2;
    
    SetStretchBltMode(hdcDest, HALFTONE);
    StretchBlt(hdcDest, offsetX, offsetY, newWidth, newHeight,
               hdcSource, 0, 0, bm.bmWidth, bm.bmHeight, SRCCOPY);
    
    SelectObject(hdcSource, hbmOldSource);
    SelectObject(hdcDest, hbmOldDest);
    DeleteDC(hdcSource);
    DeleteDC(hdcDest);
    ReleaseDC(NULL, hdcScreen);
    
    return hbmDest;
}

HBITMAP AnimatedQrCode::CreateBitmapWithBorder(HBITMAP source, int borderWidth, 
                                               COLORREF borderColor, int alpha)
{
    if (!source)
    {
        return NULL;
    }
    
    BITMAP bm;
    GetObject(source, sizeof(BITMAP), &bm);
    
    HDC hdcScreen = GetDC(NULL);
    HDC hdcSource = CreateCompatibleDC(hdcScreen);
    HDC hdcDest = CreateCompatibleDC(hdcScreen);
    
    HBITMAP hbmDest = CreateCompatibleBitmap(hdcScreen, bm.bmWidth, bm.bmHeight);
    
    HBITMAP hbmOldSource = (HBITMAP)SelectObject(hdcSource, source);
    HBITMAP hbmOldDest = (HBITMAP)SelectObject(hdcDest, hbmDest);
    
    // Копиране на оригиналното изображение
    BitBlt(hdcDest, 0, 0, bm.bmWidth, bm.bmHeight, hdcSource, 0, 0, SRCCOPY);
    
    // Рисуване на border с alpha
    BYTE r = GetRValue(borderColor);
    BYTE g = GetGValue(borderColor);
    BYTE b = GetBValue(borderColor);
    
    HPEN hPen = CreatePen(PS_SOLID, borderWidth, RGB(r, g, b));
    HPEN hOldPen = (HPEN)SelectObject(hdcDest, hPen);
    SelectObject(hdcDest, GetStockObject(NULL_BRUSH));
    
    Rectangle(hdcDest, borderWidth / 2, borderWidth / 2, 
             bm.bmWidth - borderWidth / 2, bm.bmHeight - borderWidth / 2);
    
    SelectObject(hdcDest, hOldPen);
    DeleteObject(hPen);
    
    SelectObject(hdcSource, hbmOldSource);
    SelectObject(hdcDest, hbmOldDest);
    DeleteDC(hdcSource);
    DeleteDC(hdcDest);
    ReleaseDC(NULL, hdcScreen);
    
    return hbmDest;
}

HBITMAP AnimatedQrCode::CreateBitmapWithIndicator(HBITMAP source, float angle)
{
    if (!source)
    {
        return NULL;
    }
    
    BITMAP bm;
    GetObject(source, sizeof(BITMAP), &bm);
    
    HDC hdcScreen = GetDC(NULL);
    HDC hdcSource = CreateCompatibleDC(hdcScreen);
    HDC hdcDest = CreateCompatibleDC(hdcScreen);
    
    HBITMAP hbmDest = CreateCompatibleBitmap(hdcScreen, bm.bmWidth, bm.bmHeight);
    
    HBITMAP hbmOldSource = (HBITMAP)SelectObject(hdcSource, source);
    HBITMAP hbmOldDest = (HBITMAP)SelectObject(hdcDest, hbmDest);
    
    // Копиране на оригиналното изображение
    BitBlt(hdcDest, 0, 0, bm.bmWidth, bm.bmHeight, hdcSource, 0, 0, SRCCOPY);
    
    // Рисуване на rotating indicator (малка стрелка в ъгъла)
    int centerX = bm.bmWidth - 30;
    int centerY = 30;
    int radius = 15;
    
    float radians = angle * M_PI / 180.0f;
    int endX = centerX + static_cast<int>(radius * std::cos(radians));
    int endY = centerY + static_cast<int>(radius * std::sin(radians));
    
    HPEN hPen = CreatePen(PS_SOLID, 3, RGB(0, 120, 215));
    HPEN hOldPen = (HPEN)SelectObject(hdcDest, hPen);
    
    MoveToEx(hdcDest, centerX, centerY, NULL);
    LineTo(hdcDest, endX, endY);
    
    SelectObject(hdcDest, hOldPen);
    DeleteObject(hPen);
    
    SelectObject(hdcSource, hbmOldSource);
    SelectObject(hdcDest, hbmOldDest);
    DeleteDC(hdcSource);
    DeleteDC(hdcDest);
    ReleaseDC(NULL, hdcScreen);
    
    return hbmDest;
}
