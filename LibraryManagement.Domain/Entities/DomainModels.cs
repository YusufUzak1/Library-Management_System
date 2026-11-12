using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Domain.Entities;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string Isbn { get; set; } = string.Empty;
    public string PublisherName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public BookCategoryTheme CategoryTheme { get; set; } = BookCategoryTheme.ClassicLiterature;
    public BookPhysicalFormat PhysicalFormat { get; set; } = BookPhysicalFormat.StandardHardcover;
    public int PublicationYear { get; set; } = 2024;
    public int PageCount { get; set; } = 320;
    public string ShelfLocation { get; set; } = "A-101";
    public int TotalCopies { get; set; } = 3;
    public int AvailableCopies { get; set; } = 2;
    public string PrimaryCoverHex { get; set; } = string.Empty;
    public string AccentFoilHex { get; set; } = string.Empty;
    public string Synopsis { get; set; } = string.Empty;
    public int MonthlyBorrowCount { get; set; }
    public List<BookCopy> Copies { get; set; } = new();
}

public class BookCopy
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string CopyCode { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string ShelfLocation { get; set; } = string.Empty;
    public CopyStatus Status { get; set; } = CopyStatus.Available;
    public string ConditionNote { get; set; } = "Kusursuz Kondisyon";
}

public class Member
{
    public int Id { get; set; }
    public string MemberNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime RegistrationDate { get; set; } = DateTime.Today.AddMonths(-8);
    public MemberStatus Status { get; set; } = MemberStatus.Active;
    public int MaxLoanLimit { get; set; } = 5;
    public int ActiveLoansCount { get; set; }
    public int OverdueCount { get; set; }
    public int TotalBorrowedAllTime { get; set; }
    public string TierTitle { get; set; } = "Kıdemli Araştırmacı";
}

public class ActiveLoanRecord
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string CopyBarcode { get; set; } = string.Empty;
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberNumber { get; set; } = string.Empty;
    public DateTime BorrowDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int RenewalCount { get; set; }
    public BookCategoryTheme CategoryTheme { get; set; }
    public int PageCount { get; set; } = 300;
    public bool IsOverdue => ReturnDate == null && DueDate.Date < DateTime.Today;
    public int DaysRemainingOrOverdue => (DueDate.Date - DateTime.Today).Days;
}

public class ReservationRecord
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public int MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberNumber { get; set; } = string.Empty;
    public int QueuePosition { get; set; } = 1;
    public DateTime RequestedAt { get; set; } = DateTime.Now.AddDays(-2);
    public DateTime ExpiresAt { get; set; } = DateTime.Now.AddDays(3);
    public ReservationStatus Status { get; set; } = ReservationStatus.WaitingInQueue;
}

public class ActivityLogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ActionType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string AccentTag { get; set; } = "Emerald";
}

public class MonthlyBorrowStat
{
    public string MonthLabel { get; set; } = string.Empty;
    public int BorrowedCount { get; set; }
    public int ReturnedCount { get; set; }
    public double NormalizedHeight { get; set; }
}

