using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ADS.WindowsAuth.RemoteDesktopHost.Services;

/// <summary>
/// Заснема екрана чрез DXGI Desktop Duplication (без NuGet зависимости, raw COM P/Invoke).
/// Работи в процеси стартирани с CreateProcessAsUser, за разлика от GDI BitBlt.
/// GDI BitBlt е запазен като fallback.
/// </summary>
public class ScreenCaptureService : IDisposable
{
    private readonly int _quality;
    private readonly EncoderParameters _encoderParams;
    private readonly ImageCodecInfo _jpegCodec;
    private readonly Action<string>? _logInfo;

    // DXGI COM objects (raw IntPtr)
    private IntPtr _d3dDevice   = IntPtr.Zero;
    private IntPtr _d3dContext  = IntPtr.Zero;
    private IntPtr _duplication = IntPtr.Zero;
    private bool _dxgiReady;
    private bool _dxgiFailed;

    // GDI fallback state
    private bool _desktopAttached;
    private static IntPtr _hDesktop = IntPtr.Zero;
    private bool _diagLogged;

    private const int DXGI_ERROR_WAIT_TIMEOUT = unchecked((int)0x887A0027);
    private const int DXGI_ERROR_ACCESS_LOST  = unchecked((int)0x887A0026);
    private const int D3D11_SDK_VERSION       = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;

    private static readonly Guid IID_IDXGIDevice    = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
    private static readonly Guid IID_IDXGIOutput1   = new("00CDDEA8-939B-4B83-A340-A685226666CC");
    private static readonly Guid IID_ID3D11Texture2D = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");

    public ScreenCaptureService(int quality, Action<string>? logInfo = null)
    {
        _quality = Math.Clamp(quality, 10, 100);
        _logInfo = logInfo;

        _jpegCodec = ImageCodecInfo.GetImageDecoders()
            .First(c => c.FormatID == ImageFormat.Jpeg.Guid);

        _encoderParams = new EncoderParameters(1);
        _encoderParams.Param[0] = new EncoderParameter(
            System.Drawing.Imaging.Encoder.Quality, (long)_quality);
    }

    /// <summary>Returns JPEG bytes, or empty array if no new frame (DXGI timeout – skip).</summary>
    public byte[] CaptureScreen()
    {
        if (!_dxgiFailed)
        {
            if (!_dxgiReady) InitDxgi();
            if (_dxgiReady)
            {
                var frame = CaptureDxgi();
                return frame ?? Array.Empty<byte>(); // null = timeout (no new frame)
            }
        }
        return CaptureGdi();
    }

    // ─── DXGI Desktop Duplication (raw COM) ─────────────────────────────────

    private void InitDxgi()
    {
        try
        {
            int hr = D3D11CreateDevice(
                IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
                IntPtr.Zero, 0, D3D11_SDK_VERSION,
                out _d3dDevice, IntPtr.Zero, out _d3dContext);

            if (hr < 0 || _d3dDevice == IntPtr.Zero)
            {
                _logInfo?.Invoke($"[DXGI] D3D11CreateDevice фейлна: 0x{(uint)hr:X8}. Fallback GDI.");
                _dxgiFailed = true; return;
            }

            // QI ID3D11Device → IDXGIDevice
            var iid = IID_IDXGIDevice;
            hr = COMQi(_d3dDevice, ref iid, out IntPtr dxgiDevice);
            if (hr < 0) { _dxgiFailed = true; return; }

            // IDXGIDevice::GetAdapter (vtable slot 7)
            hr = VtCall<GetAdapterFn>(dxgiDevice, 7)(dxgiDevice, out IntPtr adapter);
            COMRelease(dxgiDevice);
            if (hr < 0) { _dxgiFailed = true; return; }

            // IDXGIAdapter::EnumOutputs(0) (vtable slot 7)
            hr = VtCall<EnumOutputsFn>(adapter, 7)(adapter, 0u, out IntPtr output);
            COMRelease(adapter);
            if (hr < 0) { _dxgiFailed = true; return; }

            // QI IDXGIOutput → IDXGIOutput1
            iid = IID_IDXGIOutput1;
            hr = COMQi(output, ref iid, out IntPtr output1);
            COMRelease(output);
            if (hr < 0)
            {
                _logInfo?.Invoke("[DXGI] IDXGIOutput1 не е наличен. Fallback GDI.");
                _dxgiFailed = true; return;
            }

            // IDXGIOutput1::DuplicateOutput (vtable slot 22)
            hr = VtCall<DuplicateOutputFn>(output1, 22)(output1, _d3dDevice, out _duplication);
            COMRelease(output1);
            if (hr < 0)
            {
                _logInfo?.Invoke($"[DXGI] DuplicateOutput фейлна: 0x{(uint)hr:X8}. Fallback GDI.");
                _dxgiFailed = true; return;
            }

            _dxgiReady = true;
            _logInfo?.Invoke("[DXGI] Desktop Duplication готова.");
        }
        catch (Exception ex)
        {
            _logInfo?.Invoke($"[DXGI] Init грешка: {ex.Message}. Fallback GDI.");
            ReleaseDxgi();
            _dxgiFailed = true;
        }
    }

