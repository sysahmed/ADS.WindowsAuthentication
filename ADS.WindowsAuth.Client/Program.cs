using ADS.WindowsAuth.Client.Services;

namespace ADS.WindowsAuth.Client;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // Проверка за администраторски права
        if (!WindowsLockScreenHelper.IsRunningAsAdministrator())
        {
            System.Windows.Forms.MessageBox.Show(
                "За по-надеждна работа на lock screen, препоръчва се да стартирате приложението с администраторски права.",
                "Предупреждение",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
        }

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }    
}