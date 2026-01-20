#pragma once
#include "pch.h"
#include <vector>
#include <thread>
#include <atomic>
#include <functional>

enum class AnimationStyle
{
    None,           // Без анимация
    Pulse,          // Пулсиране (scale)
    FadeBorder,     // Fade in/out на border
    Rotate,         // Ротация на indicator
    Combined        // Комбинация от всички
};

class AnimatedQrCode
{
public:
    AnimatedQrCode();
    ~AnimatedQrCode();
    
    // Генериране на анимирани frames
    void GenerateFrames(const std::wstring& data, 
                       int size = 256, 
                       AnimationStyle style = AnimationStyle::Pulse);
    
    // Стартиране на анимацията
    void StartAnimation();
    
    // Спиране на анимацията
    void StopAnimation();
    
    // Получаване на текущия frame
    HBITMAP GetCurrentFrame();
    
    // Callback при промяна на frame
    void SetFrameChangedCallback(std::function<void(HBITMAP)> callback);
    
    // Конфигурация
    void SetFrameRate(int fps) { _fps = fps; }
    void SetAnimationStyle(AnimationStyle style) { _style = style; }

private:
    void GeneratePulseFrames(const std::wstring& data, int size);
    void GenerateFadeBorderFrames(const std::wstring& data, int size);
    void GenerateRotateFrames(const std::wstring& data, int size);
    void GenerateCombinedFrames(const std::wstring& data, int size);
    
    void AnimationLoop();
    void CleanupFrames();
    
    HBITMAP CreateScaledBitmap(HBITMAP source, float scale);
    HBITMAP CreateBitmapWithBorder(HBITMAP source, int borderWidth, COLORREF borderColor, int alpha);
    HBITMAP CreateBitmapWithIndicator(HBITMAP source, float angle);
    
    std::vector<HBITMAP> _frames;
    int _currentFrameIndex;
    std::thread _animationThread;
    std::atomic<bool> _isAnimating;
    std::mutex _mutex;
    
    int _fps;
    AnimationStyle _style;
    std::function<void(HBITMAP)> _frameChangedCallback;
};
