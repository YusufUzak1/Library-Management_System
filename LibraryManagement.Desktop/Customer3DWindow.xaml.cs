using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

namespace LibraryManagement.Desktop;

public partial class Customer3DWindow : Window
{
    readonly CheckoutState _s = CheckoutState.Instance;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Dictionary<BookItem, Book3DEntry> _activeBooks = new();
    readonly List<Book3DEntry> _dyingBooks = new();
    readonly Model3DGroup _sceneGroup = new();
    readonly PointLight _flashLight = new(Color.FromRgb(0x6F, 0xBF, 0x7F), new Point3D(0, 2.4, 1.8)) { Range = 10, ConstantAttenuation = 1, LinearAttenuation = 0.25 };
    double _flashIntensity;
    double _mx, _my, _cx, _cy;
    Stage _lastStage = Stage.Idle;
    bool _full;

    sealed class Book3DEntry
    {
        public required BookItem Book { get; init; }
        public required ModelVisual3D Visual { get; init; }
        public required TranslateTransform3D Translate { get; init; }
        public required AxisAngleRotation3D RotY { get; init; }
        public required AxisAngleRotation3D RotZ { get; init; }
        public required ScaleTransform3D Scale { get; init; }
        public double BornTime { get; init; }
        public double OutTime { get; set; }
        public double TargetY { get; set; }
        public double TargetRY { get; set; }
    }

    string _selectedCategory = "Tümü";

