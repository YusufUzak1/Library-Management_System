using Scriptorium.Core;

namespace Scriptorium.Models;

public sealed class UserAccount : ObservableObject
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Kütüphaneci"; // Baş Kütüphaneci, Kütüphaneci, Arşiv Görevlisi, Sistem Yöneticisi
    public string Title { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime LastLogin { get; set; } = DateTime.Now;

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "KP";
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}"
                : FullName[..Math.Min(2, FullName.Length)].ToUpperInvariant();
        }
    }

    public string StatusText => IsActive ? "Aktif" : "Pasif";
    public string LastLoginText => LastLogin.ToString("dd.MM.yyyy HH:mm");
}

public sealed class Author : ObservableObject
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Country { get; set; } = "Türkiye";
    public string BirthDeathYears { get; set; } = string.Empty;
    public string LiteraryMovement { get; set; } = string.Empty;
    public string Biography { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int TotalCopies { get; set; }

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "YZ";
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : FullName[..Math.Min(2, FullName.Length)].ToUpperInvariant();
        }
    }
}

public sealed class Category : ObservableObject
{
    public int Id { get; set; }
    public string DeweyCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShelfCorridor { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int TotalCopies { get; set; }
    public int ActiveLoans { get; set; }
}

public sealed class Publisher : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = "İstanbul";
    public int FoundedYear { get; set; } = 1980;
    public string ContactEmail { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int BookCount { get; set; }
    public int TotalCopies { get; set; }
}

public sealed class Book : ObservableObject
{
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int Id { get; set; }
    public string Isbn { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public int PublisherId { get; set; }
    public string PublisherName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string DeweyCode { get; set; } = string.Empty;
    public int PublicationYear { get; set; } = 2023;
    public string Edition { get; set; } = "1. Basım";
    public int PageCount { get; set; } = 250;
    public string Language { get; set; } = "Türkçe";
    public string ShelfLocation { get; set; } = "A1-01-A";
    public int TotalCopies { get; set; } = 1;
    public int AvailableCopies { get; set; } = 1;
    public int ReservedCopies { get; set; }
    public int TotalBorrowCount { get; set; }
    public string PhysicalCondition { get; set; } = "Çok İyi";
    public string Summary { get; set; } = string.Empty;
    public string CoverHex { get; set; } = "#1E4D3B";
    public DateTime AddedDate { get; set; } = DateTime.Today;

    public int BorrowedCopies => Math.Max(0, TotalCopies - AvailableCopies);
    public bool IsAvailable => AvailableCopies > 0;
    public string InventoryRatioText => $"{AvailableCopies} / {TotalCopies}";
    public double AvailabilityRatio => TotalCopies > 0 ? (double)AvailableCopies / TotalCopies : 0.0;

    public string AvailabilityStatus
    {
        get
        {
            if (AvailableCopies <= 0)
                return ReservedCopies > 0 ? "Rezerve / Ödünçte" : "Tümü Ödünçte";
            if (AvailableCopies == 1 && TotalCopies > 1)
                return "Son 1 Nüsha";
            return "Mevcut";
        }
    }

    public string StatusKind
    {
        get
        {
            if (AvailableCopies <= 0) return "Danger";
            if (AvailableCopies == 1 && TotalCopies > 1) return "Warning";
            return "Success";
        }
    }

    public string CoverInitials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title)) return "KT";
            var words = Title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
            return Title[..Math.Min(2, Title.Length)].ToUpperInvariant();
        }
    }
}

