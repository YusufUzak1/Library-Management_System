using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using LibraryManagement.Application.Interfaces;
using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Desktop;

public partial class MainWindow : Window
{
    readonly CheckoutState _s = CheckoutState.Instance;
    readonly ILibraryDataService _db;
    readonly List<(BookItem Book, Border Row)> _rows = new();
    readonly List<(string Name, Button Button)> _navButtons = new();
    private readonly System.Windows.Threading.DispatcherTimer _dbSyncTimer = new();
    string _currentRoute = "Ana Sayfa";

    public MainWindow()
    {
        InitializeComponent();
        _db = _s.DataService;
        GreetingText.Text = $"Günaydın, {_s.LoggedInUser} 👋";

        BuildNav();
        BuildStats();
        BuildActions();
        BuildToday();
        BuildRecent();
        BuildChart();
        BuildQuick();
        BuildTop();

        _s.Changed += () =>
        {
            foreach (var (b, r) in _rows)
                r.Background = _s.Books.Contains(b) ? Ui.B("#446FBF7F") : Brushes.Transparent;
            RefreshAllViews();
        };

        // Arka plan MariaDB senkronizasyon zamanlayıcısı (kullanıcı kiralama yapınca admin anında görür)
        _dbSyncTimer.Interval = TimeSpan.FromSeconds(2);
        _dbSyncTimer.Tick += async (_, _) =>
        {
            bool synced = await Task.Run(() => _db.SyncFromDatabase());
            if (synced)
            {
                RefreshAllViews();
                if (_currentRoute == "Envanter" || _currentRoute == "İade Masası")
                    RenderReturnDeskList(GlobalSearchBox.Text);
                else if (_currentRoute == "Kitaplar")
                    RenderBooksList(GlobalSearchBox.Text);
            }
        };
        _dbSyncTimer.Start();

        Unloaded += (_, _) => _dbSyncTimer.Stop();

        SetRoute("Ana Sayfa");
    }

    private void RefreshAllViews()
    {
        BuildStats();
        BuildToday();
        BuildRecent();
        BuildTop();
    }

    public enum DialogType { Confirm, Danger, Warning, Info }
    private Action? _modalConfirmAction;