    public Customer3DWindow()
    {
        InitializeComponent();
        _s.SetMode(TransactionMode.Lend);
        _s.Books.Clear(); // Başlangıçta masaya hiçbir kitap konmaz

        Build3DScene();
        SceneRoot.Content = _sceneGroup;

        BuildCategoryFilters();

        _s.Changed += RefreshUIAndBooks;
        RefreshUIAndBooks();

        Loaded += (_, _) =>
        {
            CompositionTarget.Rendering += OnRenderFrame;
        };
        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRenderFrame;
            _s.Changed -= RefreshUIAndBooks;
        };
    }

    public void GoFullScreen()
    {
        _full = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        ControlBar.Visibility = Visibility.Collapsed;
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && e.OriginalSource is not Button && e.OriginalSource is not TextBox)
        {
            try { DragMove(); } catch { }
        }
    }

    private void OnMinimizeCustomer(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnToggleFullscreenCustomer(object sender, RoutedEventArgs e)
    {
        if (_full)
        {
            _full = false;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            ControlBar.Visibility = Visibility.Visible;
        }
        else GoFullScreen();
    }

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            if (_full)
            {
                _full = false;
                WindowState = WindowState.Normal;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.CanResize;
                ControlBar.Visibility = Visibility.Visible;
            }
            else GoFullScreen();
        }
        else if (e.Key == Key.Escape && _full)
        {
            _full = false;
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            ControlBar.Visibility = Visibility.Visible;
        }
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var pos = e.GetPosition(this);
        _mx = pos.X / ActualWidth - 0.5;
        _my = pos.Y / ActualHeight - 0.5;
    }

    void OnToggleMode(object sender, RoutedEventArgs e) => _s.ToggleMode();
    void OnAddBook(object sender, RoutedEventArgs e) => _s.AddNext();
    void OnRemoveBook(object sender, RoutedEventArgs e) => _s.RemoveLast();
    void OnConfirm(object sender, RoutedEventArgs e) => _s.Confirm();

    private void BuildCategoryFilters()
    {
        string[] cats = { "Tümü", "Dünya Klasikleri", "Bilim Kurgu & Fantastik", "Tarih & Felsefe", "Türk Edebiyatı", "Çocuk & Gençlik" };
        CategoryFiltersHost.Children.Clear();
        foreach (var c in cats)
        {
            var btn = new Button
            {
                Content = c,
                Height = 36,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(16, 0, 16, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("GlassButton"),
                Cursor = Cursors.Hand
            };
            bool isActive = c == _selectedCategory;
            btn.Background = isActive ? new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90) : Ui.B("#22FFFFFF");
            btn.BorderBrush = Ui.B(isActive ? "#8FD6A0" : "#44FFFFFF");
            btn.Foreground = Ui.B(isActive ? "#FFFFFF" : "#E3DACB");
            btn.Click += (_, _) =>
            {
                _selectedCategory = c;
                BuildCategoryFilters();
                RenderSearchResults();
            };
            CategoryFiltersHost.Children.Add(btn);
        }
    }

    private void OnOpenSearchOverlay(object sender, RoutedEventArgs e)
    {
        SearchOverlay.Visibility = Visibility.Visible;
        RenderSearchResults();
        UpdateSelectedCounter();
    }

    private void OnCloseSearchOverlay(object sender, RoutedEventArgs e)
    {
        SearchOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnCustomerSearchChanged(object sender, TextChangedEventArgs e)
    {
        CustomerSearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(CustomerSearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        RenderSearchResults();
    }

    private void UpdateSelectedCounter()
    {
        SelectedCountText.Text = $"Masanızda {_s.Books.Count} kitap var";
    }

    private void RenderSearchResults()
    {
        if (SearchResultsHost == null) return;
        SearchResultsHost.Children.Clear();

        var query = CustomerSearchBox.Text.Trim().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
        var allDomain = _s.DataService.GetBooks();

        var filtered = allDomain.Where(b =>
        {
            bool matchCat = _selectedCategory == "Tümü" || b.CategoryName.Equals(_selectedCategory, StringComparison.OrdinalIgnoreCase);
            if (!matchCat) return false;

            if (string.IsNullOrWhiteSpace(query)) return true;

            return b.Title.ToLower(CultureInfo.GetCultureInfo("tr-TR")).Contains(query) ||
                   b.AuthorName.ToLower(CultureInfo.GetCultureInfo("tr-TR")).Contains(query) ||
                   b.Isbn.ToLower().Contains(query) ||
                   b.CategoryName.ToLower().Contains(query);
        }).ToList();

        SearchStatusText.Text = $"Toplam {filtered.Count} eser listeleniyor" + (_selectedCategory != "Tümü" ? $" ({_selectedCategory})" : "");
        UpdateSelectedCounter();

        if (!filtered.Any())
        {
            var emptyPanel = new StackPanel { Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center };
            emptyPanel.Children.Add(Ui.Text("Aradığınız kriterlere uygun kitap bulunamadı.", 16, FontWeights.SemiBold, "#EADFC8"));
            emptyPanel.Children.Add(Ui.Text("Farklı bir arama kelimesi deneyebilir veya diğer kategorileri seçebilirsiniz.", 13, null, "#C9BFAE"));
            SearchResultsHost.Children.Add(emptyPanel);
            return;
        }

        foreach (var b in filtered)
        {
            var bookItem = BookItem.FromDomain(b);
            bool isAdded = _s.Books.Contains(bookItem);

            var card = new Border
            {
                Width = 470,
                Height = 136,
                Margin = new Thickness(0, 0, 16, 14),
                CornerRadius = new CornerRadius(16),
                Background = Ui.B("#2EFFFFFF"),
                BorderBrush = Ui.B(isAdded ? "#6FBF7F" : "#44FFFFFF"),
                BorderThickness = new Thickness(isAdded ? 1.5 : 1),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(145) });

            // Cover
            var cov = Ui.MiniCover(bookItem, 54, 76);
            cov.VerticalAlignment = VerticalAlignment.Center;
            cov.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Children.Add(cov);

            // Info
            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 10, 0) };
            info.Children.Add(Ui.Text(b.Title, 14.5, FontWeights.SemiBold, "#FFFDF9"));
            info.Children.Add(Ui.Text(b.AuthorName, 12, null, "#ECE4D6"));

            var tagRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var catPill = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                Background = Ui.B("#33C58B4A"),
                Margin = new Thickness(0, 0, 8, 0),
                Child = Ui.Text(b.CategoryName, 10.5, null, "#E4B368")
            };
            var shelfPill = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2),
                Background = Ui.B("#22FFFFFF"),
                Child = Ui.Text($"Raf {b.ShelfLocation}", 10.5, null, "#DCD2C2")
            };
            tagRow.Children.Add(catPill);
            tagRow.Children.Add(shelfPill);
            info.Children.Add(tagRow);

            var stockText = Ui.Text(b.AvailableCopies > 0 ? $"Mevcut ({b.AvailableCopies} adet)" : "Tükendi", 11, FontWeights.SemiBold, b.AvailableCopies > 0 ? "#7FD08F" : "#F08A7A");
            stockText.Margin = new Thickness(0, 4, 0, 0);
            info.Children.Add(stockText);

            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            // Action Button
            var actionBtn = new Button
            {
                Height = 38,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Style = (Style)FindResource("GlassButton")
            };

            void UpdateBtnState()
            {
                bool added = _s.Books.Contains(bookItem);
                if (added)
                {
                    actionBtn.Content = "✓ Masada (Çıkar)";
                    actionBtn.Background = Ui.B("#33C58B4A");
                    actionBtn.BorderBrush = Ui.B("#E4B368");
                    actionBtn.Foreground = Ui.B("#FFFDF9");
                    card.BorderBrush = Ui.B("#6FBF7F");
                }
                else
                {
                    actionBtn.Content = "➕ Masaya Ekle";
                    actionBtn.Background = new LinearGradientBrush(Ui.C("#5FAE72"), Ui.C("#2F6B46"), 90);
                    actionBtn.BorderBrush = Ui.B("#8FD6A0");
                    actionBtn.Foreground = Ui.B("#FFFFFF");
                    card.BorderBrush = Ui.B("#44FFFFFF");
                }
            }

            UpdateBtnState();

            actionBtn.Click += (_, _) =>
            {
                bool willAdd = !_s.Books.Contains(bookItem);
                _s.Toggle(bookItem);
                UpdateBtnState();
                UpdateSelectedCounter();

                if (willAdd)
                {
                    ShowToast($"'{b.Title}' masanıza eklendi!");
                }
                else
                {
                    ShowToast($"'{b.Title}' masadan kaldırıldı.");
                }
            };

            Grid.SetColumn(actionBtn, 2);
            grid.Children.Add(actionBtn);

            card.Child = grid;
            SearchResultsHost.Children.Add(card);
        }
    }

    static double Bounce(double t)
    {
        const double n = 7.5625, d = 2.75;
        if (t < 1 / d) return n * t * t;
        if (t < 2 / d) { t -= 1.5 / d; return n * t * t + 0.75; }
        if (t < 2.5 / d) { t -= 2.25 / d; return n * t * t + 0.9375; }
        t -= 2.625 / d;
        return n * t * t + 0.984375;
    }

    void OnRenderFrame(object? sender, EventArgs e)
    {
        double t = _clock.Elapsed.TotalSeconds;

        // Smooth camera parallax + subtle breathing sway (matching customer-3d.html line 160)
        _cx += (_mx * 0.9 - _cx) * 0.04;
        _cy += (_my * 0.3 - _cy) * 0.04;
        var camPos = new Point3D(_cx + Math.Sin(t * 0.3) * 0.08, 2.05 - _cy, 6.5);
        var lookTarget = new Point3D(_cx * 0.2, 1.7, 0);
        Cam.Position = camPos;
        Cam.LookDirection = lookTarget - camPos;

        // Animate active books dropping & settling
        foreach (var entry in _activeBooks.Values)
        {
            double p = (t - entry.BornTime) / 0.95;
            if (p < 1.0)
            {
                double bVal = Bounce(Math.Max(p, 0));
                entry.Translate.OffsetY = entry.TargetY + (1.0 - bVal) * 4.5;
                entry.RotY.Angle += (entry.TargetRY - entry.RotY.Angle) * 0.08;
                entry.RotZ.Angle *= 0.93;
            }
            else
            {
                entry.Translate.OffsetY += (entry.TargetY - entry.Translate.OffsetY) * 0.15;
                entry.RotY.Angle = entry.TargetRY;
                entry.RotZ.Angle = 0;
            }
        }

        // Animate removed books sliding & scaling out
        for (int i = _dyingBooks.Count - 1; i >= 0; i--)
        {
            var d = _dyingBooks[i];
            double p = (t - d.OutTime) / 0.45;
            d.Translate.OffsetX += 0.09;
            d.RotZ.Angle += 1.7;
            double sc = Math.Max(0.01, 1.0 - p * 0.6);
            d.Scale.ScaleX = sc;
            d.Scale.ScaleY = sc;
            d.Scale.ScaleZ = sc;
            if (p > 1.0)
            {
                BooksRoot.Children.Remove(d.Visual);
                _dyingBooks.RemoveAt(i);
            }
        }

        // Completion flash decay
        if (_flashIntensity > 0.01)
        {
            _flashIntensity *= 0.96;
            byte g = (byte)Math.Clamp(0xBF * Math.Min(1.0, _flashIntensity), 0, 255);
            byte r = (byte)Math.Clamp(0x6F * Math.Min(1.0, _flashIntensity), 0, 255);
            byte b = (byte)Math.Clamp(0x7F * Math.Min(1.0, _flashIntensity), 0, 255);
            _flashLight.Color = Color.FromRgb(r, g, b);
        }
        else
        {
            _flashLight.Color = Colors.Black;
        }
    }

    void RefreshUIAndBooks()
    {
        var txt = _s.GetTexts();
        ModeBtn.Content = "Mod: " + (_s.Mode == TransactionMode.Add ? "Kitap Ekleme" : "Ödünç Alma");
        PanelTitle.Text = "▣ " + txt.PanelTitle;
        PanelId.Text = txt.PanelId;
        DisplayTitle.Text = txt.Title;
        Sub.Text = txt.Subtitle;

        int activeIdx = _s.Stage switch { Stage.Idle => 0, Stage.Preparing => 1, Stage.Waiting => 2, _ => 3 };
        StepsHost.Children.Clear();
        for (int i = 0; i < txt.Steps.Length; i++)
        {
            bool done = i < activeIdx || (i == 3 && activeIdx == 3);
            bool cur = i == activeIdx && !done;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
            var dot = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = Ui.B(done ? "#6FBF7F" : cur ? "#FFFFFF" : "#47FFFFFF"),
                BorderBrush = Ui.B(cur ? "#6FBF7F" : "Transparent"),
                BorderThickness = new Thickness(cur ? 3 : 0),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var lbl = Ui.Text(txt.Steps[i], 14.5, null, done || cur ? "#FFFFFF" : "#9C9384");
            row.Children.Add(dot);
            row.Children.Add(lbl);
            StepsHost.Children.Add(row);
        }

        ListTitle.Text = $"{txt.ListHeader} ({_s.Books.Count})";
        BookListHost.Children.Clear();
        foreach (var b in _s.Books)
        {
            var itemBorder = new Border
            {
                BorderBrush = Ui.B("#1AFFFFFF"),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 8, 0, 8)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var swatch = Ui.MiniCover(b, 30, 22);
            swatch.Margin = new Thickness(0, 0, 14, 0);
            row.Children.Add(swatch);
            row.Children.Add(Ui.Text(b.Title, 13.5));
            itemBorder.Child = row;
            BookListHost.Children.Add(itemBorder);
        }

        // Sync 3D Book Stack
        double now = _clock.Elapsed.TotalSeconds;
        var currentList = _s.Books.ToList();

        foreach (var gone in _activeBooks.Keys.Where(k => !currentList.Contains(k)).ToList())
        {
            var entry = _activeBooks[gone];
            _activeBooks.Remove(gone);
            entry.OutTime = now;
            _dyingBooks.Add(entry);
        }

        double cum = 0.03;
        for (int i = 0; i < currentList.Count; i++)
        {
            var b = currentList[i];
            double ty = cum + b.Thickness3D / 2.0;
            double ryDeg = ((i % 3) - 1) * 2.2;

            if (!_activeBooks.TryGetValue(b, out var entry))
            {
                double startX = ((i * 37) % 5 - 2) * 0.04;
                double startY = ty + 4.5;
                double startRY = i % 2 == 1 ? 28.0 : -28.0;
                double startRZ = i % 2 == 1 ? 11.5 : -11.5;

                var rotY = new AxisAngleRotation3D(new Vector3D(0, 1, 0), startRY);
                var rotZ = new AxisAngleRotation3D(new Vector3D(0, 0, 1), startRZ);
                var scale = new ScaleTransform3D(1, 1, 1);
                var trans = new TranslateTransform3D(startX, startY, 0.5);

                var tg = new Transform3DGroup();
                tg.Children.Add(scale);
                tg.Children.Add(new RotateTransform3D(rotZ));
                tg.Children.Add(new RotateTransform3D(rotY));
                tg.Children.Add(trans);

                var visual = MakeBook3D(b, tg);
                BooksRoot.Children.Add(visual);

                entry = new Book3DEntry
                {
                    Book = b,
                    Visual = visual,
                    Translate = trans,
                    RotY = rotY,
                    RotZ = rotZ,
                    Scale = scale,
                    BornTime = now,
                    TargetY = ty,
                    TargetRY = ryDeg
                };
                _activeBooks[b] = entry;
            }
            else
            {
                entry.TargetY = ty;
                entry.TargetRY = ryDeg;
            }
            cum += b.Thickness3D;
        }

        if (_s.Stage == Stage.Completed && _lastStage != Stage.Completed)
        {
            _flashIntensity = 2.8;
        }
        _lastStage = _s.Stage;
    }

    // =========================================================================
    // 3D SCENE CONSTRUCTION (Three.js scene translated to native WPF Viewport3D)
    // =========================================================================
    void Build3DScene()
    {
        // Lights
        _sceneGroup.Children.Add(new AmbientLight(Color.FromRgb(0x6A, 0x50, 0x38)));
        _sceneGroup.Children.Add(new DirectionalLight(Color.FromRgb(0xFF, 0xC4, 0x84), new Vector3D(-5, -6, 4.5)));
        _sceneGroup.Children.Add(new DirectionalLight(Color.FromRgb(0x55, 0x3A, 0x24), new Vector3D(3, -2, -5)));
        _flashLight.Color = Colors.Black;
        _sceneGroup.Children.Add(_flashLight);

        var darkWoodMat = SolidMat("#3A2212");
        var goldMat = EmissiveMat("#FFC860", "#DDA030");
        var brassMat = SpecularMat("#B9803F", "#FFE0A0", 40);

        // 1. Back Wall (wood grain texture)
        var wallBrush = CreateWoodBrush("#4A2C18", 8, 2);
        AddPlane(40, 14, new MaterialGroup { Children = { new DiffuseMaterial(wallBrush) } }, 0, 3, -4.3);

        // 2. Center Arched Alcove + Golden Arch + Antique Celestial Clock & Grimoire Design
        AddPlane(2.8, 3.8, SolidMat("#5A361D", "#221106"), 0, 1.95, -4.2);
        AddArchRing(1.48, 0.045, goldMat, 0, 2.75, -4.15);
        AddPlane(2.7, 3.4, new DiffuseMaterial(CreateAlcoveArtBrush()), 0, 2.05, -4.12);

        // 3. Right Arched Sunset Window + Frame
        AddPlane(2.4, 4.2, MetadataMaterial(CreateWindowBrush(), true), 3.0, 2.5, -4.2);
        AddArchRing(1.2, 0.085, darkWoodMat, 3.0, 3.4, -4.15);
        AddBox(0.16, 2.9, 0.16, darkWoodMat, 1.8, 1.95, -4.15);
        AddBox(0.16, 2.9, 0.16, darkWoodMat, 4.2, 1.95, -4.15);
        AddBox(2.7, 0.14, 0.35, darkWoodMat, 3.0, 0.45, -4.05);

        // 4. Left 6-Tier Bookshelf + ~150 Books + Library Ladder
        double sx = -3.3, sz = -3.95;
        AddBox(0.12, 4.8, 0.55, darkWoodMat, sx - 1.1, 2.4, sz);
        AddBox(0.12, 4.8, 0.55, darkWoodMat, sx + 1.1, 2.4, sz);
        AddBox(2.3, 4.8, 0.05, SolidMat("#1B0F07"), sx, 2.4, sz - 0.25);

        string[] palette = { "#5B3A29", "#7A4B2A", "#3E4F3A", "#2F3E55", "#6E2C2C", "#8C6A3C", "#4A3A52", "#9A8A5A" };
        var palMats = palette.Select(p => SolidMat(p)).ToArray();
        var rng = new Random(42);
        int bookCount = 0;
        for (int r = 0; r < 6; r++)
        {
            double y = 0.35 + r * 0.82;
            AddBox(2.2, 0.06, 0.55, darkWoodMat, sx, y, sz);
            double x = sx - 1.0;
            while (x < sx + 0.95 && bookCount < 160)
            {
                double bw = 0.07 + rng.NextDouble() * 0.08;
                double bh = 0.50 + rng.NextDouble() * 0.20;
                AddBox(bw, bh, 0.38, palMats[bookCount % 8], x + bw / 2, y + 0.03 + bh / 2, sz);
                bookCount++;
                x += bw + 0.006;
            }
        }
        // Ladder
        for (int i = 0; i < 9; i++)
            AddBox(0.34, 0.04, 0.04, darkWoodMat, sx + 1.5, 0.5 + i * 0.5, sz + 0.35);
        AddBox(0.05, 4.8, 0.05, darkWoodMat, sx + 1.33, 2.5, sz + 0.35);
        AddBox(0.05, 4.8, 0.05, darkWoodMat, sx + 1.67, 2.5, sz + 0.35);

        // 5. Counter + Vertical Slats + Leather Mat
        var counterTopMat = new MaterialGroup
        {
            Children =
            {
                new DiffuseMaterial(CreateWoodBrush("#9A6234", 3, 1)),
                new SpecularMaterial(Ui.B("#44FFE0B0"), 28)
            }
        };
        AddBox(16, 0.3, 3.6, counterTopMat, 0, -0.15, 0.3);
        AddBox(16, 1.7, 0.25, new DiffuseMaterial(CreateWoodBrush("#5A3318", 4, 1)), 0, -1.1, 2.0);
        var slatMat = SolidMat("#3A2010");
        for (int i = -7; i <= 7; i++)
            AddBox(0.08, 1.3, 0.06, slatMat, i * 1.05, -1.1, 2.15);
        AddBox(16, 0.12, 0.34, SpecularMat("#C48A52", "#FFE0B0", 25), 0, -0.3, 2.1);
        // Dark green leather mat
        AddBox(4.6, 0.03, 2.5, SolidMat("#1F3828"), 0, 0.015, 0.5);

        // 6. Hanging Lamps (3x)
        (double x, double y, double z)[] lamps = { (-3.2, 4.3, -1.6), (0.9, 3.9, -2.1), (4.4, 4.4, -1.8) };
        var cordMat = SolidMat("#222222");
        var shadeMat = SpecularMat("#2B2118", "#886644", 30);
        var bulbMat = EmissiveMat("#FFE0A8", "#FFD080");
        var glowBrush = CreateGlowBrush();
        foreach (var (lx, ly, lz) in lamps)
        {
            AddBox(0.02, 8, 0.02, cordMat, lx, ly + 4.1, lz);
            AddConeFrustum(0.10, 0.42, 0.38, shadeMat, lx, ly, lz);
            AddSphere(0.11, bulbMat, lx, ly - 0.12, lz);
            AddPlane(1.7, 1.7, new EmissiveMaterial(glowBrush), lx, ly - 0.25, lz + 0.05);
            _sceneGroup.Children.Add(new PointLight(Color.FromRgb(0xFF, 0xB8, 0x68), new Point3D(lx, ly - 0.4, lz))
            {
                Range = 9,
                ConstantAttenuation = 1.0,
                LinearAttenuation = 0.18
            });
        }

        // 7. Banker's Desk Lamp, Pen Holder & Service Bell
        double dlx = -3.2, dlz = 0.7;
        AddCylinder(0.36, 0.07, brassMat, dlx, 0.035, dlz);
        AddCylinder(0.04, 1.3, brassMat, dlx + 0.1, 0.7, dlz);
        AddBox(0.75, 0.06, 0.06, brassMat, dlx + 0.38, 1.32, dlz);
        // Green dome shade
        AddDome(0.56, SpecularMat("#1F6A3F", "#6FBF7F", 35), dlx + 0.70, 1.38, dlz);
        AddSphere(0.11, EmissiveMat("#FFF0C0", "#FFE090"), dlx + 0.70, 1.34, dlz);
        AddPlane(2.1, 2.1, new EmissiveMaterial(glowBrush), dlx + 0.70, 1.25, dlz + 0.15);
        _sceneGroup.Children.Add(new PointLight(Color.FromRgb(0xFF, 0xC4, 0x74), new Point3D(dlx + 0.7, 1.25, dlz + 0.2))
        {
            Range = 7,
            ConstantAttenuation = 1.0,
            LinearAttenuation = 0.22
        });

        // Pen holder & 3 pens
        AddBox(0.36, 0.08, 0.36, SolidMat("#1E2A20"), -2.55, 0.04, 1.3);
        AddCylinder(0.16, 0.42, SpecularMat("#2D2D2D", "#666666", 20), -2.55, 0.21, 1.3);
        (double a, double b, string c)[] pens = { (-0.05, 0.2, "#C9A25E"), (0.05, -0.15, "#8FC9A0"), (0.10, 0.10, "#222222") };
        foreach (var (pa, pb, pc) in pens)
            AddCylinder(0.015, 0.55, SolidMat(pc), -2.55 + pa, 0.56, 1.3 + pb * 0.3);

        // Service bell on the right
        AddCylinder(0.35, 0.06, brassMat, 2.2, 0.03, 0.9);
        AddDome(0.28, SpecularMat("#D9A93F", "#FFF0B0", 55), 2.2, 0.06, 0.9);
        AddBox(0.10, 0.05, 0.10, brassMat, 2.2, 0.35, 0.9);

        // 8. Climbing Vines (Leaves on left foreground)
        var leafMat1 = SolidMat("#2E5E2A");
        var leafMat2 = SolidMat("#3E7A38");
        Point3D[][] vinePaths =
        {
            new[] { new Point3D(-3.8, 4.6, 1.1), new Point3D(-3.4, 3.6, 1.2), new Point3D(-3.9, 2.7, 1.0), new Point3D(-3.5, 1.8, 1.3) },
            new[] { new Point3D(-3.2, 4.5, 0.4), new Point3D(-3.0, 3.9, 0.6), new Point3D(-3.25, 3.3, 0.5) },
            new[] { new Point3D(-3.7, 0.1, 1.4), new Point3D(-3.4, 0.9, 1.3), new Point3D(-3.8, 1.6, 1.1) }
        };
        foreach (var pts in vinePaths)
        {
            for (int i = 0; i < 45; i++)
            {
                double u = i / 44.0;
                var p = LerpPath(pts, u);
                double lx = p.X + (rng.NextDouble() - 0.5) * 0.45;
                double ly = p.Y + (rng.NextDouble() - 0.5) * 0.22;
                double lz = p.Z + (rng.NextDouble() - 0.5) * 0.35;
                double s = 0.12 + rng.NextDouble() * 0.10;
                AddBox(s * 1.4, s * 0.8, 0.02, i % 2 == 0 ? leafMat1 : leafMat2, lx, ly, lz);
            }
        }
    }

    static Point3D LerpPath(Point3D[] pts, double t)
    {
        double seg = t * (pts.Length - 1);
        int idx = Math.Clamp((int)seg, 0, pts.Length - 2);
        double f = seg - idx;
        var a = pts[idx];
        var b = pts[idx + 1];
        return new Point3D(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f);
    }

    // =========================================================================
    // PROCEDURAL 3D BOOK BUILDER (matching mkBook in customer-3d.html)
    // =========================================================================
    ModelVisual3D MakeBook3D(BookItem b, Transform3D transform)
    {
        double Wd = b.Width3D, Tk = b.Thickness3D, D = b.Depth3D, bd = 0.035;
        var group = new Model3DGroup { Transform = transform };

        var coverMat = SolidMat(b.Cover);
        var spineMat = new MaterialGroup
        {
            Children =
            {
                new DiffuseMaterial(CreateSpineBrush(b)),
                new SpecularMaterial(Ui.B("#33FFFFFF"), 24)
            }
        };
        var topCoverMat = new MaterialGroup
        {
            Children =
            {
                new DiffuseMaterial(CreateTopCoverBrush(b)),
                new SpecularMaterial(Ui.B("#33FFFFFF"), 24)
            }
        };
        var pagesMat = new DiffuseMaterial(CreatePagesBrush());

        // Spine box at z = D / 2 (front-facing edge on the counter)
        AddBoxToGroup(group, Wd, Tk, 0.05, coverMat, frontOverride: spineMat, topOverride: null, 0, 0, D / 2);

        // Top cover board (y = Tk/2 - bd/2) with decorated top face
        AddBoxToGroup(group, Wd, bd, D, coverMat, frontOverride: null, topOverride: topCoverMat, 0, Tk / 2 - bd / 2, 0);

        // Bottom cover board (y = -Tk/2 + bd/2)
        AddBoxToGroup(group, Wd, bd, D, coverMat, frontOverride: null, topOverride: null, 0, -Tk / 2 + bd / 2, 0);

        // Page block inside boards
        AddBoxToGroup(group, Wd - 0.10, Tk - 2 * bd, D - 0.08, pagesMat, frontOverride: null, topOverride: null, 0, 0, -0.01);

        return new ModelVisual3D { Content = group };
    }

    // =========================================================================
    // PROCEDURAL TEXTURE BRUSHES (Canvas 2D equivalents in WPF)
    // =========================================================================
    static ImageBrush RenderToBrush(int w, int h, Action<DrawingContext, int, int> draw)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            draw(dc, w, h);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        var ib = new ImageBrush(rtb) { ViewportUnits = BrushMappingMode.Absolute };
        ib.Freeze();
        return ib;
    }

    static ImageBrush CreateWoodBrush(string baseHex, int repX, int repY)
    {
        var rng = new Random(baseHex.GetHashCode());
        var ib = RenderToBrush(256, 256, (dc, w, h) =>
        {
            dc.DrawRectangle(Ui.B(baseHex), null, new Rect(0, 0, w, h));
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(42, 30, 14, 4)), 1.4);
            for (int i = 0; i < 90; i++)
            {
                double y = rng.NextDouble() * h;
                dc.DrawLine(pen, new Point(0, y), new Point(w, y + (rng.NextDouble() - 0.5) * 10));
            }
        });
        var tiled = ib.CloneCurrentValue();
        tiled.TileMode = TileMode.Tile;
        tiled.Viewport = new Rect(0, 0, 1.0 / repX, 1.0 / repY);
        tiled.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
        tiled.Freeze();
        return tiled;
    }

    static ImageBrush CreateAlcoveArtBrush() => RenderToBrush(800, 1000, (dc, w, h) =>
    {
        // 1. Arched background clipping
        var archClip = Geometry.Parse("M0,1000 L0,400 A400,400 0 0 1 800,400 L800,1000 Z");
        dc.PushClip(archClip);

        // Rich dark wood backing with warm radial amber glow
        var bgGradient = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.38),
            GradientOrigin = new Point(0.5, 0.38),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        bgGradient.GradientStops.Add(new GradientStop(Ui.C("#6E3A1A"), 0.0));
        bgGradient.GradientStops.Add(new GradientStop(Ui.C("#38190B"), 0.55));
        bgGradient.GradientStops.Add(new GradientStop(Ui.C("#1A0B05"), 1.0));
        dc.DrawRectangle(bgGradient, null, new Rect(0, 0, w, h));

        // Vitrage arch border rings
        var goldPen = new Pen(Ui.B("#DDA030"), 4);
        var thinGoldPen = new Pen(Ui.B("#C59235"), 1.5);
        var faintPen = new Pen(Ui.B("#55E4B368"), 1);

        // Arched inner tracery lines
        for (int r = 370; r >= 340; r -= 15)
        {
            var rArch = Geometry.Parse($"M{400 - r},1000 L{400 - r},400 A{r},{r} 0 0 1 {400 + r},400 L{400 + r},1000");
            dc.DrawGeometry(null, (r == 370 ? goldPen : thinGoldPen), rArch);
        }

        // Stained glass radiant rays in the arch lunette
        double cx = 400, cy = 380;
        for (int a = -160; a <= -20; a += 14)
        {
            double rad = a * Math.PI / 180.0;
            double x1 = cx + Math.Cos(rad) * 220;
            double y1 = cy + Math.Sin(rad) * 220;
            double x2 = cx + Math.Cos(rad) * 350;
            double y2 = cy + Math.Sin(rad) * 350;
            dc.DrawLine(faintPen, new Point(x1, y1), new Point(x2, y2));
        }

        // 2. Centerpiece: Antique Grand Library Clock
        double clockRadius = 175;
        var clockBg = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.48, 0.45),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        clockBg.GradientStops.Add(new GradientStop(Ui.C("#FFF8E7"), 0.0));
        clockBg.GradientStops.Add(new GradientStop(Ui.C("#F2DEB0"), 0.65));
        clockBg.GradientStops.Add(new GradientStop(Ui.C("#C9A662"), 0.92));
        clockBg.GradientStops.Add(new GradientStop(Ui.C("#5B3D1B"), 1.0));

        // Outer clock brass bezel
        dc.DrawEllipse(new SolidColorBrush(Ui.C("#2A1408")), new Pen(Ui.B("#E5B548"), 10), new Point(cx, cy), clockRadius + 14, clockRadius + 14);
        dc.DrawEllipse(null, new Pen(Ui.B("#FFE699"), 3), new Point(cx, cy), clockRadius + 6, clockRadius + 6);
        dc.DrawEllipse(clockBg, new Pen(Ui.B("#7A4D1A"), 3), new Point(cx, cy), clockRadius, clockRadius);

        // Minute and hour tick marks
        for (int m = 0; m < 60; m++)
        {
            double angle = m * 6.0 * Math.PI / 180.0;
            bool isHour = m % 5 == 0;
            double len = isHour ? 14 : 7;
            double r1 = clockRadius - 8;
            double r2 = r1 - len;
            var p1 = new Point(cx + Math.Sin(angle) * r1, cy - Math.Cos(angle) * r1);
            var p2 = new Point(cx + Math.Sin(angle) * r2, cy - Math.Cos(angle) * r2);
            dc.DrawLine(new Pen(Ui.B(isHour ? "#3D220D" : "#7B552C"), isHour ? 3 : 1.2), p1, p2);
        }

        // Inner decorative guilloche ring
        dc.DrawEllipse(null, new Pen(Ui.B("#C99A48"), 1.5), new Point(cx, cy), clockRadius - 52, clockRadius - 52);
        dc.DrawEllipse(null, new Pen(Ui.B("#D9B06A"), 1), new Point(cx, cy), clockRadius - 56, clockRadius - 56);

        // Roman Numerals (I through XII)
        string[] romans = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
        var romanTypeface = new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        for (int i = 0; i < 12; i++)
        {
            double angle = i * 30.0 * Math.PI / 180.0;
            double numDist = clockRadius - 32;
            var numText = new FormattedText(romans[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, romanTypeface, 23, Ui.B("#2A1508"), 1.0);
            double nx = cx + Math.Sin(angle) * numDist - numText.Width / 2;
            double ny = cy - Math.Cos(angle) * numDist - numText.Height / 2;
            dc.DrawText(numText, new Point(nx, ny));
        }

        // Clock Center Filigree (Sunburst / Compass star)
        for (int s = 0; s < 8; s++)
        {
            double sAngle = s * 45.0 * Math.PI / 180.0;
            var starTip = new Point(cx + Math.Sin(sAngle) * 44, cy - Math.Cos(sAngle) * 44);
            var leftTip = new Point(cx + Math.Sin(sAngle - 0.2) * 16, cy - Math.Cos(sAngle - 0.2) * 16);
            var rightTip = new Point(cx + Math.Sin(sAngle + 0.2) * 16, cy - Math.Cos(sAngle + 0.2) * 16);
            var starPath = new PathGeometry();
            var fig = new PathFigure { StartPoint = starTip, IsClosed = true, IsFilled = true };
            fig.Segments.Add(new LineSegment(leftTip, true));
            fig.Segments.Add(new LineSegment(new Point(cx, cy), true));
            fig.Segments.Add(new LineSegment(rightTip, true));
            starPath.Figures.Add(fig);
            dc.DrawGeometry(s % 2 == 0 ? Ui.B("#C99A48") : Ui.B("#A5782C"), null, starPath);
        }

        // Baroque Clock Hands set at 10:10 (Hour hand at 305°, Minute hand at 60°)
        double hourAngle = 305.0 * Math.PI / 180.0;
        var hPen = new Pen(Ui.B("#221005"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var hTip = new Point(cx + Math.Sin(hourAngle) * 85, cy - Math.Cos(hourAngle) * 85);
        dc.DrawLine(hPen, new Point(cx, cy), hTip);
        var hRing = new Point(cx + Math.Sin(hourAngle) * 60, cy - Math.Cos(hourAngle) * 60);
        dc.DrawEllipse(Ui.B("#221005"), null, hRing, 8, 8);
        dc.DrawEllipse(Ui.B("#F2DEB0"), null, hRing, 3.5, 3.5);

        double minAngle = 60.0 * Math.PI / 180.0;
        var mPen = new Pen(Ui.B("#180A03"), 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var mTip = new Point(cx + Math.Sin(minAngle) * 115, cy - Math.Cos(minAngle) * 115);
        dc.DrawLine(mPen, new Point(cx, cy), mTip);
        var mRing = new Point(cx + Math.Sin(minAngle) * 85, cy - Math.Cos(minAngle) * 85);
        dc.DrawEllipse(Ui.B("#180A03"), null, mRing, 7, 7);
        dc.DrawEllipse(Ui.B("#F2DEB0"), null, mRing, 3, 3);

        // Center brass cap
        dc.DrawEllipse(Ui.B("#E5B548"), new Pen(Ui.B("#221005"), 2.5), new Point(cx, cy), 12, 12);
        dc.DrawEllipse(Ui.B("#FFF2B8"), null, new Point(cx - 2, cy - 2), 4, 4);

        // 3. Lower Emblem: Sculpted Golden Open Grimoire & Laurel Wreath
        double bookY = 670;
        var laurelPen = new Pen(Ui.B("#DDA030"), 2);
        for (int side = -1; side <= 1; side += 2)
        {
            var branch = new PathGeometry();
            var bFig = new PathFigure { StartPoint = new Point(cx + side * 30, bookY + 80) };
            bFig.Segments.Add(new BezierSegment(
                new Point(cx + side * 150, bookY + 60),
                new Point(cx + side * 180, bookY - 30),
                new Point(cx + side * 120, bookY - 90), true));
            branch.Figures.Add(bFig);
            dc.DrawGeometry(null, laurelPen, branch);

            for (double t = 0.2; t <= 0.9; t += 0.14)
            {
                double lx = cx + side * (50 + t * 110);
                double ly = bookY + 70 - t * 140;
                dc.DrawEllipse(Ui.B("#E5B548"), null, new Point(lx, ly), 9, 4);
                dc.DrawEllipse(Ui.B("#FFDF78"), null, new Point(lx + side * 4, ly - 3), 7, 3);
            }
        }

        // Open Book (intricately curved leaves)
        var bookPen = new Pen(Ui.B("#FFDF88"), 4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        var bookFill = new LinearGradientBrush(Ui.C("#FFF8E7"), Ui.C("#DBC392"), 90);
        var bookGeo = Geometry.Parse($"M{cx},{bookY - 20} C{cx - 70},{bookY - 50} {cx - 140},{bookY - 45} {cx - 190},{bookY - 15} L{cx - 190},{bookY + 55} C{cx - 140},{bookY + 25} {cx - 70},{bookY + 25} {cx},{bookY + 55} C{cx + 70},{bookY + 25} {cx + 140},{bookY + 25} {cx + 190},{bookY + 55} L{cx + 190},{bookY - 15} C{cx + 140},{bookY - 45} {cx + 70},{bookY - 50} {cx},{bookY - 20} Z");
        dc.DrawGeometry(bookFill, bookPen, bookGeo);
        dc.DrawLine(new Pen(Ui.B("#7B4E1B"), 3), new Point(cx, bookY - 18), new Point(cx, bookY + 55));

        // Calligraphy lines
        var pageLinePen = new Pen(Ui.B("#9B7A50"), 1.8);
        for (int l = 0; l < 4; l++)
        {
            double py = bookY - 5 + l * 12;
            dc.DrawLine(pageLinePen, new Point(cx - 165, py + 5), new Point(cx - 35, py));
            dc.DrawLine(pageLinePen, new Point(cx + 35, py), new Point(cx + 165, py + 5));
        }

        // Radiating starburst atop the open book
        dc.DrawEllipse(Ui.B("#FFE89E"), null, new Point(cx, bookY - 32), 6, 6);
        dc.DrawLine(new Pen(Ui.B("#FFE89E"), 2), new Point(cx, bookY - 44), new Point(cx, bookY - 20));
        dc.DrawLine(new Pen(Ui.B("#FFE89E"), 2), new Point(cx - 12, bookY - 32), new Point(cx + 12, bookY - 32));

        // 4. Elegant bottom pedestal / scrollwork
        var pedestalGeo = Geometry.Parse($"M{cx - 140},{bookY + 95} Q{cx},{bookY + 115} {cx + 140},{bookY + 95} L{cx + 110},{bookY + 115} Q{cx},{bookY + 130} {cx - 110},{bookY + 115} Z");
        dc.DrawGeometry(Ui.B("#C99A48"), new Pen(Ui.B("#FFE082"), 1.5), pedestalGeo);

        dc.Pop();
    });

    static ImageBrush CreateWindowBrush() => RenderToBrush(480, 840, (dc, w, h) =>
    {
        var clip = Geometry.Parse("M0,840 L0,240 A240,240 0 0 1 480,240 L480,840 Z");
        dc.PushClip(clip);
        var sky = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        sky.GradientStops.Add(new GradientStop(Ui.C("#3F5F96"), 0.0));
        sky.GradientStops.Add(new GradientStop(Ui.C("#D98F78"), 0.55));
        sky.GradientStops.Add(new GradientStop(Ui.C("#F6B774"), 0.80));
        sky.GradientStops.Add(new GradientStop(Ui.C("#F8D08F"), 1.0));
        dc.DrawRectangle(sky, null, new Rect(0, 0, w, h));

        var rng = new Random(19);
        for (int i = 0; i < 42; i++)
        {
            var treeBrush = new SolidColorBrush(Color.FromArgb(190, (byte)(20 + rng.Next(30)), (byte)(40 + rng.Next(40)), 25));
            double r = 30 + rng.NextDouble() * 50;
            dc.DrawEllipse(treeBrush, null, new Point(rng.NextDouble() * w, 520 + rng.NextDouble() * 320), r, r);
        }

        var mullionPen = new Pen(Ui.B("#1D130B"), 9);
        dc.DrawLine(mullionPen, new Point(240, 0), new Point(240, h));
        dc.DrawLine(mullionPen, new Point(0, 330), new Point(w, 330));
        dc.DrawLine(mullionPen, new Point(0, 620), new Point(w, 620));
        dc.Pop();
    });

    static RadialGradientBrush CreateGlowBrush()
    {
        var b = new RadialGradientBrush();
        b.GradientStops.Add(new GradientStop(Color.FromArgb(190, 255, 210, 140), 0));
        b.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 170, 70), 1));
        b.Freeze();
        return b;
    }

    static ImageBrush CreateSpineBrush(BookItem b) => RenderToBrush(1024, 128, (dc, w, h) =>
    {
        var baseCol = Ui.C(b.Cover);
        var grad = new LinearGradientBrush(Lighten(baseCol, 0.22), Darken(baseCol, 0.32), 90);
        dc.DrawRectangle(grad, null, new Rect(0, 0, w, h));

        var gold = Ui.B(b.Accent);
        var bandPen = new Pen(gold, 6);
        foreach (double x in new[] { 50.0, 78.0, w - 78.0, w - 50.0 })
            dc.DrawLine(bandPen, new Point(x, 6), new Point(x, h - 6));

        dc.DrawRectangle(null, new Pen(gold, 3), new Rect(100, 14, w - 200, h - 28));

        var tf = new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var ft = new FormattedText(b.Title, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, tf, 56, gold, 1.0)
        {
            MaxTextWidth = w - 260,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2));
    });

    static ImageBrush CreateTopCoverBrush(BookItem b) => RenderToBrush(512, 360, (dc, w, h) =>
    {
        dc.DrawRectangle(Ui.B(b.Cover), null, new Rect(0, 0, w, h));
        var gold = Ui.B(b.Accent);
        dc.DrawRectangle(null, new Pen(gold, 4), new Rect(24, 24, w - 48, h - 48));

        var tf = new Typeface(new FontFamily("Georgia"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var ft = new FormattedText(b.Title, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight, tf, 34, gold, 1.0)
        {
            MaxTextWidth = w - 90,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(ft, new Point((w - ft.Width) / 2, (h - ft.Height) / 2 - 10));

        var fa = new FormattedText(b.Author.ToUpper(CultureInfo.GetCultureInfo("tr-TR")), CultureInfo.GetCultureInfo("tr-TR"),
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, gold, 1.0)
        {
            MaxTextWidth = w - 90,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };
        dc.DrawText(fa, new Point((w - fa.Width) / 2, (h - fa.Height) / 2 + 28));
    });

    static ImageBrush CreatePagesBrush() => RenderToBrush(128, 64, (dc, w, h) =>
    {
        dc.DrawRectangle(Ui.B("#EFE4C6"), null, new Rect(0, 0, w, h));
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 120, 95, 50)), 1);
        for (int y = 0; y < h; y += 3)
            dc.DrawLine(pen, new Point(0, y), new Point(w, y));
    });

    static Color Lighten(Color c, double f) => Color.FromRgb(
        (byte)Math.Clamp(c.R + (255 - c.R) * f, 0, 255),
        (byte)Math.Clamp(c.G + (255 - c.G) * f, 0, 255),
        (byte)Math.Clamp(c.B + (255 - c.B) * f, 0, 255));

    static Color Darken(Color c, double f) => Color.FromRgb(
        (byte)Math.Clamp(c.R * (1 - f), 0, 255),
        (byte)Math.Clamp(c.G * (1 - f), 0, 255),
        (byte)Math.Clamp(c.B * (1 - f), 0, 255));

    // =========================================================================
    // 3D MESH PRIMITIVE HELPERS
    // =========================================================================
    static Material SolidMat(string hex, string? emissiveHex = null)
    {
        if (emissiveHex == null) return new DiffuseMaterial(Ui.B(hex));
        var mg = new MaterialGroup();
        mg.Children.Add(new DiffuseMaterial(Ui.B(hex)));
        mg.Children.Add(new EmissiveMaterial(Ui.B(emissiveHex)));
        return mg;
    }

    static Material EmissiveMat(string diffHex, string emHex)
    {
        var mg = new MaterialGroup();
        mg.Children.Add(new DiffuseMaterial(Ui.B(diffHex)));
        mg.Children.Add(new EmissiveMaterial(Ui.B(emHex)));
        return mg;
    }

    static Material SpecularMat(string diffHex, string specHex, double power)
    {
        var mg = new MaterialGroup();
        mg.Children.Add(new DiffuseMaterial(Ui.B(diffHex)));
        mg.Children.Add(new SpecularMaterial(Ui.B(specHex), power));
        return mg;
    }

    static Material MetadataMaterial(ImageBrush brush, bool emissive)
    {
        if (!emissive) return new DiffuseMaterial(brush);
        var mg = new MaterialGroup();
        mg.Children.Add(new DiffuseMaterial(brush));
        mg.Children.Add(new EmissiveMaterial(brush));
        return mg;
    }

    void AddPlane(double w, double h, Material mat, double cx, double cy, double cz)
    {
        double hw = w / 2, hh = h / 2;
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(cx - hw, cy - hh, cz),
                new(cx + hw, cy - hh, cz),
                new(cx + hw, cy + hh, cz),
                new(cx - hw, cy + hh, cz)
            },
            TextureCoordinates = new PointCollection { new(0, 1), new(1, 1), new(1, 0), new(0, 0) },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        mesh.Freeze();
        _sceneGroup.Children.Add(new GeometryModel3D(mesh, mat) { BackMaterial = mat });
    }

    void AddBox(double w, double h, double d, Material mat, double cx, double cy, double cz) =>
        AddBoxToGroup(_sceneGroup, w, h, d, mat, null, null, cx, cy, cz);

    static void AddBoxToGroup(Model3DGroup target, double w, double h, double d, Material baseMat, Material? frontOverride, Material? topOverride, double cx, double cy, double cz)
    {
        double hw = w / 2, hh = h / 2, hd = d / 2;

        // Front (+Z)
        target.Children.Add(MakeQuad(
            new(cx - hw, cy - hh, cz + hd), new(cx + hw, cy - hh, cz + hd),
            new(cx + hw, cy + hh, cz + hd), new(cx - hw, cy + hh, cz + hd),
            frontOverride ?? baseMat));
        // Back (-Z)
        target.Children.Add(MakeQuad(
            new(cx + hw, cy - hh, cz - hd), new(cx - hw, cy - hh, cz - hd),
            new(cx - hw, cy + hh, cz - hd), new(cx + hw, cy + hh, cz - hd),
            baseMat));
        // Top (+Y)
        target.Children.Add(MakeQuad(
            new(cx - hw, cy + hh, cz + hd), new(cx + hw, cy + hh, cz + hd),
            new(cx + hw, cy + hh, cz - hd), new(cx - hw, cy + hh, cz - hd),
            topOverride ?? baseMat));
        // Bottom (-Y)
        target.Children.Add(MakeQuad(
            new(cx - hw, cy - hh, cz - hd), new(cx + hw, cy - hh, cz - hd),
            new(cx + hw, cy - hh, cz + hd), new(cx - hw, cy - hh, cz + hd),
            baseMat));
        // Left (-X)
        target.Children.Add(MakeQuad(
            new(cx - hw, cy - hh, cz - hd), new(cx - hw, cy - hh, cz + hd),
            new(cx - hw, cy + hh, cz + hd), new(cx - hw, cy + hh, cz - hd),
            baseMat));
        // Right (+X)
        target.Children.Add(MakeQuad(
            new(cx + hw, cy - hh, cz + hd), new(cx + hw, cy - hh, cz - hd),
            new(cx + hw, cy + hh, cz - hd), new(cx + hw, cy + hh, cz + hd),
            baseMat));
    }

    static GeometryModel3D MakeQuad(Point3D p0, Point3D p1, Point3D p2, Point3D p3, Material mat)
    {
        var m = new MeshGeometry3D
        {
            Positions = new Point3DCollection { p0, p1, p2, p3 },
            TextureCoordinates = new PointCollection { new(0, 1), new(1, 1), new(1, 0), new(0, 0) },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        m.Freeze();
        return new GeometryModel3D(m, mat);
    }

    void AddCylinder(double r, double h, Material mat, double cx, double cy, double cz) =>
        AddConeFrustum(r, r, h, mat, cx, cy, cz);

    void AddConeFrustum(double rTop, double rBot, double h, Material mat, double cx, double cy, double cz, int segs = 20)
    {
        double hh = h / 2;
        var mesh = new MeshGeometry3D();
        for (int i = 0; i <= segs; i++)
        {
            double a = i * 2 * Math.PI / segs;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            mesh.Positions.Add(new Point3D(cx + rBot * cos, cy - hh, cz + rBot * sin));
            mesh.Positions.Add(new Point3D(cx + rTop * cos, cy + hh, cz + rTop * sin));
        }
        for (int i = 0; i < segs; i++)
        {
            int b0 = i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
            mesh.TriangleIndices.Add(b0); mesh.TriangleIndices.Add(b1); mesh.TriangleIndices.Add(t1);
            mesh.TriangleIndices.Add(b0); mesh.TriangleIndices.Add(t1); mesh.TriangleIndices.Add(t0);
        }
        mesh.Freeze();
        _sceneGroup.Children.Add(new GeometryModel3D(mesh, mat) { BackMaterial = mat });
    }

    void AddDome(double r, Material mat, double cx, double cy, double cz, int rings = 10, int segs = 20)
    {
        var mesh = new MeshGeometry3D();
        for (int lat = 0; lat <= rings; lat++)
        {
            double theta = lat * (Math.PI / 2) / rings;
            double sinT = Math.Sin(theta), cosT = Math.Cos(theta);
            for (int lon = 0; lon <= segs; lon++)
            {
                double phi = lon * 2 * Math.PI / segs;
                mesh.Positions.Add(new Point3D(cx + r * sinT * Math.Cos(phi), cy + r * cosT, cz + r * sinT * Math.Sin(phi)));
            }
        }
        for (int lat = 0; lat < rings; lat++)
        {
            for (int lon = 0; lon < segs; lon++)
            {
                int first = lat * (segs + 1) + lon;
                int second = first + segs + 1;
                mesh.TriangleIndices.Add(first); mesh.TriangleIndices.Add(second); mesh.TriangleIndices.Add(first + 1);
                mesh.TriangleIndices.Add(second); mesh.TriangleIndices.Add(second + 1); mesh.TriangleIndices.Add(first + 1);
            }
        }
        mesh.Freeze();
        _sceneGroup.Children.Add(new GeometryModel3D(mesh, mat) { BackMaterial = mat });
    }

    void AddSphere(double r, Material mat, double cx, double cy, double cz)
    {
        AddDome(r, mat, cx, cy, cz, 8, 16);
        AddDome(-r, mat, cx, cy, cz, 8, 16);
    }

    void AddArchRing(double radius, double thickness, Material mat, double cx, double cy, double cz, int segs = 28)
    {
        for (int i = 0; i < segs; i++)
        {
            double a0 = Math.PI * i / segs;
            double a1 = Math.PI * (i + 1) / segs;
            double am = (a0 + a1) / 2;
            double x = cx + Math.Cos(am) * radius;
            double y = cy + Math.Sin(am) * radius;
            double len = radius * Math.PI / segs * 1.15;
            AddBox(len, thickness, thickness, mat, x, y, cz);
        }
    }

    private Action? _modalConfirmAction;

    public void ShowModal(string title, string message, string confirmText, Action onConfirm)
    {
        ModalTitle.Text = title;
        ModalMessage.Text = message;
        ModalConfirmBtn.Content = confirmText;
        ModalConfirmBtn.Background = new LinearGradientBrush(Ui.C("#A34032"), Ui.C("#5B1B15"), 90);
        ModalConfirmBtn.BorderBrush = Ui.B("#F08A7A");
        _modalConfirmAction = onConfirm;
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

    private void OnLogoutCustomer(object sender, RoutedEventArgs e)
    {
        ShowModal(
            "Oturumu Kapat",
            "Kullanıcı oturumunuz sonlandırılacak ve giriş ekranına dönülecektir. Devam etmek istiyor musunuz?",
            "Giriş Ekranına Dön",
            () => App.ReturnToLogin(this));
    }

    private void OnExitCustomer(object sender, RoutedEventArgs e)
    {
        ShowModal(
            "Uygulamayı Kapat",
            "Kütüphane Otomasyon Sistemini tamamen kapatmak istediğinize emin misiniz?",
            "Evet, Kapat",
            () => System.Windows.Application.Current.Shutdown());
    }
}