public sealed class Member : ObservableObject
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string MemberType { get; set; } = "Standart Okur"; // Akademik Personel, Lisansüstü Öğrenci, Üniversite Öğrencisi, Standart Okur
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string DepartmentOrInstitution { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime RegistrationDate { get; set; } = DateTime.Today;
    public string Status { get; set; } = "Aktif"; // Aktif, Askıda, Üyelik Süresi Dolmuş
    public int MaxLoanLimit { get; set; } = 5;
    public int ActiveLoansCount { get; set; }
    public int OverdueCount { get; set; }
    public int TotalBorrowedCount { get; set; }
    public decimal FineBalance { get; set; }
    public string Notes { get; set; } = string.Empty;

    public string QuotaRatioText => $"{ActiveLoansCount} / {MaxLoanLimit}";
    public double QuotaPercentage => MaxLoanLimit > 0 ? Math.Min(1.0, (double)ActiveLoansCount / MaxLoanLimit) : 0;
    public string RegistrationDateText => RegistrationDate.ToString("dd.MM.yyyy");
    public string FineBalanceText => $"{FineBalance:N2} ₺";
    public bool CanBorrow => Status == "Aktif" && ActiveLoansCount < MaxLoanLimit && OverdueCount == 0;

    public string EligibilityMessage
    {
        get
        {
            if (Status != "Aktif")
                return $"Üyelik durumu '{Status}' olduğu için yeni ödünç verilemez.";
            if (OverdueCount > 0)
                return $"Üyenin teslim süresi geçmiş {OverdueCount} adet kitabı bulunmaktadır.";
            if (ActiveLoansCount >= MaxLoanLimit)
                return $"Üye maksimum ödünç kotasına ({ActiveLoansCount}/{MaxLoanLimit}) ulaşmıştır.";
            return $"Ödünç almaya uygun ({MaxLoanLimit - ActiveLoansCount} kitap hakkı mevcut).";
        }
    }

    public string StatusKind => Status switch
    {
        "Aktif" when OverdueCount == 0 => "Success",
        "Aktif" when OverdueCount > 0 => "Warning",
        "Askıda" => "Danger",
        _ => "Info"
    };

    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "ÜY";
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : FullName[..Math.Min(2, FullName.Length)].ToUpperInvariant();
        }
    }
}