    private unsafe byte[]? CaptureDxgi()
    {
        if (_duplication == IntPtr.Zero) return null;

        IntPtr resource = IntPtr.Zero;
        DXGI_OUTDUPL_FRAME_INFO fi = default;

        int hr = VtCall<AcquireNextFrameFn>(_duplication, 8)(_duplication, 100u, out fi, out resource);

        if (hr == DXGI_ERROR_WAIT_TIMEOUT) return null;
        if (hr == DXGI_ERROR_ACCESS_LOST)
        {
            _logInfo?.Invoke("[DXGI] ACCESS_LOST – реинициализация.");
            ReleaseDxgi(); _dxgiReady = false;
            return null;
        }
        if (hr < 0) return null;

        try
        {
            // QI IDXGIResource → ID3D11Texture2D
            var iid = IID_ID3D11Texture2D;
            if (COMQi(resource, ref iid, out IntPtr texture) < 0) return null;

            try
            {
                // Get texture dimensions
                VtCall<GetTexDescFn>(texture, 9)(texture, out D3D11_TEXTURE2D_DESC desc);

                // Create CPU-readable staging texture
                var sdesc = desc;
                sdesc.Usage          = 3;       // D3D11_USAGE_STAGING
                sdesc.BindFlags      = 0;
                sdesc.CPUAccessFlags = 0x20000; // D3D11_CPU_ACCESS_READ
                sdesc.MiscFlags      = 0;
                sdesc.MipLevels      = 1;
                sdesc.ArraySize      = 1;

                hr = VtCall<CreateTex2DFn>(_d3dDevice, 5)(_d3dDevice, ref sdesc, IntPtr.Zero, out IntPtr staging);
                if (hr < 0) return null;

                try
                {
                    // CopyResource (slot 47)
                    VtCall<CopyResourceFn>(_d3dContext, 47)(_d3dContext, staging, texture);

                    // Map (slot 14)
                    hr = VtCall<MapFn>(_d3dContext, 14)(
                        _d3dContext, staging, 0u, 1 /*D3D11_MAP_READ*/, 0u,
                        out D3D11_MAPPED_SUBRESOURCE mapped);
                    if (hr < 0) return null;

                    try
                    {
                        return TextureToJpeg(mapped, (int)desc.Width, (int)desc.Height);
                    }
                    finally
                    {
                        // Unmap (slot 15)
                        VtCall<UnmapFn>(_d3dContext, 15)(_d3dContext, staging, 0u);
                    }
                }
                finally { COMRelease(staging); }
            }
            finally { COMRelease(texture); }
        }
        catch (Exception ex)
        {
            _logInfo?.Invoke($"[DXGI] Capture грешка: {ex.Message}");
            return null;
        }
        finally
        {
            if (resource != IntPtr.Zero) COMRelease(resource);
            // ReleaseFrame (slot 14)
            if (_duplication != IntPtr.Zero)
                VtCall<ReleaseFrameFn>(_duplication, 14)(_duplication);
        }
    }

