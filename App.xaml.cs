using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;

namespace Scriptorium;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                File.WriteAllText("app_error.log", args.Exception.ToString());
            }
            catch
            {
                // Ignore file write errors
            }
            MessageBox.Show(
                $"Beklenmeyen bir arayüz hatası oluştu:\n{args.Exception.Message}",
                "Scriptorium — Sistem Uyarısı",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        var trCulture = new CultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = trCulture;
        CultureInfo.DefaultThreadCurrentUICulture = trCulture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(trCulture.IetfLanguageTag)));

        base.OnStartup(e);
    }
}
