using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LibraryManagement.Desktop;

public partial class LoginWindow : Window
{
    bool _showPassword;
    bool _syncingPassword;
    bool _submitting;
    UserRole _selectedRole = UserRole.Admin;

    public UserRole SelectedRole => _selectedRole;

    public LoginWindow()
    {
        InitializeComponent();
        PassBox.Password = "1234";
        UpdateRoleButtons();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Submit();
            else if (e.Key == Key.Escape) DialogResult = false;
        };
        Loaded += async (_, _) =>
        {
            UserBox.Focus();
            UserBox.SelectAll();
            var shown = new List<BookItem>();
            var catalog = CheckoutState.Instance.GetCatalogSample();
            int[] indices = { 2, 1, 0, 3 };
            foreach (var i in indices)
            {
                await Task.Delay(500);
                if (i < catalog.Count)
                {
                    shown.Add(catalog[i]);
                    Deco.Sync(shown);
                }
            }
        };
    }

    void OnSelectAdminRole(object sender, RoutedEventArgs e)
    {
        _selectedRole = UserRole.Admin;
        if (string.Equals(UserBox.Text.Trim(), "kullanici", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(UserBox.Text.Trim(), "user", StringComparison.OrdinalIgnoreCase))
        {
            UserBox.Text = "admin";
        }
        UpdateRoleButtons();
    }

    void OnSelectUserRole(object sender, RoutedEventArgs e)
    {
        _selectedRole = UserRole.User;
        if (string.Equals(UserBox.Text.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
        {
            UserBox.Text = "kullanici";
        }
        UpdateRoleButtons();
    }

    void UpdateRoleButtons()
    {
        var activeBrush = new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90);
        var activeBorder = Ui.B("#8FD6A0");

        if (_selectedRole == UserRole.Admin)
        {
            AdminRoleBtn.Background = activeBrush;
            AdminRoleBtn.BorderBrush = activeBorder;
            AdminRoleBtn.Foreground = Brushes.White;

            UserRoleBtn.Background = Brushes.Transparent;
            UserRoleBtn.BorderBrush = Brushes.Transparent;
            UserRoleBtn.Foreground = Ui.B("#C9BFAE");
        }
        else
        {
            UserRoleBtn.Background = activeBrush;
            UserRoleBtn.BorderBrush = activeBorder;
            UserRoleBtn.Foreground = Brushes.White;

            AdminRoleBtn.Background = Brushes.Transparent;
            AdminRoleBtn.BorderBrush = Brushes.Transparent;
            AdminRoleBtn.Foreground = Ui.B("#C9BFAE");
        }
    }

    void OnDrag(object s, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    void OnClose(object s, RoutedEventArgs e) => DialogResult = false;
    void OnLogin(object s, RoutedEventArgs e) => Submit();

    void OnPasswordChanged(object s, RoutedEventArgs e)
    {
        if (_syncingPassword) return;
        _syncingPassword = true;
        PassTextBox.Text = PassBox.Password;
        _syncingPassword = false;
    }

    void OnPassTextChanged(object s, TextChangedEventArgs e)
    {
        if (_syncingPassword) return;
        _syncingPassword = true;
        PassBox.Password = PassTextBox.Text;
        _syncingPassword = false;
    }

    void OnTogglePassword(object s, RoutedEventArgs e)
    {
        _showPassword = !_showPassword;
        if (_showPassword)
        {
            PassTextBox.Text = PassBox.Password;
            PassBox.Visibility = Visibility.Collapsed;
            PassTextBox.Visibility = Visibility.Visible;
            PassTextBox.Focus();
            PassTextBox.CaretIndex = PassTextBox.Text.Length;
            EyeIcon.Foreground = Ui.B("#6FBF7F");
        }
        else
        {
            PassBox.Password = PassTextBox.Text;
            PassTextBox.Visibility = Visibility.Collapsed;
            PassBox.Visibility = Visibility.Visible;
            PassBox.Focus();
            EyeIcon.Foreground = Ui.B("#C9BFAE");
        }
    }

    void OnForgotPassword(object s, MouseButtonEventArgs e)
    {
        ErrorText.Foreground = Ui.B("#E4B368");
        ErrorText.Text = "Şifre sıfırlama için Sistem Yöneticisi ile iletişime geçin.";
    }

    string CurrentPassword => _showPassword ? PassTextBox.Text : PassBox.Password;

    async void Submit()
    {
        if (_submitting) return;
        ErrorText.Foreground = Ui.B("#F08A7A");
        ErrorText.Text = "";
        string user = UserBox.Text.Trim();
        string pass = CurrentPassword;

        if (user.Length == 0 || pass.Length == 0)
        {
            Fail("Kullanıcı adı ve şifre gerekli.");
            return;
        }
        if (!TryLogin(user, pass))
        {
            Fail("Kullanıcı adı veya şifre hatalı.");
            return;
        }

        // Kullanıcı adına göre rol belirleme (admin / yonetici ise Admin, kullanici / user veya tab seçimine göre)
        if (string.Equals(user, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user, "yonetici", StringComparison.OrdinalIgnoreCase))
        {
            _selectedRole = UserRole.Admin;
        }
        else if (string.Equals(user, "kullanici", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(user, "user", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(user, "ogrenci", StringComparison.OrdinalIgnoreCase))
        {
            _selectedRole = UserRole.User;
        }

        _submitting = true;
        CheckoutState.Instance.LoggedInUser = user;
        CheckoutState.Instance.LoggedInRole = _selectedRole;

        string roleTitle = _selectedRole == UserRole.Admin ? "Yönetim Paneli açılıyor…" : "Kullanıcı Paneli açılıyor…";
        WelcomeTitle.Text = $"Hoş geldiniz, {user}";
        ((TextBlock)WelcomePanel.Children[2]).Text = roleTitle;
        FormPanel.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Visible;

        await Task.Delay(950);
        DialogResult = true;
    }

    static bool TryLogin(string user, string password) => true;

    void Fail(string message)
    {
        ErrorText.Text = message;
        var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(380) };
        double[] xs = { -12, 10, -8, 6, -3, 0 };
        for (int i = 0; i < xs.Length; i++)
            a.KeyFrames.Add(new LinearDoubleKeyFrame(xs[i], KeyTime.FromPercent((i + 1) / (double)xs.Length)));
        Shake.BeginAnimation(TranslateTransform.XProperty, a);
    }
}