public sealed class BorrowRecord : ObservableObject
{
    public int Id { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string BookIsbn { get; set; } = string.Empty;
    public string BookBarcode { get; set; } = string.Empty;
    public string ShelfLocation { get; set; } = string.Empty;
    public int MemberId { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberPhone { get; set; } = string.Empty;
    public string MemberEmail { get; set; } = string.Empty;
    public string MemberType { get; set; } = string.Empty;
    public DateTime BorrowDate { get; set; } = DateTime.Today;
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(15);
    public DateTime? ReturnDate { get; set; }
    public int RenewalCount { get; set; }
    public string Status { get; set; } = "Ödünçte"; // Ödünçte, İade Edildi, Gecikmiş
    public decimal FineAmount { get; set; }
    public string ProcessedBy { get; set; } = "Ayşe Yılmaz";
    public string ReturnCondition { get; set; } = "Normal";
    public string Notes { get; set; } = string.Empty;

    public bool IsReturned => ReturnDate.HasValue || Status == "İade Edildi";
    public bool IsOverdue => !IsReturned && DueDate.Date < DateTime.Today;
    public bool IsDueToday => !IsReturned && DueDate.Date == DateTime.Today;

    public int OverdueDays
    {
        get
        {
            var referenceDate = ReturnDate?.Date ?? DateTime.Today;
            var diff = (referenceDate - DueDate.Date).Days;
            return Math.Max(0, diff);
        }
    }

    public decimal CalculatedFine => OverdueDays * 5.00m;
    public string CalculatedFineText => $"{(FineAmount > 0 ? FineAmount : CalculatedFine):N2} ₺";

    public string BorrowDateText => BorrowDate.ToString("dd.MM.yyyy");
    public string DueDateText => DueDate.ToString("dd.MM.yyyy");
    public string ReturnDateText => ReturnDate?.ToString("dd.MM.yyyy HH:mm") ?? "—";

    public string DueStatusLabel
    {
        get
        {
            if (IsReturned)
                return OverdueDays > 0 ? $"İade ({OverdueDays}g Gecikmeli)" : "Zamanında İade";
            var daysLeft = (DueDate.Date - DateTime.Today).Days;
            if (daysLeft < 0)
                return $"{Math.Abs(daysLeft)} Gün Gecikti";
            if (daysLeft == 0)
                return "Bugün Son Gün";
            if (daysLeft <= 2)
                return $"{daysLeft} Gün Kaldı";
            return $"{daysLeft} Gün Var";
        }
    }

    public string StatusKind
    {
        get
        {
            if (IsReturned) return "Success";
            if (IsOverdue) return "Danger";
            if (IsDueToday || (DueDate.Date - DateTime.Today).Days <= 2) return "Warning";
            return "Info";
        }
    }

    public string SeverityLevel
    {
        get
        {
            if (OverdueDays >= 15) return "Kritik (>15 Gün)";
            if (OverdueDays >= 7) return "Yüksek (7-14 Gün)";
            if (OverdueDays >= 1) return "Uyarı (1-6 Gün)";
            return "Normal";
        }
    }
}

public sealed class Reservation : ObservableObject
{
    public int Id { get; set; }
    public string ReservationCode { get; set; } = string.Empty;
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string BookIsbn { get; set; } = string.Empty;
    public string ShelfLocation { get; set; } = string.Empty;
    public int AvailableCopies { get; set; }
    public int MemberId { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberPhone { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; } = DateTime.Now;
    public DateTime ExpiryDate { get; set; } = DateTime.Now.AddDays(3);
    public int QueuePosition { get; set; } = 1;
    public string Status { get; set; } = "Bekliyor"; // Bekliyor, Rafta Ayrıldı, Tamamlandı, İptal Edildi
    public string Notes { get; set; } = string.Empty;

    public string ReservationDateText => ReservationDate.ToString("dd.MM.yyyy HH:mm");
    public string ExpiryDateText => ExpiryDate.ToString("dd.MM.yyyy");
    public string QueueText => $"{QueuePosition}. Sıra";
    public bool CanFulfill => (Status == "Bekliyor" || Status == "Rafta Ayrıldı") && AvailableCopies > 0;

    public string StatusKind => Status switch
    {
        "Rafta Ayrıldı" => "Success",
        "Bekliyor" => "Warning",
        "Tamamlandı" => "Info",
        _ => "Danger"
    };
}

public sealed class MonthlyLoanStat
{
    public string MonthName { get; set; } = string.Empty;
    public string ShortMonth { get; set; } = string.Empty;
    public int BorrowedCount { get; set; }
    public int ReturnedCount { get; set; }
    public int OverdueCount { get; set; }
    public double BorrowBarHeight { get; set; }
    public double ReturnBarHeight { get; set; }
}

public sealed class CategoryStat
{
    public string CategoryName { get; set; } = string.Empty;
    public string DeweyCode { get; set; } = string.Empty;
    public int TitleCount { get; set; }
    public int CopyCount { get; set; }
    public int LoanCount { get; set; }
    public double SharePercentage { get; set; }
}

public sealed class LibrarySettings : ObservableObject
{
    public string LibraryName { get; set; } = "Scriptorium Merkez Kütüphanesi";
    public string InstitutionName { get; set; } = "Kültür ve Akademik Araştırmalar Vakfı";
    public string BranchCode { get; set; } = "MRK-IST-01";
    public int StandardLoanDays { get; set; } = 15;
    public int AcademicLoanDays { get; set; } = 30;
    public int MaxBooksStandard { get; set; } = 5;
    public int MaxBooksAcademic { get; set; } = 10;
    public int MaxRenewalCount { get; set; } = 2;
    public decimal DailyOverdueFee { get; set; } = 5.00m;
    public int ReservationHoldHours { get; set; } = 48;
    public bool BlockLoanWhenOverdue { get; set; } = true;
    public bool AutoCalculateFines { get; set; } = true;

    // MariaDB Connection Settings
    public string DbHost { get; set; } = "127.0.0.1";
    public int DbPort { get; set; } = 3306;
    public string DbName { get; set; } = "kutuphane_otomasyonu";
    public string DbUser { get; set; } = "root";
    public string DbPassword { get; set; } = "RootPassword123!";
}

