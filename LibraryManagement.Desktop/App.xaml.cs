using System.Windows;

namespace LibraryManagement.Desktop;

public partial class App : System.Windows.Application
{
    public static void ReturnToLogin(Window currentWindow)
    {
        if (Current == null) return;

        Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        currentWindow.Close();

        var loginWin = new LoginWindow();
        if (loginWin.ShowDialog() == true)
        {
            if (loginWin.SelectedRole == UserRole.Admin)
            {
                var adminWindow = new MainWindow();
                Current.MainWindow = adminWindow;
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                adminWindow.Show();
            }
            else
            {
                var userWindow = new Customer3DWindow();
                Current.MainWindow = userWindow;
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                userWindow.Show();
            }
        }
        else
        {
            Current.Shutdown();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Doğrudan "--customer-3d" veya "--customer" parametresi verilirse 3D Müşteri Ekranı açılır.
        if (e.Args.Contains("--customer-3d") || e.Args.Contains("--customer") || e.Args.Contains("--customer-2d"))
        {
            var customer3D = new Customer3DWindow();
            MainWindow = customer3D;
            customer3D.Show();
            return;
        }

        // Doğrudan "--admin" verilirse doğrudan Admin Paneli açılır.
        if (e.Args.Contains("--admin"))
        {
            CheckoutState.Instance.LoggedInRole = UserRole.Admin;
            CheckoutState.Instance.LoggedInUser = "admin";
            var main = new MainWindow();
            MainWindow = main;
            main.Show();
            return;
        }

        // Giriş Ekranı (LoginWindow)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var loginWin = new LoginWindow();
        if (loginWin.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        // Seçilen veya girilen role göre yönlendirme:
        // Admin -> MainWindow (Admin Paneli)
        // User -> Customer3DWindow (3D Kullanıcı/Müşteri Paneli)
        if (loginWin.SelectedRole == UserRole.Admin)
        {
            var adminWindow = new MainWindow();
            MainWindow = adminWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            adminWindow.Show();
        }
        else
        {
            var userWindow = new Customer3DWindow();
            MainWindow = userWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            userWindow.Show();
        }
    }
}