    public void ShowModal(string title, string message, string confirmText, Action onConfirm, DialogType type = DialogType.Confirm, string cancelText = "Vazgeç")
    {
        ModalTitle.Text = title;
        ModalMessage.Text = message;
        ModalConfirmBtn.Content = confirmText;
        ModalCancelBtn.Content = cancelText;
        ModalCancelBtn.Visibility = string.IsNullOrEmpty(cancelText) ? Visibility.Collapsed : Visibility.Visible;
        _modalConfirmAction = onConfirm;

        switch (type)
        {
            case DialogType.Danger:
                ModalIconCircle.Background = Ui.B("#33F08A7A");
                ModalIcon.Text = "\uE7E8";
                ModalIcon.Foreground = Ui.B("#F08A7A");
                ModalConfirmBtn.Background = new LinearGradientBrush(Ui.C("#A34032"), Ui.C("#5B1B15"), 90);
                ModalConfirmBtn.BorderBrush = Ui.B("#F08A7A");
                ModalConfirmBtn.Foreground = Ui.B("#FFFFFF");
                break;
            case DialogType.Warning:
                ModalIconCircle.Background = Ui.B("#33E4B368");
                ModalIcon.Text = "\uE7BA";
                ModalIcon.Foreground = Ui.B("#E4B368");
                ModalConfirmBtn.Background = new LinearGradientBrush(Ui.C("#A67C30"), Ui.C("#593E12"), 90);
                ModalConfirmBtn.BorderBrush = Ui.B("#E4B368");
                ModalConfirmBtn.Foreground = Ui.B("#FFFFFF");
                break;
            case DialogType.Info:
                ModalIconCircle.Background = Ui.B("#3370B0E0");
                ModalIcon.Text = "\uE946";
                ModalIcon.Foreground = Ui.B("#70B0E0");
                ModalConfirmBtn.Background = new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90);
                ModalConfirmBtn.BorderBrush = Ui.B("#8FD6A0");
                ModalConfirmBtn.Foreground = Ui.B("#FFFFFF");
                break;
            default:
                ModalIconCircle.Background = Ui.B("#336FBF7F");
                ModalIcon.Text = "\uE73E";
                ModalIcon.Foreground = Ui.B("#6FBF7F");
                ModalConfirmBtn.Background = new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90);
                ModalConfirmBtn.BorderBrush = Ui.B("#8FD6A0");
                ModalConfirmBtn.Foreground = Ui.B("#FFFFFF");
                break;
        }

        ModalHost.Visibility = Visibility.Visible;
    }

    private void OnModalConfirmClick(object sender, RoutedEventArgs e)
    {
        ModalHost.Visibility = Visibility.Collapsed;
        _modalConfirmAction?.Invoke();
    }

    private void OnModalCancelClick(object sender, RoutedEventArgs e)
    {
        ModalHost.Visibility = Visibility.Collapsed;
    }

    public async void ShowToast(string message, bool isError = false)
    {
        ToastText.Text = message;
        ToastIcon.Text = isError ? "\uE783" : "\uE73E";
        ToastIcon.Foreground = Ui.B(isError ? "#F08A7A" : "#7FD08F");
        ToastHost.BorderBrush = Ui.B(isError ? "#88F08A7A" : "#556FBF7F");
        ToastHost.Visibility = Visibility.Visible;
        await Task.Delay(2600);
        ToastHost.Visibility = Visibility.Collapsed;
    }

    private void OnBackToDashboardClick(object sender, RoutedEventArgs e)
    {
        SetRoute("Ana Sayfa");
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not Button && e.OriginalSource is not TextBox)
        {
            try { DragMove(); } catch { }
        }
    }

    private void OnMinimizeAppClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeAppClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnExitAppClick(object sender, RoutedEventArgs e)
    {
        ShowModal(
            "Uygulamayı Kapat",
            "Kütüphane Otomasyon Sistemini tamamen kapatmak istediğinize emin misiniz?",
            "Evet, Kapat",
            () => System.Windows.Application.Current.Shutdown(),
            DialogType.Danger);
    }

    private void OnLogoutClick()
    {
        ShowModal(
            "Oturumu Kapat",
            "Yönetici oturumunuz kapatılacak ve giriş ekranına yönlendirileceksiniz. Devam etmek istiyor musunuz?",
            "Giriş Ekranına Dön",
            () => App.ReturnToLogin(this),
            DialogType.Warning);
    }

    private void OnGlobalSearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = GlobalSearchBox.Text.Trim();
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

        if (_currentRoute == "Kitaplar")
        {
            RenderBooksList(query);
        }
        else if (_currentRoute == "Üyeler")
        {
            RenderMembersList(query);
        }
        else if (_currentRoute == "İade Masası" || _currentRoute == "Envanter")
        {
            RenderReturnDeskList(query);
        }
        else if (!string.IsNullOrEmpty(query) && _currentRoute == "Ana Sayfa")
        {
            SetRoute("Kitaplar");
            RenderBooksList(query);
        }
    }

    public void SetRoute(string route)
    {
        _currentRoute = route;

        foreach (var (name, btn) in _navButtons)
        {
            bool active = name == route;
            btn.Background = active ? new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90) : Brushes.Transparent;
            btn.BorderBrush = active ? Ui.B("#8FD6A0") : Brushes.Transparent;
            btn.Foreground = Ui.B(active ? "#FFFFFF" : "#E3DACB");
        }

        if (route == "Ana Sayfa")
        {
            DashboardView.Visibility = Visibility.Visible;
            SubPageView.Visibility = Visibility.Collapsed;
            BackToDashboardBtn.Visibility = Visibility.Collapsed;
            GreetingText.Text = $"Günaydın, {_s.LoggedInUser} 👋";
            GreetingSub.Text = "Kütüphane yönetim sistemine hoş geldiniz.";
            RefreshAllViews();
        }
        else
        {
            DashboardView.Visibility = Visibility.Collapsed;
            SubPageView.Visibility = Visibility.Visible;
            BackToDashboardBtn.Visibility = Visibility.Visible;

            switch (route)
            {
                case "Kitaplar":
                    ShowBooksView();
                    break;
                case "Üyeler":
                    ShowMembersView();
                    break;
                case "Rezervasyonlar":
                    ShowReservationsView();
                    break;
                case "Envanter":
                    ShowInventoryView();
                    break;
                case "İade Masası":
                    ShowReturnDeskView();
                    break;
                case "Yeni Kitap":
                    ShowAddBookView();
                    break;
                case "Yeni Üye":
                    ShowAddMemberView();
                    break;
                case "Raporlar":
                    ShowReportsView();
                    break;
                case "Ayarlar":
                    ShowSettingsView();
                    break;
            }
        }
    }

    #region Left Navigation & Header Actions
    void BuildNav()
    {
        var logo = (StackPanel)FindName("Logo");
        logo.Children.Clear();
        logo.Children.Add(Ui.Icon("\uE8F1", 26, "#F4EFE6"));
        var t = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        t.Children.Add(Ui.Text("Fırat Üniversitesi", 13, FontWeights.SemiBold));
        t.Children.Add(Ui.Text("Kütüphane Sistemi", 13, FontWeights.SemiBold));
        logo.Children.Add(t);

        string[] names = { "Ana Sayfa", "Kitaplar", "Üyeler", "Rezervasyonlar", "Envanter", "İade Masası", "Raporlar", "Ayarlar" };
        string[] icons = { "\uE80F", "\uE8F1", "\uE716", "\uE787", "\uE7B8", "\uE72C", "\uE9D9", "\uE713" };
        
        NavHost.Children.Clear();
        _navButtons.Clear();

        for (int i = 0; i < names.Length; i++)
        {
            string routeName = names[i];
            var b = GlassBtn(icons[i], routeName, false, () => SetRoute(routeName), 38);
            b.Margin = new Thickness(0, 0, 0, 5);
            b.Background = Brushes.Transparent;
            b.BorderBrush = Brushes.Transparent;
            b.Foreground = Ui.B("#E3DACB");
            NavHost.Children.Add(b);
            _navButtons.Add((routeName, b));
        }

        var prof = new Grid();
        prof.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        prof.ColumnDefinitions.Add(new ColumnDefinition());
        prof.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var av = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), Background = Ui.B("#3C6B4A"), Child = Ui.Icon("\uE77B", 18) };
        var who = new StackPanel { Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        who.Children.Add(Ui.Text(_s.LoggedInUser + " Uzak", 12, FontWeights.SemiBold));
        who.Children.Add(Ui.Text("Kütüphane Yöneticisi", 10, null, "#C9BFAE"));
        Grid.SetColumn(who, 1);

        var logoutBtn = new Button
        {
            Width = 34,
            Height = 34,
            Style = (Style)FindResource("GlassButton"),
            Background = Ui.B("#22E4B368"),
            BorderBrush = Ui.B("#66E4B368"),
            ToolTip = "Oturumu Kapat (Giriş Ekranına Dön)",
            Cursor = Cursors.Hand,
            Content = Ui.Icon("\uE8AC", 15, "#E4B368")
        };
        logoutBtn.Click += (_, _) => OnLogoutClick();
        Grid.SetColumn(logoutBtn, 2);

        prof.Children.Add(av);
        prof.Children.Add(who);
        prof.Children.Add(logoutBtn);
        ((Border)FindName("Profile")).Child = prof;
    }

    void BuildStats()
    {
        var host = (UniformGrid)FindName("StatsHost");
        host.Children.Clear();
        var metrics = _db.GetDashboardMetrics();

        (string g, string v, string l, string tint, string ic, Action onClick)[] s =
        {
            ("\uE8F1", metrics.TotalBooks.ToString("N0"), "Toplam Kitap", "#3A5E45", "#7FD08F", () => SetRoute("Kitaplar")),
            ("\uE81E", metrics.AvailableBooks.ToString("N0"), "Mevcut Kitap", "#3A6050", "#7FD08F", () => SetRoute("Envanter")),
            ("\uE716", metrics.RegisteredMembers.ToString("N0"), "Kayıtlı Üye", "#6A5230", "#E4B368", () => SetRoute("Üyeler")),
            ("\uE823", metrics.OverdueReturns.ToString("N0"), "Gecikmiş İade", "#7A3A32", "#F08A7A", () => SetRoute("İade Masası")),
        };
        for (int i = 0; i < s.Length; i++)
        {
            var item = s[i];
            var card = new Border { Style = (Style)FindResource("Glass"), Margin = new Thickness(i == 0 ? 0 : 7, 0, i == 3 ? 0 : 7, 8), Padding = new Thickness(16, 0, 16, 0), Cursor = Cursors.Hand };
            card.Background = new LinearGradientBrush(Ui.C(item.tint + "").WithAlpha(0xAA), Ui.C("#14FFFFFF"), 45);
            card.MouseLeftButtonUp += (_, _) => item.onClick();

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition());
            var ico = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(14), Background = Ui.B(item.tint), Child = Ui.Icon(item.g, 22, item.ic) };
            var tx = new StackPanel { Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            tx.Children.Add(Ui.Text(item.v, 26, FontWeights.SemiBold)); tx.Children.Add(Ui.Text(item.l, 12, null, "#D8CFC0"));
            Grid.SetColumn(tx, 1); g.Children.Add(ico); g.Children.Add(tx); card.Child = g; host.Children.Add(card);
        }
    }

    void BuildActions()
    {
        var h = (StackPanel)FindName("ActionsHost");
        h.Children.Clear();
        void Add(Button b, double w = 155) { b.Width = w; b.Margin = new Thickness(0, 0, 12, 0); h.Children.Add(b); }

        Add(GlassBtn("\uE710", "Ödünç Ver", true, () => ShowQuickLendDialog()));
        Add(GlassBtn("\uE72C", "İade Al", false, () => SetRoute("İade Masası")));
        Add(GlassBtn("\uE8FA", "Üye Ekle", false, () => SetRoute("Yeni Üye")));
        Add(GlassBtn("\uE736", "Yeni Kitap", false, () => SetRoute("Yeni Kitap")));
        Add(GlassBtn("\uE73E", "Onayla", true, OnConfirmActionClick));
    }

    #region Quick Lend & Approval Logic
    private List<Member> _quickLendMembers = new();
    private List<Book> _quickLendBooks = new();

    public void ShowQuickLendDialog(Book? preselectedBook = null)
    {
        _quickLendMembers = _db.GetMembers().Where(m => m.Status == MemberStatus.Active).ToList();
        _quickLendBooks = _db.GetBooks().Where(b => b.AvailableCopies > 0).ToList();

        CmbLendMember.Items.Clear();
        foreach (var m in _quickLendMembers)
        {
            CmbLendMember.Items.Add($"{m.FullName} ({m.MemberNumber} - {m.TierTitle})");
        }
        if (CmbLendMember.Items.Count > 0) CmbLendMember.SelectedIndex = 0;

        CmbLendBook.Items.Clear();
        int bookIndex = 0;
        for (int i = 0; i < _quickLendBooks.Count; i++)
        {
            var b = _quickLendBooks[i];
            CmbLendBook.Items.Add($"{b.Title} - {b.AuthorName} (Stok: {b.AvailableCopies})");
            if (preselectedBook != null && b.Id == preselectedBook.Id)
                bookIndex = i;
        }
        if (CmbLendBook.Items.Count > 0) CmbLendBook.SelectedIndex = bookIndex;

        CmbLendDuration.SelectedIndex = 0; // 15 gün
        QuickLendHost.Visibility = Visibility.Visible;
    }

    private void OnCancelQuickLendClick(object sender, RoutedEventArgs e)
    {
        QuickLendHost.Visibility = Visibility.Collapsed;
    }

    private void OnConfirmQuickLendClick(object sender, RoutedEventArgs e)
    {
        if (CmbLendMember.SelectedIndex < 0 || CmbLendMember.SelectedIndex >= _quickLendMembers.Count)
        {
            ShowToast("Lütfen ödünç alacak bir üye seçin.", isError: true);
            return;
        }
        if (CmbLendBook.SelectedIndex < 0 || CmbLendBook.SelectedIndex >= _quickLendBooks.Count)
        {
            ShowToast("Lütfen verilecek uygun bir kitap seçin.", isError: true);
            return;
        }

        var member = _quickLendMembers[CmbLendMember.SelectedIndex];
        var book = _quickLendBooks[CmbLendBook.SelectedIndex];

        int days = CmbLendDuration.SelectedIndex switch
        {
            1 => 7,
            2 => 30,
            _ => 15
        };

        if (_db.CommitCheckout(member, new[] { book }, days, out string msg))
        {
            QuickLendHost.Visibility = Visibility.Collapsed;
            ShowToast($"✓ '{book.Title}' {member.FullName} adlı üyeye başarıyla ödünç verildi.");
            RefreshAllViews();
        }
        else
        {
            ShowToast(msg, isError: true);
        }
    }

    private void OnConfirmActionClick()
    {
        if (_s.Books.Count > 0)
        {
            var bookTitles = string.Join(", ", _s.Books.Select(b => b.Title));
            ShowModal(
                "Ödünç İşlemini Onayla",
                $"Seçilen {_s.Books.Count} kitap için ödünç verme işlemini onaylayıp MariaDB veritabanına kaydetmek istiyor musunuz?\n\nKitaplar: {bookTitles}",
                "✓ Onayla ve Kaydet",
                () =>
                {
                    _s.Confirm();
                    ShowToast($"✓ {_s.Books.Count} kitap için ödünç işlemi onaylandı ve MariaDB'ye kaydedildi.");
                    RefreshAllViews();
                },
                DialogType.Confirm);
        }
        else
        {
            ShowQuickLendDialog();
        }
    }
    #endregion
    #endregion

    #region In-Page Sub Views (Directly on MainWindow)

    // 1. Kitaplar View
    private StackPanel _booksContainer = null!;
    private void ShowBooksView()
    {
        GreetingText.Text = "📚 Kitap Kataloğu & Envanter";
        GreetingSub.Text = "Kütüphanedeki tüm kitapların güncel envanter listesi ve durumları.";

        SubPageBody.Children.Clear();
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Action bar
        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var addBtn = GlassBtn("\uE710", "Yeni Kitap Ekle", true, () => SetRoute("Yeni Kitap"), 38);
        addBtn.Padding = new Thickness(14, 0, 14, 0);
        DockPanel.SetDock(addBtn, Dock.Right);
        topBar.Children.Add(addBtn);

        var countText = Ui.Text($"Toplam {_db.GetBooks().Count} Eser Kayıtlı", 14, FontWeights.SemiBold, "#E4B368");
        countText.VerticalAlignment = VerticalAlignment.Center;
        topBar.Children.Add(countText);
        mainGrid.Children.Add(topBar);

        // Scrollable list
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _booksContainer = new StackPanel();
        sv.Content = _booksContainer;
        Grid.SetRow(sv, 1);
        mainGrid.Children.Add(sv);

        SubPageBody.Children.Add(mainGrid);
        RenderBooksList(GlobalSearchBox.Text);
    }

    private void RenderBooksList(string? query)
    {
        if (_booksContainer == null) return;
        _booksContainer.Children.Clear();

        var books = _db.GetBooks();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLower();
            books = books.Where(b => b.Title.ToLower().Contains(q) || b.AuthorName.ToLower().Contains(q) || b.Isbn.Contains(q) || b.CategoryName.ToLower().Contains(q)).ToList();
        }

        if (!books.Any())
        {
            _booksContainer.Children.Add(Ui.Text("Aramanızla eşleşen kitap bulunamadı.", 14, null, "#C9BFAE"));
            return;
        }

        foreach (var b in books)
        {
            var card = new Border
            {
                Style = (Style)FindResource("Glass"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 10, 12, 10),
                Background = Ui.B("#25FFFFFF")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            var bookItem = BookItem.FromDomain(b);
            var cov = Ui.MiniCover(bookItem, 30, 42);
            grid.Children.Add(cov);

            var titlePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            titlePanel.Children.Add(Ui.Text(b.Title, 13, FontWeights.SemiBold));
            titlePanel.Children.Add(Ui.Text(b.AuthorName, 11, null, "#C9BFAE"));
            Grid.SetColumn(titlePanel, 1);
            grid.Children.Add(titlePanel);

            var catPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Ui.B("#33C58B4A"),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.Text(b.CategoryName, 11, null, "#E4B368")
            };
            Grid.SetColumn(catPill, 2);
            grid.Children.Add(catPill);

            var shelfText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            shelfText.Children.Add(Ui.Text("Raf No", 10, null, "#A09485"));
            shelfText.Children.Add(Ui.Text(b.ShelfLocation, 12, FontWeights.SemiBold, "#EADFC8"));
            Grid.SetColumn(shelfText, 3);
            grid.Children.Add(shelfText);

            var stockText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stockText.Children.Add(Ui.Text("Stok Durumu", 10, null, "#A09485"));
            stockText.Children.Add(Ui.Text($"{b.AvailableCopies} / {b.TotalCopies}", 12, FontWeights.SemiBold, b.AvailableCopies > 0 ? "#7FD08F" : "#F08A7A"));
            Grid.SetColumn(stockText, 4);
            grid.Children.Add(stockText);

            var lendBtn = GlassBtn("\uE710", "Ödünç Ver", b.AvailableCopies > 0, () =>
            {
                _s.SetMode(TransactionMode.Lend);
                _s.Toggle(bookItem);
                ShowToast($"'{b.Title}' ödünç sepetine eklendi.");
            }, 32);
            lendBtn.IsEnabled = b.AvailableCopies > 0;
            lendBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(lendBtn, 5);
            grid.Children.Add(lendBtn);

            card.Child = grid;
            _booksContainer.Children.Add(card);
        }
    }

    // 2. Üyeler View
    private StackPanel _membersContainer = null!;
    private void ShowMembersView()
    {
        GreetingText.Text = "👥 Kayıtlı Üyeler";
        GreetingSub.Text = "Kütüphane okuyucu ve personel rehberi.";

        SubPageBody.Children.Clear();
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var addBtn = GlassBtn("\uE8FA", "Yeni Üye Ekle", true, () => SetRoute("Yeni Üye"), 38);
        addBtn.Padding = new Thickness(14, 0, 14, 0);
        DockPanel.SetDock(addBtn, Dock.Right);
        topBar.Children.Add(addBtn);

        var countText = Ui.Text($"Toplam {_db.GetMembers().Count} Üye Kayıtlı", 14, FontWeights.SemiBold, "#E4B368");
        countText.VerticalAlignment = VerticalAlignment.Center;
        topBar.Children.Add(countText);
        mainGrid.Children.Add(topBar);

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _membersContainer = new StackPanel();
        sv.Content = _membersContainer;
        Grid.SetRow(sv, 1);
        mainGrid.Children.Add(sv);

        SubPageBody.Children.Add(mainGrid);
        RenderMembersList(GlobalSearchBox.Text);
    }

    private void RenderMembersList(string? query)
    {
        if (_membersContainer == null) return;
        _membersContainer.Children.Clear();

        var members = _db.GetMembers();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLower();
            members = members.Where(m => m.FullName.ToLower().Contains(q) || m.MemberNumber.ToLower().Contains(q) || m.Email.ToLower().Contains(q) || m.PhoneNumber.Contains(q)).ToList();
        }

        foreach (var m in members)
        {
            var card = new Border
            {
                Style = (Style)FindResource("Glass"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 10, 14, 10),
                Background = Ui.B("#25FFFFFF")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            var av = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), Background = Ui.B("#3C6B4A"), Child = Ui.Icon("\uE77B", 16) };
            grid.Children.Add(av);

            var namePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            namePanel.Children.Add(Ui.Text(m.FullName, 13, FontWeights.SemiBold));
            namePanel.Children.Add(Ui.Text(m.MemberNumber, 11, null, "#E4B368"));
            Grid.SetColumn(namePanel, 1);
            grid.Children.Add(namePanel);

            var contactPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            contactPanel.Children.Add(Ui.Text(m.Email, 11, null, "#EADFC8"));
            contactPanel.Children.Add(Ui.Text(m.PhoneNumber, 10, null, "#A09485"));
            Grid.SetColumn(contactPanel, 2);
            grid.Children.Add(contactPanel);

            var tierPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Ui.B("#33FFFFFF"),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.Text(m.TierTitle, 11, null, "#FFFFFF")
            };
            Grid.SetColumn(tierPill, 3);
            grid.Children.Add(tierPill);

            var statusPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Ui.B("#337FD08F"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.Text("Aktif", 11, FontWeights.SemiBold, "#7FD08F")
            };
            Grid.SetColumn(statusPill, 4);
            grid.Children.Add(statusPill);

            card.Child = grid;
            _membersContainer.Children.Add(card);
        }
    }

    // 3. İade Masası & Envanter View
    private StackPanel _returnDeskContainer = null!;
    private void ShowReturnDeskView()
    {
        GreetingText.Text = "↩️ Emanet & İade Masası";
        GreetingSub.Text = "Üyelerdeki emanet kitapların iade alınması ve sürelerinin 15 gün uzatılması.";

        SubPageBody.Children.Clear();
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var activeLoans = _db.GetActiveLoans().Where(l => l.ReturnDate == null).ToList();
        var countText = Ui.Text($"Şu Anda Dolaşımda {activeLoans.Count} Kitap Bulunuyor", 14, FontWeights.SemiBold, "#E4B368");
        countText.VerticalAlignment = VerticalAlignment.Center;
        topBar.Children.Add(countText);
        mainGrid.Children.Add(topBar);

        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _returnDeskContainer = new StackPanel();
        sv.Content = _returnDeskContainer;
        Grid.SetRow(sv, 1);
        mainGrid.Children.Add(sv);

        SubPageBody.Children.Add(mainGrid);
        RenderReturnDeskList(GlobalSearchBox.Text);
    }

    private void ShowInventoryView()
    {
        GreetingText.Text = "📦 Envanter & Stok Yönetimi";
        GreetingSub.Text = "Kütüphane arşivindeki tüm kitapların fiziksel raf konumları ve kopya durumları.";

        SubPageBody.Children.Clear();
        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var allBooks = _db.GetBooks();
        int totalTitles = allBooks.Count;
        int totalCopies = allBooks.Sum(b => b.TotalCopies);
        int availableCopies = allBooks.Sum(b => b.AvailableCopies);
        int borrowedCopies = totalCopies - availableCopies;

        // Metric summary row
        var metricsGrid = new UniformGrid { Columns = 4, Margin = new Thickness(0, 0, 0, 16) };
        metricsGrid.Children.Add(MakeReportCard("Toplam Eser Çeşidi", totalTitles.ToString(), "Kayıtlı Başlık", "#2D4C3A", "#7FD08F"));
        metricsGrid.Children.Add(MakeReportCard("Fiziksel Kopya", totalCopies.ToString(), "Toplam Kitap Adedi", "#2D4354", "#70B0E0"));
        metricsGrid.Children.Add(MakeReportCard("Rafta Mevcut", availableCopies.ToString(), "Ödünç Alınabilir", "#3E542D", "#9FE070"));
        metricsGrid.Children.Add(MakeReportCard("Dolaşımda / Emanette", borrowedCopies.ToString(), "Okuyucularda", "#54382D", "#E4B368"));
        mainGrid.Children.Add(metricsGrid);

        var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var countText = Ui.Text($"Envanterde {totalTitles} Farklı Eser Bulunuyor", 14, FontWeights.SemiBold, "#E4B368");
        countText.VerticalAlignment = VerticalAlignment.Center;
        topBar.Children.Add(countText);
        Grid.SetRow(topBar, 1);
        mainGrid.Children.Add(topBar);

        // List
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var container = new StackPanel();
        sv.Content = container;
        Grid.SetRow(sv, 2);
        mainGrid.Children.Add(sv);

        foreach (var b in allBooks)
        {
            var card = new Border
            {
                Style = (Style)FindResource("Glass"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(16, 12, 16, 12),
                Background = Ui.B("#20FFFFFF")
            };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(Ui.Text(b.Title, 13, FontWeights.SemiBold));
            info.Children.Add(Ui.Text(b.AuthorName + " • " + b.CategoryName, 11, null, "#C9BFAE"));
            g.Children.Add(info);

            var shelf = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            shelf.Children.Add(Ui.Text($"Raf: {b.ShelfLocation}", 12, FontWeights.SemiBold, "#E4B368"));
            shelf.Children.Add(Ui.Text($"ISBN: {b.Isbn}", 10, null, "#A09485"));
            Grid.SetColumn(shelf, 1);
            g.Children.Add(shelf);

            var stock = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stock.Children.Add(Ui.Text($"{b.AvailableCopies} / {b.TotalCopies} Stok", 13, FontWeights.SemiBold, b.AvailableCopies > 0 ? "#7FD08F" : "#F08A7A"));
            stock.Children.Add(Ui.Text(b.AvailableCopies > 0 ? "Rafta Mevcut" : "Tükendi", 10, null, "#C9BFAE"));
            Grid.SetColumn(stock, 2);
            g.Children.Add(stock);

            var actionPnl = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            var lendBtn = GlassBtn("\uE710", "Hızlı Ödünç", true, () => ShowQuickLendDialog(b), 32);
            lendBtn.Padding = new Thickness(10, 0, 10, 0);
            actionPnl.Children.Add(lendBtn);
            Grid.SetColumn(actionPnl, 3);
            g.Children.Add(actionPnl);

            card.Child = g;
            container.Children.Add(card);
        }

        SubPageBody.Children.Add(mainGrid);
    }

    private void RenderReturnDeskList(string? query)
    {
        if (_returnDeskContainer == null) return;
        _returnDeskContainer.Children.Clear();

        var loans = _db.GetActiveLoans().Where(l => l.ReturnDate == null).ToList();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.ToLower();
            loans = loans.Where(l => l.BookTitle.ToLower().Contains(q) || l.MemberName.ToLower().Contains(q) || l.AuthorName.ToLower().Contains(q)).ToList();
        }

        if (!loans.Any())
        {
            _returnDeskContainer.Children.Add(Ui.Text("Şu anda iade bekleyen veya emanette kitap bulunmamaktadır.", 14, null, "#C9BFAE"));
            return;
        }

        foreach (var l in loans)
        {
            var card = new Border
            {
                Style = (Style)FindResource("Glass"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 10, 14, 10),
                Background = Ui.B("#25FFFFFF")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            var bookPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            bookPanel.Children.Add(Ui.Text(l.BookTitle, 13, FontWeights.SemiBold));
            bookPanel.Children.Add(Ui.Text(l.AuthorName, 11, null, "#C9BFAE"));
            grid.Children.Add(bookPanel);

            var memberPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            memberPanel.Children.Add(Ui.Text(l.MemberName, 12, FontWeights.SemiBold, "#EADFC8"));
            memberPanel.Children.Add(Ui.Text("Ödünç Alan Okuyucu", 10, null, "#A09485"));
            Grid.SetColumn(memberPanel, 1);
            grid.Children.Add(memberPanel);

            var datePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            datePanel.Children.Add(Ui.Text("Son Teslim", 10, null, "#A09485"));
            datePanel.Children.Add(Ui.Text(l.DueDate.ToString("dd.MM.yyyy"), 12, FontWeights.SemiBold));
            Grid.SetColumn(datePanel, 2);
            grid.Children.Add(datePanel);

            var overdueBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 4, 8, 4),
                Background = Ui.B(l.IsOverdue ? "#44F08A7A" : "#337FD08F"),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.Text(l.IsOverdue ? "GECİKMİŞ" : "SÜRESİNDE", 11, FontWeights.SemiBold, l.IsOverdue ? "#F08A7A" : "#7FD08F")
            };
            Grid.SetColumn(overdueBadge, 3);
            grid.Children.Add(overdueBadge);

            var returnBtn = GlassBtn("\uE72C", "İade Al", true, () =>
            {
                _db.ProcessReturn(l.Id, out string msg);
                RenderReturnDeskList(GlobalSearchBox.Text);
                BuildStats();
                ShowToast("Kitap başarıyla iade alındı.");
            }, 32);
            returnBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(returnBtn, 4);
            grid.Children.Add(returnBtn);

            var renewBtn = GlassBtn("\uE823", "+15 Gün", false, () =>
            {
                _db.RenewLoan(l.Id, 15, out string msg);
                RenderReturnDeskList(GlobalSearchBox.Text);
                BuildStats();
                ShowToast("Ödünç süresi 15 gün uzatıldı.");
            }, 32);
            renewBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(renewBtn, 5);
            grid.Children.Add(renewBtn);

            card.Child = grid;
            _returnDeskContainer.Children.Add(card);
        }
    }

    // 4. In-Page Yeni Kitap Formu
    private void ShowAddBookView()
    {
        GreetingText.Text = "📖 Yeni Kitap Ekle";
        GreetingSub.Text = "Kütüphane envanterine yeni bir eser kaydedin.";

        SubPageBody.Children.Clear();
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var card = new Border
        {
            Style = (Style)FindResource("Glass"),
            Width = 620,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 10),
            Padding = new Thickness(28, 22, 28, 22),
            Background = Ui.B("#25FFFFFF")
        };

        var sp = new StackPanel();

        TextBox txtTitle = AddFormField(sp, "Kitap Başlığı (*)", "Örn: Nutuk");
        TextBox txtAuthor = AddFormField(sp, "Yazar Adı (*)", "Örn: Mustafa Kemal Atatürk");
        TextBox txtIsbn = AddFormField(sp, "ISBN Numarası", "Örn: 978-975-1234-56-7");

        var lblCat = Ui.Text("Kategori", 12, FontWeights.SemiBold, "#EADFC8");
        lblCat.Margin = new Thickness(0, 6, 0, 4);
        sp.Children.Add(lblCat);

        var cmbCategory = new ComboBox
        {
            Height = 36,
            Margin = new Thickness(0, 0, 0, 12),
            Background = Ui.B("#22FFFFFF"),
            Foreground = Ui.B("#1A0F08"),
            FontSize = 13
        };
        foreach (var cat in new[] { "Dünya Klasikleri", "Bilim Kurgu & Fantastik", "Tarih & Felsefe", "Türk Edebiyatı", "Çocuk & Gençlik" })
            cmbCategory.Items.Add(cat);
        cmbCategory.SelectedIndex = 0;
        sp.Children.Add(cmbCategory);

        TextBox txtShelf = AddFormField(sp, "Raf Konumu", "Örn: A-04-12");
        TextBox txtCopies = AddFormField(sp, "Toplam Kopya Adedi", "5");

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelBtn = GlassBtn("\uE711", "Vazgeç", false, () => SetRoute("Kitaplar"), 38);
        cancelBtn.Margin = new Thickness(0, 0, 12, 0);

        var saveBtn = GlassBtn("\uE73E", "Kitabı Kaydet", true, () =>
        {
            if (string.IsNullOrWhiteSpace(txtTitle.Text) || string.IsNullOrWhiteSpace(txtAuthor.Text))
            {
                ShowModal("Eksik Bilgi", "Lütfen Kitap Başlığı ve Yazar adını doldurunuz.", "Tamam", () => { }, DialogType.Warning, cancelText: "");
                return;
            }

            int.TryParse(txtCopies.Text, out int totalCopies);
            if (totalCopies <= 0) totalCopies = 5;

            var newBook = new Book
            {
                Title = txtTitle.Text.Trim(),
                AuthorName = txtAuthor.Text.Trim(),
                Isbn = string.IsNullOrWhiteSpace(txtIsbn.Text) ? Guid.NewGuid().ToString()[..13] : txtIsbn.Text.Trim(),
                CategoryName = cmbCategory.SelectedItem?.ToString() ?? "Genel",
                ShelfLocation = string.IsNullOrWhiteSpace(txtShelf.Text) ? "A-01" : txtShelf.Text.Trim(),
                TotalCopies = totalCopies,
                AvailableCopies = totalCopies,
                PublisherName = "Fırat Üniversitesi Yayınları",
                CategoryTheme = BookCategoryTheme.ClassicLiterature,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover
            };

            _db.AddNewBook(newBook);
            _s.SetMode(TransactionMode.Add);
            _s.Toggle(BookItem.FromDomain(newBook));
            RefreshAllViews();
            ShowToast($"'{newBook.Title}' başarıyla envantere eklendi.");
            SetRoute("Kitaplar");
        }, 38);

        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(saveBtn);
        sp.Children.Add(btnPanel);

        card.Child = sp;
        sv.Content = card;
        SubPageBody.Children.Add(sv);
    }

    // 5. In-Page Yeni Üye Formu
    private void ShowAddMemberView()
    {
        GreetingText.Text = "👤 Yeni Üye Kaydı";
        GreetingSub.Text = "Kütüphane sistemine yeni bir okuyucu kaydedin.";

        SubPageBody.Children.Clear();
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };

        var card = new Border
        {
            Style = (Style)FindResource("Glass"),
            Width = 580,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 10),
            Padding = new Thickness(28, 22, 28, 22),
            Background = Ui.B("#25FFFFFF")
        };

        var sp = new StackPanel();

        TextBox txtName = AddFormField(sp, "Ad Soyad (*)", "Örn: Ahmet Yılmaz");
        TextBox txtNumber = AddFormField(sp, "Öğrenci / Üye No (*)", "Örn: 220101045");
        TextBox txtEmail = AddFormField(sp, "E-posta Adresi", "Örn: ahmet.yilmaz@firat.edu.tr");
        TextBox txtPhone = AddFormField(sp, "Telefon Numarası", "Örn: +90 532 123 45 67");

        var lblTier = Ui.Text("Üyelik Grubu", 12, FontWeights.SemiBold, "#EADFC8");
        lblTier.Margin = new Thickness(0, 6, 0, 4);
        sp.Children.Add(lblTier);

        var cmbTier = new ComboBox
        {
            Height = 36,
            Margin = new Thickness(0, 0, 0, 12),
            Background = Ui.B("#22FFFFFF"),
            Foreground = Ui.B("#1A0F08"),
            FontSize = 13
        };
        foreach (var tier in new[] { "Lisans Öğrencisi", "Yüksek Lisans / Doktora", "Akademisyen", "Kütüphane Personeli", "Standart Okuyucu" })
            cmbTier.Items.Add(tier);
        cmbTier.SelectedIndex = 0;
        sp.Children.Add(cmbTier);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancelBtn = GlassBtn("\uE711", "Vazgeç", false, () => SetRoute("Üyeler"), 38);
        cancelBtn.Margin = new Thickness(0, 0, 12, 0);

        var saveBtn = GlassBtn("\uE73E", "Üyeyi Kaydet", true, () =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || string.IsNullOrWhiteSpace(txtNumber.Text))
            {
                ShowModal("Eksik Bilgi", "Lütfen Ad Soyad ve Üye Numarasını doldurunuz.", "Tamam", () => { }, DialogType.Warning, cancelText: "");
                return;
            }

            var newMember = new Member
            {
                FullName = txtName.Text.Trim(),
                MemberNumber = txtNumber.Text.Trim(),
                Email = string.IsNullOrWhiteSpace(txtEmail.Text) ? $"{txtNumber.Text.Trim()}@firat.edu.tr" : txtEmail.Text.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(txtPhone.Text) ? "Belirtilmedi" : txtPhone.Text.Trim(),
                TierTitle = cmbTier.SelectedItem?.ToString() ?? "Lisans Öğrencisi",
                Status = MemberStatus.Active,
                MaxLoanLimit = 5,
                RegistrationDate = DateTime.Today
            };

            _db.AddNewMember(newMember);
            RefreshAllViews();
            ShowToast($"'{newMember.FullName}' başarıyla üye kaydedildi.");
            SetRoute("Üyeler");
        }, 38);

        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(saveBtn);
        sp.Children.Add(btnPanel);

        card.Child = sp;
        sv.Content = card;
        SubPageBody.Children.Add(sv);
    }

    private TextBox AddFormField(StackPanel parent, string label, string hint)
    {
        var lbl = Ui.Text(label, 12, FontWeights.SemiBold, "#EADFC8");
        lbl.Margin = new Thickness(0, 6, 0, 4);
        parent.Children.Add(lbl);

        var txt = new TextBox
        {
            Height = 36,
            Margin = new Thickness(0, 0, 0, 8),
            Background = Ui.B("#22FFFFFF"),
            BorderBrush = Ui.B("#44FFFFFF"),
            Foreground = Ui.B("#FFFFFF"),
            CaretBrush = Ui.B("#E4B368"),
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            ToolTip = hint
        };
        parent.Children.Add(txt);
        return txt;
    }

    // 6. In-Page Rezervasyonlar
    private void ShowReservationsView()
    {
        GreetingText.Text = "📌 Kitap Rezervasyonları";
        GreetingSub.Text = "Bekleyen ve aktif kitap rezervasyon kayıtları.";

        SubPageBody.Children.Clear();
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var sp = new StackPanel();

        var resList = _db.GetReservations();
        foreach (var r in resList)
        {
            var card = new Border
            {
                Style = (Style)FindResource("Glass"),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(14, 10, 14, 10),
                Background = Ui.B("#25FFFFFF")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });

            var bookPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            bookPanel.Children.Add(Ui.Text(r.BookTitle, 13, FontWeights.SemiBold));
            bookPanel.Children.Add(Ui.Text($"Sıra No: #{r.QueuePosition}", 11, null, "#E4B368"));
            grid.Children.Add(bookPanel);

            var memberPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            memberPanel.Children.Add(Ui.Text(r.MemberName, 12, FontWeights.SemiBold, "#EADFC8"));
            memberPanel.Children.Add(Ui.Text("Talep Sahibi", 10, null, "#A09485"));
            Grid.SetColumn(memberPanel, 1);
            grid.Children.Add(memberPanel);

            var datePanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            datePanel.Children.Add(Ui.Text("Talep Tarihi", 10, null, "#A09485"));
            datePanel.Children.Add(Ui.Text(r.RequestedAt.ToString("dd.MM.yyyy"), 12, FontWeights.SemiBold));
            Grid.SetColumn(datePanel, 2);
            grid.Children.Add(datePanel);

            var statusBadge = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 4, 10, 4),
                Background = Ui.B("#33E4B368"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = Ui.Text(r.Status.ToString(), 11, FontWeights.SemiBold, "#E4B368")
            };
            Grid.SetColumn(statusBadge, 3);
            grid.Children.Add(statusBadge);

            card.Child = grid;
            sp.Children.Add(card);
        }

        sv.Content = sp;
        SubPageBody.Children.Add(sv);
    }

    // 7. In-Page Raporlar View
    private void ShowReportsView()
    {
        GreetingText.Text = "📊 Dolaşım & İşlem Raporları";
        GreetingSub.Text = "Kütüphane kullanım istatistikleri ve aylık devir hızı.";

        SubPageBody.Children.Clear();
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var mainGrid = new Grid { Margin = new Thickness(0, 10, 0, 10) };
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(260) });

        // Metric summary row
        var metricsGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 16) };
        metricsGrid.Children.Add(MakeReportCard("Aylık Devir Hızı", "%87.4", "Ödünç / Mevcut Oranı", "#3A5E45", "#7FD08F"));
        metricsGrid.Children.Add(MakeReportCard("Zamanında İade", "%96.2", "Gecikmesiz İade Başarısı", "#3A6050", "#7FD08F"));
        metricsGrid.Children.Add(MakeReportCard("En Popüler Kategori", "Dünya Klasikleri", "Aylık 412 İşlem", "#6A5230", "#E4B368"));
        mainGrid.Children.Add(metricsGrid);

        // Chart Card
        var chartCard = new Border
        {
            Style = (Style)FindResource("Glass"),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(20),
            Background = Ui.B("#25FFFFFF")
        };
        Grid.SetRow(chartCard, 1);

        var chartSp = new DockPanel();
        var head = Header("\uE9D9", "Aylık Ödünç Alma Hacmi");
        DockPanel.SetDock(head, Dock.Top);
        chartSp.Children.Add(head);

        var stats = _db.GetMonthlyBorrowStats();
        var barGrid = new Grid();
        barGrid.RowDefinitions.Add(new RowDefinition());
        barGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int i = 0; i < stats.Count; i++)
        {
            barGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = new Border
            {
                Width = 26,
                Height = stats[i].BorrowedCount / 800.0 * 140,
                VerticalAlignment = VerticalAlignment.Bottom,
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Background = new LinearGradientBrush(Ui.C("#8FE0A0"), Ui.C("#2F6B46"), 90),
                ToolTip = $"{stats[i].MonthLabel}: {stats[i].BorrowedCount} Kitap"
            };
            Grid.SetColumn(bar, i);
            var lab = Ui.Text(stats[i].MonthLabel, 11, null, "#C9BFAE");
            lab.HorizontalAlignment = HorizontalAlignment.Center;
            lab.Margin = new Thickness(0, 8, 0, 0);
            Grid.SetColumn(lab, i);
            Grid.SetRow(lab, 1);
            barGrid.Children.Add(bar);
            barGrid.Children.Add(lab);
        }
        chartSp.Children.Add(barGrid);
        chartCard.Child = chartSp;
        mainGrid.Children.Add(chartCard);

        sv.Content = mainGrid;
        SubPageBody.Children.Add(sv);
    }

    private Border MakeReportCard(string title, string value, string sub, string tint, string ic)
    {
        var b = new Border
        {
            Style = (Style)FindResource("Glass"),
            Margin = new Thickness(6),
            Padding = new Thickness(16, 14, 16, 14),
            Background = new LinearGradientBrush(Ui.C(tint).WithAlpha(0x99), Ui.C("#14FFFFFF"), 45)
        };
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(title, 11, null, "#C9BFAE"));
        sp.Children.Add(Ui.Text(value, 22, FontWeights.SemiBold, ic));
        sp.Children.Add(Ui.Text(sub, 10, null, "#EADFC8"));
        b.Child = sp;
        return b;
    }

    // 8. In-Page Ayarlar View
    private void ShowSettingsView()
    {
        GreetingText.Text = "⚙️ Sistem Ayarları";
        GreetingSub.Text = "Kütüphane işletim kuralları, gecikme bedelleri ve sistem yapılandırması.";

        SubPageBody.Children.Clear();
        var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var sp = new StackPanel { MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 10) };

        sp.Children.Add(MakeSettingGroup("📚 Ödünç Verme Politikaları", new[]
        {
            ("Standart Ödünç Süresi", "15 Gün"),
            ("Maksimum Kitap Limiti", "5 Kitap / Üye"),
            ("Maksimum Süre Uzatma Hakkı", "2 Kez (+15 Gün)")
        }));

        sp.Children.Add(MakeSettingGroup("💰 Ceza & Ücret Tarifesi", new[]
        {
            ("Günlük Gecikme Bedeli", "2.50 ₺ / gün"),
            ("Kayıp Kitap Tazmin Bedeli", "Kitap Bedeli + %20 İşlem Masrafı")
        }));

        sp.Children.Add(MakeSettingGroup("💻 Altyapı & Entegrasyon", new[]
        {
            ("Grafik & Görselleştirme Motoru", "WPF Direct3D Donanım Hızlandırmalı"),
            ("Veritabanı Katmanı", _db.DatabaseStatusText),
            ("Yazılım Versiyonu", "Fırat Kütüphane v2.5 (Glassmorphic)")
        }));

        sv.Content = sp;
        SubPageBody.Children.Add(sv);
    }

    private Border MakeSettingGroup(string header, (string Label, string Value)[] items)
    {
        var card = new Border
        {
            Style = (Style)FindResource("Glass"),
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 0, 14),
            Padding = new Thickness(20, 16, 20, 16),
            Background = Ui.B("#25FFFFFF")
        };
        var sp = new StackPanel();
        sp.Children.Add(Ui.Text(header, 14, FontWeights.SemiBold, "#E4B368"));
        sp.Children.Add(new Rectangle { Height = 1, Fill = Ui.B("#33FFFFFF"), Margin = new Thickness(0, 8, 0, 12) });

        foreach (var (lbl, val) in items)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(Ui.Text(lbl, 13, null, "#EADFC8"));
            var v = Ui.Text(val, 13, FontWeights.SemiBold, "#7FD08F");
            Grid.SetColumn(v, 1);
            g.Children.Add(v);
            sp.Children.Add(g);
        }
        card.Child = sp;
        return card;
    }

    // 9. In-Page 3D Müşteri & Kitap Önizleme Ekranı
    private void ShowCustomer3DView()
    {
        GreetingText.Text = "🎨 3D Müşteri & Kitap Deneyimi";
        GreetingSub.Text = "Gerçek zamanlı üç boyutlu kitap animasyonu ve sayaç.";

        SubPageBody.Children.Clear();
        var grid = new Grid();

        var vp = new Viewport3D { ClipToBounds = true };
        var cam = new PerspectiveCamera
        {
            Position = new Point3D(0, 2.05, 6.5),
            LookDirection = new Vector3D(0, -0.35, -6.5),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 67,
            NearPlaneDistance = 0.1,
            FarPlaneDistance = 60
        };
        vp.Camera = cam;

        var scene = new ModelVisual3D();
        var mg = new Model3DGroup();
        mg.Children.Add(new AmbientLight(Color.FromRgb(40, 32, 28)));
        mg.Children.Add(new DirectionalLight(Color.FromRgb(255, 230, 200), new Vector3D(-0.4, -0.8, -0.4)));
        scene.Content = mg;
        vp.Children.Add(scene);

        var booksRoot = new ModelVisual3D();
        vp.Children.Add(booksRoot);

        // Add 3D books from checkout state
        var bGroup = new Model3DGroup();
        var selBooks = _s.Books.ToList();
        double startX = -((selBooks.Count - 1) * 0.45) / 2.0;

        for (int i = 0; i < selBooks.Count; i++)
        {
            var book = selBooks[i];
            var mesh = new MeshGeometry3D();
            double x = startX + i * 0.45;
            double w = 0.35, h = 0.55, d = 0.1;

            mesh.Positions.Add(new Point3D(x - w / 2, 0, d / 2));
            mesh.Positions.Add(new Point3D(x + w / 2, 0, d / 2));
            mesh.Positions.Add(new Point3D(x + w / 2, h, d / 2));
            mesh.Positions.Add(new Point3D(x - w / 2, h, d / 2));
            mesh.Positions.Add(new Point3D(x - w / 2, 0, -d / 2));
            mesh.Positions.Add(new Point3D(x + w / 2, 0, -d / 2));
            mesh.Positions.Add(new Point3D(x + w / 2, h, -d / 2));
            mesh.Positions.Add(new Point3D(x - w / 2, h, -d / 2));

            int[] triangles = { 0,1,2, 0,2,3, 5,4,7, 5,7,6, 4,0,3, 4,3,7, 1,5,6, 1,6,2, 3,2,6, 3,6,7, 4,5,1, 4,1,0 };
            foreach (var t in triangles) mesh.TriangleIndices.Add(t);

            var mat = new DiffuseMaterial(new SolidColorBrush(Ui.C(book.Cover)));
            bGroup.Children.Add(new GeometryModel3D(mesh, mat));
        }
        booksRoot.Content = bGroup;

        grid.Children.Add(vp);

        var overlay = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20),
            Padding = new Thickness(20, 10, 20, 10),
            CornerRadius = new CornerRadius(14),
            Background = Ui.B("#88000000"),
            Child = Ui.Text($"Sepetteki Kitap: {_s.Books.Count} Adet | Mod: {_s.Mode}", 14, FontWeights.SemiBold, "#E4B368")
        };
        grid.Children.Add(overlay);

        SubPageBody.Children.Add(grid);
    }
    #endregion

    #region Dashboard Building Components
    Button GlassBtn(string glyph, string text, bool primary, Action click, double h = 42)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var ic = Ui.Icon(glyph, 15); ic.Margin = new Thickness(0, 0, 10, 0);
        sp.Children.Add(ic); sp.Children.Add(Ui.Text(text, 13, FontWeights.SemiBold));
        var b = new Button { Content = sp, Height = h, Style = (Style)FindResource("GlassButton") };
        if (primary) { b.Background = new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90); b.BorderBrush = Ui.B("#8FD6A0"); }
        b.Click += (_, _) => click();
        return b;
    }

    static StackPanel Header(string glyph, string title, string? right = null)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var ic = Ui.Icon(glyph, 14, "#E4B368"); ic.Margin = new Thickness(0, 0, 8, 0);
        sp.Children.Add(ic); sp.Children.Add(Ui.Text(title, 13, FontWeights.SemiBold));
        if (right != null) { var r = Ui.Text(right, 11, null, "#C9BFAE"); r.Margin = new Thickness(60, 0, 0, 0); sp.Children.Add(r); }
        return sp;
    }

    void BuildToday()
    {
        var h = (StackPanel)FindName("TodayHost");
        h.Children.Clear();
        h.Children.Add(Header("\uE787", "Günün İşlemleri", "Bugün, " + DateTime.Now.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"))));

        var acts = _db.GetRecentActivity();
        int lendCount = acts.Count(a => a.Timestamp.Date == DateTime.Today && a.Title.Contains("Ödünç"));
        int returnCount = acts.Count(a => a.Timestamp.Date == DateTime.Today && a.Title.Contains("İade"));
        int memberCount = acts.Count(a => a.Timestamp.Date == DateTime.Today && a.Title.Contains("Üye"));
        int resCount = _db.GetReservations().Count;

        if (lendCount == 0)
        {
            lendCount = _db.GetActiveLoans().Count(l => l.BorrowDate.Date == DateTime.Today);
        }

        (string i, string l, int v)[] rows = {
            ("\uE8EF", "Ödünç Verme", lendCount),
            ("\uE8FB", "İade Alma", returnCount),
            ("\uE77B", "Yeni Üye", memberCount),
            ("\uE8D4", "Rezervasyon", resCount)
        };

        foreach (var r in rows)
        {
            var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            var lab = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = Ui.Icon(r.i, 13, "#C9BFAE"); ic.Margin = new Thickness(0, 0, 8, 0);
            lab.Children.Add(ic); lab.Children.Add(Ui.Text(r.l, 12));
            var bar = new Grid();
            int fill = Math.Clamp(r.v, 0, 20);
            int rest = Math.Max(1, 20 - fill);
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fill, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rest, GridUnitType.Star) });
            bar.Children.Add(new Border { CornerRadius = new CornerRadius(3), Background = Ui.B("#7FD08F") });
            var track = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = Ui.B("#26FFFFFF"), Child = bar, Margin = new Thickness(6, 0, 10, 0) };
            Grid.SetColumn(track, 1);
            var val = Ui.Text(r.v.ToString(), 12, FontWeights.SemiBold); Grid.SetColumn(val, 2);
            g.Children.Add(lab); g.Children.Add(track); g.Children.Add(val); h.Children.Add(g);
        }
    }

    Border BookRow(BookItem b, FrameworkElement content)
    {
        var r = new Border { Padding = new Thickness(6, 4, 6, 4), CornerRadius = new CornerRadius(10), Background = Brushes.Transparent, Cursor = Cursors.Hand, Child = content };
        r.MouseLeftButtonUp += (_, _) => _s.Toggle(b);
        _rows.Add((b, r)); return r;
    }

    void BuildRecent()
    {
        var h = (StackPanel)FindName("RecentHost");
        h.Children.Clear();
        h.Children.Add(Header("\uE81C", "Son İşlemler"));
        var activities = _db.GetRecentActivity().Take(4).ToList();
        var allDomain = _db.GetBooks();

        foreach (var act in activities)
        {
            var match = allDomain.FirstOrDefault(b => act.Description.Contains(b.Title)) ?? allDomain.FirstOrDefault();
            if (match == null) continue;
            var bookItem = BookItem.FromDomain(match);

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
            var cov = Ui.MiniCover(bookItem, 26, 36);
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(Ui.Text(bookItem.Title, 12, FontWeights.SemiBold)); t.Children.Add(Ui.Text(bookItem.Author, 10, null, "#C9BFAE"));
            Grid.SetColumn(t, 1);
            var pill = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 3, 10, 3), Background = Ui.B("#556FBF7F"), VerticalAlignment = VerticalAlignment.Center, Child = Ui.Text(act.Title, 10, null, "#C9F0CF") };
            Grid.SetColumn(pill, 2);
            var diff = DateTime.Now - act.Timestamp;
            string agoStr = diff.TotalMinutes < 1 ? "az önce" : diff.TotalMinutes < 60 ? $"{(int)diff.TotalMinutes} dk" : $"{act.Timestamp:HH:mm}";
            var ago = Ui.Text(agoStr, 10, null, "#C9BFAE"); ago.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(ago, 3);
            g.Children.Add(cov); g.Children.Add(t); g.Children.Add(pill); g.Children.Add(ago);
            h.Children.Add(BookRow(bookItem, g));
        }
    }

    void BuildChart()
    {
        var host = (DockPanel)FindName("ChartHost");
        host.Children.Clear();
        var head = Header("\uE9D9", "Aylık Ödünç Alma"); DockPanel.SetDock(head, Dock.Top); host.Children.Add(head);
        var stats = _db.GetMonthlyBorrowStats();
        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition()); g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int i = 0; i < stats.Count; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var bar = new Border
            {
                Width = 16, Height = stats[i].BorrowedCount / 800.0 * 100, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(3, 3, 0, 0),
                Background = new LinearGradientBrush(Ui.C("#8FE0A0"), Ui.C("#2F6B46"), 90),
                Cursor = Cursors.Hand,
                ToolTip = $"{stats[i].MonthLabel}: {stats[i].BorrowedCount} Kitap"
            };
            Grid.SetColumn(bar, i);
            var lab = Ui.Text(stats[i].MonthLabel, 9, null, "#C9BFAE"); lab.HorizontalAlignment = HorizontalAlignment.Center; lab.Margin = new Thickness(0, 4, 0, 0);
            Grid.SetColumn(lab, i); Grid.SetRow(lab, 1);
            g.Children.Add(bar); g.Children.Add(lab);
        }
        host.Children.Add(g);
    }

    void BuildQuick()
    {
        var host = (DockPanel)FindName("QuickHost");
        host.Children.Clear();
        var head = Header("\uE8A1", "Hızlı Erişim"); DockPanel.SetDock(head, Dock.Top); host.Children.Add(head);
        var grid = new UniformGrid { Columns = 3, Rows = 2 };

        (string g, string l, string color, Action onClick)[] q =
        {
            ("\uE8F1", "Kitap Kataloğu", "#5FAE72", () => SetRoute("Kitaplar")),
            ("\uE716", "Üye Listesi", "#5CAFEA", () => SetRoute("Üyeler")),
            ("\uE787", "Rezervasyonlar", "#E4B368", () => SetRoute("Rezervasyonlar")),
            ("\uE9D9", "Raporlar", "#A37CE5", () => SetRoute("Raporlar")),
            ("\uE7B8", "Envanter", "#E57C5A", () => SetRoute("Envanter")),
            ("\uE713", "Sistem Ayarları", "#58C4BE", () => SetRoute("Ayarlar"))
        };
        foreach (var item in q)
        {
            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var icoBox = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(11),
                Background = Ui.B(item.color + "28"),
                BorderBrush = Ui.B(item.color + "55"),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 7),
                Child = Ui.Icon(item.g, 18, item.color)
            };
            sp.Children.Add(icoBox);
            var t = Ui.Text(item.l, 12, FontWeights.SemiBold, "#F4EFE6");
            t.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(t);

            var btn = new Button
            {
                Content = sp,
                Margin = new Thickness(4),
                Height = 84,
                Style = (Style)FindResource("GlassButton"),
                Background = Ui.B("#22FFFFFF"),
                BorderBrush = Ui.B("#33FFFFFF"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = $"{item.l} ekranına git"
            };
            btn.Click += (_, _) => item.onClick();
            grid.Children.Add(btn);
        }
        host.Children.Add(grid);
    }

    void BuildTop()
    {
        var h = (StackPanel)FindName("TopHost");
        h.Children.Clear();
        h.Children.Add(Header("\uE735", "En Çok Ödünç Verilen Kitaplar"));
        var topBooks = _db.GetMostBorrowedBooks(3);

        for (int i = 0; i < topBooks.Count; i++)
        {
            var domainB = topBooks[i];
            var b = BookItem.FromDomain(domainB);
            var count = domainB.MonthlyBorrowCount;

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) }); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
            var rank = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = Ui.B("#33FFFFFF"), Child = Ui.Text((i + 1).ToString(), 11), HorizontalAlignment = HorizontalAlignment.Left };
            ((TextBlock)rank.Child).HorizontalAlignment = HorizontalAlignment.Center;
            var cov = Ui.MiniCover(b, 28, 40); Grid.SetColumn(cov, 1);
            var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            t.Children.Add(Ui.Text(b.Title, 12, FontWeights.SemiBold)); t.Children.Add(Ui.Text(b.Author, 10, null, "#C9BFAE")); Grid.SetColumn(t, 2);
            var pill = new Border { CornerRadius = new CornerRadius(10), Background = Ui.B("#556FBF7F"), Padding = new Thickness(0, 3, 0, 3), Child = Ui.Text(count.ToString(), 11, FontWeights.SemiBold) };
            ((TextBlock)pill.Child).HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(pill, 3);
            g.Children.Add(rank); g.Children.Add(cov); g.Children.Add(t); g.Children.Add(pill);
            h.Children.Add(BookRow(b, g));
        }
    }
    #endregion
}

internal static class ColorExt
{
    public static Color WithAlpha(this Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);
}