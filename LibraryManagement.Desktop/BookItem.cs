using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LibraryManagement.Application.Interfaces;
using LibraryManagement.Infrastructure;

namespace LibraryManagement.Desktop;

public sealed record BookItem(string Title, string Author, string Category, int Pages, string Cover, string Accent)
{
    public double Width => Category switch { "Çocuk" or "Şiir" => 270, "Akademik" => 300, _ => 330 };
    public double Thickness => Math.Clamp(26 + Pages / 18.0, 32, 66);   // front-band height (page count)
    public double Depth => Width * 0.27;                                  // visible top-face height (perspective)

    // 3D world unit dimensions (matching customer-3d.html)
    public double Width3D => Category == "Çocuk" ? 1.8 : 2.2;
    public double Thickness3D => Math.Clamp(0.26 + Pages / 5000.0, 0.30, 0.50);
    public double Depth3D => Width3D * 0.68;

    public static BookItem FromDomain(Domain.Entities.Book b) => new(
        b.Title,
        b.AuthorName,
        b.CategoryName,
        b.PageCount,
        string.IsNullOrWhiteSpace(b.PrimaryCoverHex) ? "#1D3550" : b.PrimaryCoverHex,
        string.IsNullOrWhiteSpace(b.AccentFoilHex) ? "#D9B76A" : b.AccentFoilHex);
}

public enum Stage { Idle, Preparing, Waiting, Completed }
public enum TransactionMode { Lend, Add }
public enum UserRole { Admin, User }

/// <summary>Shared application state backed by ILibraryDataService.</summary>
public sealed class CheckoutState
{
    public static CheckoutState Instance { get; } = new();

    private readonly ILibraryDataService _dataService;
    public ILibraryDataService DataService => _dataService;

    public ObservableCollection<BookItem> Books { get; } = new();
    public Stage Stage { get; private set; } = Stage.Idle;
    public TransactionMode Mode { get; private set; } = TransactionMode.Lend;
    public string LoggedInUser { get; set; } = "Yusuf";
    public UserRole LoggedInRole { get; set; } = UserRole.Admin;
    public event Action? Changed;

    private CheckoutState()
    {
        _dataService = new LibraryDataService();

        Books.CollectionChanged += (_, _) =>
        {
            if (Stage is Stage.Idle or Stage.Preparing)
                Stage = Books.Count == 0 ? Stage.Idle : Stage.Preparing;
            Changed?.Invoke();
        };
    }

    public IReadOnlyList<BookItem> GetCatalogSample()
    {
        var domainBooks = _dataService.GetBooks();
        return domainBooks.Select(BookItem.FromDomain).ToList();
    }

    public void SetMode(TransactionMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        Changed?.Invoke();
    }

    public void ToggleMode()
    {
        Mode = Mode == TransactionMode.Add ? TransactionMode.Lend : TransactionMode.Add;
        Changed?.Invoke();
    }

    public void Toggle(BookItem b)
    {
        if (Stage is Stage.Waiting or Stage.Completed) return;
        if (!Books.Remove(b)) Books.Add(b);
    }

    public void AddNext()
    {
        if (Stage is Stage.Waiting or Stage.Completed) return;
        var catalog = GetCatalogSample();
        var next = catalog.FirstOrDefault(b => !Books.Contains(b));
        if (next != null) Toggle(next);
    }

    public void RemoveLast()
    {
        if (Books.Count > 0 && Stage == Stage.Preparing) Books.RemoveAt(Books.Count - 1);
    }

    public async void Confirm()
    {
        if (Stage != Stage.Preparing || Books.Count == 0) return;
        Stage = Stage.Waiting; Changed?.Invoke();

        // Arka plan veritabanı servisine yansıtma (CommitCheckout)
        var members = _dataService.GetMembers();
        var member = members.FirstOrDefault(m => string.Equals(m.FullName, LoggedInUser, StringComparison.OrdinalIgnoreCase))
                     ?? members.FirstOrDefault(m => string.Equals(m.MemberNumber, LoggedInUser, StringComparison.OrdinalIgnoreCase))
                     ?? members.FirstOrDefault()
                     ?? new Domain.Entities.Member
                     {
                         Id = 1,
                         FullName = LoggedInUser,
                         MemberNumber = "UYE-2026-1001",
                         PhoneNumber = "0532 111 2233",
                         Email = "uye@kutuphane.edu.tr",
                         TierTitle = "Öğrenci"
                     };

        var allDomain = _dataService.GetBooks();
        var matched = allDomain.Where(db => Books.Any(b => string.Equals(b.Title, db.Title, StringComparison.OrdinalIgnoreCase))).ToList();
        if (matched.Count == 0 && Books.Count > 0)
        {
            foreach (var b in Books)
            {
                var found = allDomain.FirstOrDefault(x => string.Equals(x.Title, b.Title, StringComparison.OrdinalIgnoreCase));
                if (found != null) matched.Add(found);
                else
                {
                    matched.Add(new Domain.Entities.Book
                    {
                        Id = allDomain.Count > 0 ? allDomain.First().Id : 1,
                        Title = b.Title,
                        AuthorName = b.Author,
                        CategoryName = b.Category,
                        PageCount = b.Pages,
                        PrimaryCoverHex = b.Cover,
                        AccentFoilHex = b.Accent
                    });
                }
            }
        }

        _dataService.CommitCheckout(member, matched, 15, out _);

        await Task.Delay(2200);
        Stage = Stage.Completed; Changed?.Invoke();
        await Task.Delay(3500);
        Books.Clear();
        Stage = Stage.Idle; Changed?.Invoke();
    }

