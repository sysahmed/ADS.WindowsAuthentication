using ADS.WindowsAuth.RemoteDesktopHost.Services;
using Microsoft.Extensions.Configuration;
using System.Runtime.InteropServices;

namespace ADS.WindowsAuth.RemoteDesktopHost;

static class Program
{
    [STAThread]
    static void Main()
    {
        // ───────────────────────────────────────────────────────────────────
        // ВАЖНО: Превключваме към интерактивния window station (winsta0) и
        // Default desktop ПРЕДИ всичко друго. Без това GetDC/BitBlt се провалят
        // с error 6 когато процесът е стартиран чрез CreateProcessAsUser от Session 0.
        // ───────────────────────────────────────────────────────────────────
        SwitchToInteractiveWindowStation();

        ApplicationConfiguration.Initialize();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var apiBaseUrl = config["ApiBaseUrl"] ?? "https://localhost:7001";
        var frameRate = int.TryParse(config["FrameRate"], out var fr) ? fr : 10;
        var quality = int.TryParse(config["Quality"], out var q) ? q : 50;

        Application.Run(new TrayApplicationContext(apiBaseUrl, frameRate, quality));
    }

    /// <summary>
    /// Превключва процеса към winsta0\Default (интерактивния desktop).
    /// Задължително при стартиране чрез CreateProcessAsUser от Session 0 / SYSTEM.
    /// </summary>
    private static void SwitchToInteractiveWindowStation()
    {
        const uint WINSTA_ALL_ACCESS = 0x037F;
        const uint DESKTOP_ALL_ACCESS = 0x01FF;

        // Отваряме winsta0 – интерактивният window station на текущата сесия
        IntPtr hWinSta = OpenWindowStation("winsta0", false, WINSTA_ALL_ACCESS);
        if (hWinSta != IntPtr.Zero)
        {
            SetProcessWindowStation(hWinSta);
            // Не затваряме – процесът трябва да го пази отворен
        }

        // Отваряме Default desktop
        IntPtr hDesk = OpenDesktop("Default", 0, false, DESKTOP_ALL_ACCESS);
        if (hDesk != IntPtr.Zero)
        {
            SetThreadDesktop(hDesk);
            // Не затваряме – нишката трябва да го пази отворен
        }
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenWindowStation(string lpszWinSta, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessWindowStation(IntPtr hWinSta);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenDesktop(string lpszDesktop, uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);
}