    private unsafe byte[] TextureToJpeg(D3D11_MAPPED_SUBRESOURCE mapped, int w, int h)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h),
                              ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = w * 4;
            for (int row = 0; row < h; row++)
            {
                var src = (byte*)mapped.pData + (long)row * mapped.RowPitch;
                var dst = (byte*)bd.Scan0     + (long)row * bd.Stride;
                Buffer.MemoryCopy(src, dst, rowBytes, rowBytes);
            }
        }
        finally { bmp.UnlockBits(bd); }

        using var ms = new MemoryStream();
        bmp.Save(ms, _jpegCodec, _encoderParams);
        return ms.ToArray();
    }

    private void ReleaseDxgi()
    {
        if (_duplication != IntPtr.Zero) { COMRelease(_duplication); _duplication = IntPtr.Zero; }
        if (_d3dContext  != IntPtr.Zero) { COMRelease(_d3dContext);  _d3dContext  = IntPtr.Zero; }
        if (_d3dDevice   != IntPtr.Zero) { COMRelease(_d3dDevice);   _d3dDevice   = IntPtr.Zero; }
        _dxgiReady = false;
    }

    // ─── COM vtable helpers ──────────────────────────────────────────────────

    private static unsafe T VtCall<T>(IntPtr obj, int slot) where T : Delegate
    {
        IntPtr* vtbl = *(IntPtr**)obj;
        return Marshal.GetDelegateForFunctionPointer<T>(vtbl[slot]);
    }

    private static int COMQi(IntPtr pUnk, ref Guid riid, out IntPtr ppv)
        => VtCall<QiFn>(pUnk, 0)(pUnk, ref riid, out ppv);

    private static void COMRelease(IntPtr pUnk)
    {
        if (pUnk != IntPtr.Zero) VtCall<ReleaseFn>(pUnk, 2)(pUnk);
    }

    // ─── COM delegate types ──────────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int QiFn(IntPtr p, ref Guid riid, out IntPtr ppv);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate uint ReleaseFn(IntPtr p);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetAdapterFn(IntPtr pDev, out IntPtr ppAdapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumOutputsFn(IntPtr pAdapter, uint index, out IntPtr ppOutput);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int DuplicateOutputFn(IntPtr pOutput1, IntPtr pDevice, out IntPtr ppDupl);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int AcquireNextFrameFn(IntPtr pDupl, uint timeoutMs,
        out DXGI_OUTDUPL_FRAME_INFO pInfo, out IntPtr ppResource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ReleaseFrameFn(IntPtr pDupl);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GetTexDescFn(IntPtr pTex, out D3D11_TEXTURE2D_DESC pDesc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTex2DFn(IntPtr pDev, ref D3D11_TEXTURE2D_DESC pDesc,
        IntPtr pInitData, out IntPtr ppTex);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void CopyResourceFn(IntPtr pCtx, IntPtr pDst, IntPtr pSrc);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int MapFn(IntPtr pCtx, IntPtr pRes, uint subres, int mapType, uint flags,
        out D3D11_MAPPED_SUBRESOURCE pMapped);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void UnmapFn(IntPtr pCtx, IntPtr pRes, uint subres);

    // ─── D3D/DXGI structs ───────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize;
        public int  Format;                        // DXGI_FORMAT
        public uint SampleCount, SampleQuality;    // DXGI_SAMPLE_DESC
        public int  Usage, BindFlags, CPUAccessFlags, MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct D3D11_MAPPED_SUBRESOURCE
    {
        public IntPtr pData;
        public uint   RowPitch, DepthPitch;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DXGI_OUTDUPL_FRAME_INFO
    {
        public long  LastPresentTime, LastMouseUpdateTime;
        public uint  AccumulatedFrames;
        public int   RectsCoalesced, ProtectedContentMaskedOut;
        public int   PtrX, PtrY, PtrVisible;       // DXGI_OUTDUPL_POINTER_POSITION
        public uint  TotalMetadataBufferSize, PointerShapeBufferSize;
    }

    // ─── D3D11 P/Invoke ─────────────────────────────────────────────────────

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.Winapi)]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter, int DriverType, IntPtr Software, int Flags,
        IntPtr pFeatureLevels, int FeatureLevels, int SDKVersion,
        out IntPtr ppDevice, IntPtr pFeatureLevel, out IntPtr ppImmediateContext);

    // ─── GDI BitBlt fallback ─────────────────────────────────────────────────

    private byte[] CaptureGdi()
    {
        if (!_desktopAttached)
            _desktopAttached = AttachThreadToInputDesktop();

        int screenW = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int screenH = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        int screenX = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int screenY = GetSystemMetrics(SM_YVIRTUALSCREEN);

        if (screenW <= 0 || screenH <= 0)
        {
            var b = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            screenW = b.Width; screenH = b.Height; screenX = b.X; screenY = b.Y;
        }

        if (!_diagLogged)
        {
            _diagLogged = true;
            _logInfo?.Invoke($"[GDI Fallback] VirtualScreen={screenW}x{screenH}, Session={System.Diagnostics.Process.GetCurrentProcess().SessionId}");
        }

        IntPtr screenDc = GetDC(IntPtr.Zero);
        bool useReleaseDC = screenDc != IntPtr.Zero;
        if (!useReleaseDC)
        {
            screenDc = CreateDC("DISPLAY", null!, null!, IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
                throw new InvalidOperationException("Не може да се получи screen DC.");
        }

        var memDc = CreateCompatibleDC(screenDc);
        if (memDc == IntPtr.Zero)
        {
            if (useReleaseDC) ReleaseDC(IntPtr.Zero, screenDc); else DeleteDC(screenDc);
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        var hBmp = CreateCompatibleBitmap(screenDc, screenW, screenH);
        if (hBmp == IntPtr.Zero)
        {
            DeleteDC(memDc);
            if (useReleaseDC) ReleaseDC(IntPtr.Zero, screenDc); else DeleteDC(screenDc);
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        var oldBmp = SelectObject(memDc, hBmp);
        try
        {
            bool ok = BitBlt(memDc, 0, 0, screenW, screenH,
                             screenDc, screenX, screenY, SRCCOPY | CAPTUREBLT);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                throw new System.ComponentModel.Win32Exception(err,
                    $"BitBlt неуспешен (err={err}, {screenW}x{screenH}).");
            }

            using var bmp = Image.FromHbitmap(hBmp);
            using var ms  = new MemoryStream();
            bmp.Save(ms, _jpegCodec, _encoderParams);
            return ms.ToArray();
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(hBmp);
            DeleteDC(memDc);
            if (useReleaseDC) ReleaseDC(IntPtr.Zero, screenDc); else DeleteDC(screenDc);
        }
    }

    private bool AttachThreadToInputDesktop()
    {
        const uint DESKTOP_ALL = 0x01FF;
        var hDesk = OpenInputDesktop(0, false, DESKTOP_ALL);
        if (hDesk == IntPtr.Zero)
            hDesk = OpenDesktop("Default", 0, false, DESKTOP_ALL);
        if (hDesk == IntPtr.Zero) return false;

        bool ok = SetThreadDesktop(hDesk);
        if (!ok) { CloseDesktop(hDesk); return false; }

        if (_hDesktop != IntPtr.Zero) CloseDesktop(_hDesktop);
        _hDesktop = hDesk;
        _logInfo?.Invoke("[GDI] Input desktop прикачен (GDI fallback).");
        return true;
    }

    public void Dispose()
    {
        ReleaseDxgi();
        _encoderParams.Dispose();
    }

    #region Win32 GDI

    private const int SRCCOPY    = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;
    private const int SM_XVIRTUALSCREEN  = 76;
    private const int SM_YVIRTUALSCREEN  = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")] private static extern int    GetSystemMetrics(int n);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] private static extern int    ReleaseDC(IntPtr h, IntPtr hdc);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint f, bool i, uint a);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern IntPtr OpenDesktop(string n, uint f, bool i, uint a);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetThreadDesktop(IntPtr h);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseDesktop(IntPtr h);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateDC(string d, string? dev, string? o, IntPtr i);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr h);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleBitmap(IntPtr h, int w, int ht);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr h, IntPtr o);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr h, int x, int y, int w, int ht, IntPtr s, int sx, int sy, int op);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr h);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

    #endregion
}