    public (string PanelTitle, string PanelId, string ListHeader, string[] Steps, string Title, string Subtitle) GetTexts()
    {
        if (Mode == TransactionMode.Add)
        {
            string[] steps =
            {
                "1. Kitap Bilgileri Girildi",
                "2. Barkod & Kategori Tanımlanıyor",
                "3. Raf Konumu Belirleniyor",
                "4. Kayıt Tamamlandı"
            };
            var (ti, su) = Stage switch
            {
                Stage.Idle => ("Yeni Kitap Ekle", "Yeni kitap bekleniyor..."),
                Stage.Preparing => ("Yeni Kitap Ekle", "Sisteme yeni kitaplar ve kayıtlar ekleniyor..."),
                Stage.Waiting => ("Yeni Kitap Ekle", "Raf konumları belirleniyor..."),
                _ => ("Kayıt Tamam!", "Kitaplar sisteme kaydedildi.")
            };
            return ("Kitap Ekleme İşlemi", "#KE-10427", "Eklenecek Kitaplar", steps, ti, su);
        }
        else
        {
            string[] steps =
            {
                "İşlem Başlatıldı",
                "Kitaplar Hazırlanıyor",
                "Onay Bekleniyor",
                "İşlem Tamamlandı"
            };
            var (ti, su) = Stage switch
            {
                Stage.Idle => ("Hoş Geldiniz", "Kitap seçiminiz için bekleniyor..."),
                Stage.Preparing => ("Hoş Geldiniz", "Seçtiğiniz kitaplar özenle hazırlanıyor..."),
                Stage.Waiting => ("Neredeyse Hazır", "Kütüphaneci onayı bekleniyor..."),
                _ => ("İyi Okumalar!", "İşleminiz tamamlandı. Keyifli okumalar dileriz.")
            };
            return ("Ödünç Alma İşlemi", "#LA-10427", "Seçilen Kitaplar", steps, ti, su);
        }
    }
}

internal static class Ui
{
    public static SolidColorBrush B(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static TextBlock Text(string s, double size = 13, FontWeight? w = null, string fg = "#FFFFFF")
    {
        // High-contrast color boost to eliminate faint/silik text on dark glass
        if (fg == "#C9BFAE" || fg == "#D8CFC0") fg = "#ECE4D6";
        else if (fg == "#A09485" || fg == "#9C9384") fg = "#DCD2C2";
        else if (fg == "#F4EFE6") fg = "#FFFFFF";

        var tb = new TextBlock
        {
            Text = s,
            FontSize = size,
            FontWeight = w ?? FontWeights.Medium,
            Foreground = B(fg),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Segoe UI, Segoe UI Variable Text")
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(tb, ClearTypeHint.Enabled);
        tb.UseLayoutRounding = true;
        tb.SnapsToDevicePixels = true;
        return tb;
    }

    public static TextBlock Icon(string glyph, double size = 16, string fg = "#FFFFFF")
    {
        if (fg == "#C9BFAE") fg = "#ECE4D6";
        var tb = new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = size,
            Foreground = B(fg),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        TextOptions.SetTextFormattingMode(tb, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(tb, TextRenderingMode.ClearType);
        RenderOptions.SetClearTypeHint(tb, ClearTypeHint.Enabled);
        tb.UseLayoutRounding = true;
        tb.SnapsToDevicePixels = true;
        return tb;
    }

    public static Border MiniCover(BookItem b, double w, double h) => new()
    {
        Width = w, Height = h, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1),
        BorderBrush = B(b.Accent), Background = new LinearGradientBrush(C(b.Cover), C(b.Accent), 60) { Opacity = 1 },
        Child = new Border { Margin = new Thickness(3), BorderThickness = new Thickness(0.6), BorderBrush = B(b.Accent), Opacity = 0.7 }
    };
}
