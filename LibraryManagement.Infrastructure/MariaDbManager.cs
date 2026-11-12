using System.Data;
using Dapper;
using MySqlConnector;
using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Infrastructure;

public sealed class MariaDbManager
{
    private string? _activeConnectionString;
    public bool IsConnected { get; private set; }
    public string StatusMessage { get; private set; } = "Bağlantı başlatılıyor...";

    private readonly string[] _serverCandidateStrings =
    {
        "Server=127.0.0.1;Port=3306;User ID=root;Password=RootPassword123!;CharacterSet=utf8mb4;ConnectionTimeout=2;DefaultCommandTimeout=5;AllowUserVariables=true",
        "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharacterSet=utf8mb4;ConnectionTimeout=2;DefaultCommandTimeout=5;AllowUserVariables=true",
        "Server=127.0.0.1;Port=3306;User ID=root;Password=root;CharacterSet=utf8mb4;ConnectionTimeout=2;DefaultCommandTimeout=5;AllowUserVariables=true",
        "Server=127.0.0.1;Port=3306;User ID=chat_user;Password=ChatPassword123!;CharacterSet=utf8mb4;ConnectionTimeout=2;DefaultCommandTimeout=5;AllowUserVariables=true"
    };

    public bool Initialize()
    {
        foreach (var candidate in _serverCandidateStrings)
        {
            try
            {
                using var serverConn = new MySqlConnection(candidate);
                serverConn.Open();

                serverConn.Execute("CREATE DATABASE IF NOT EXISTS `kutuphane_otomasyonu` CHARACTER SET utf8mb4 COLLATE utf8mb4_turkish_ci;");

                var builder = new MySqlConnectionStringBuilder(candidate)
                {
                    Database = "kutuphane_otomasyonu"
                };
                _activeConnectionString = builder.ConnectionString;

                using var dbConn = new MySqlConnection(_activeConnectionString);
                dbConn.Open();

                EnsureTables(dbConn);
                IsConnected = true;
                StatusMessage = "MariaDB 11.4 Bağlı (kutuphane_otomasyonu)";
                return true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"MariaDB denendi: {ex.Message}";
            }
        }

        IsConnected = false;
        StatusMessage = "MariaDB'ye ulaşılamadı. Yerel mod aktif.";
        return false;
    }

    private void EnsureTables(MySqlConnection conn)
    {
        const string ddl = """
            CREATE TABLE IF NOT EXISTS books (
                Id INT PRIMARY KEY,
                Isbn VARCHAR(32) NOT NULL,
                Barcode VARCHAR(40) NOT NULL,
                Title VARCHAR(220) NOT NULL,
                Subtitle VARCHAR(220) NOT NULL DEFAULT '',
                AuthorId INT NOT NULL DEFAULT 1,
                AuthorName VARCHAR(140) NOT NULL,
                PublisherId INT NOT NULL DEFAULT 1,
                PublisherName VARCHAR(140) NOT NULL DEFAULT '',
                CategoryId INT NOT NULL DEFAULT 1,
                CategoryName VARCHAR(120) NOT NULL,
                DeweyCode VARCHAR(32) NOT NULL DEFAULT '800',
                PublicationYear INT NOT NULL DEFAULT 2024,
                Edition VARCHAR(64) NOT NULL DEFAULT '1. Baskı',
                PageCount INT NOT NULL DEFAULT 320,
                Language VARCHAR(50) NOT NULL DEFAULT 'Türkçe',
                ShelfLocation VARCHAR(50) NOT NULL DEFAULT 'A-01',
                TotalCopies INT NOT NULL DEFAULT 3,
                AvailableCopies INT NOT NULL DEFAULT 3,
                ReservedCopies INT NOT NULL DEFAULT 0,
                TotalBorrowCount INT NOT NULL DEFAULT 0,
                PhysicalCondition VARCHAR(50) NOT NULL DEFAULT 'İyi',
                Summary TEXT NOT NULL,
                CoverHex VARCHAR(16) NOT NULL DEFAULT '#1D3550',
                AddedDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS members (
                Id INT PRIMARY KEY,
                MemberNumber VARCHAR(40) NOT NULL UNIQUE,
                FullName VARCHAR(140) NOT NULL,
                IdentityNumber VARCHAR(20) NOT NULL DEFAULT '10000000000',
                MemberType VARCHAR(64) NOT NULL DEFAULT 'Öğrenci',
                Email VARCHAR(120) NOT NULL DEFAULT '',
                Phone VARCHAR(40) NOT NULL DEFAULT '',
                DepartmentOrInstitution VARCHAR(140) NOT NULL DEFAULT '',
                Address TEXT NOT NULL,
                RegistrationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                Status VARCHAR(40) NOT NULL DEFAULT 'Aktif',
                MaxLoanLimit INT NOT NULL DEFAULT 5,
                ActiveLoansCount INT NOT NULL DEFAULT 0,
                OverdueCount INT NOT NULL DEFAULT 0,
                TotalBorrowedCount INT NOT NULL DEFAULT 0,
                FineBalance DECIMAL(10,2) NOT NULL DEFAULT 0.00,
                Notes TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS borrow_records (
                Id INT PRIMARY KEY,
                TransactionCode VARCHAR(40) NOT NULL,
                BookId INT NOT NULL,
                BookTitle VARCHAR(220) NOT NULL,
                BookAuthor VARCHAR(140) NOT NULL,
                BookIsbn VARCHAR(32) NOT NULL,
                BookBarcode VARCHAR(40) NOT NULL,
                ShelfLocation VARCHAR(50) NOT NULL,
                MemberId INT NOT NULL,
                MemberNumber VARCHAR(40) NOT NULL,
                MemberName VARCHAR(140) NOT NULL,
                MemberPhone VARCHAR(40) NOT NULL,
                MemberEmail VARCHAR(120) NOT NULL,
                MemberType VARCHAR(64) NOT NULL,
                BorrowDate DATETIME NOT NULL,
                DueDate DATETIME NOT NULL,
                ReturnDate DATETIME NULL,
                RenewalCount INT NOT NULL DEFAULT 0,
                Status VARCHAR(40) NOT NULL DEFAULT 'Ödünçte',
                FineAmount DECIMAL(10,2) NOT NULL DEFAULT 0.00,
                ProcessedBy VARCHAR(120) NOT NULL DEFAULT 'Kullanıcı Kiosku',
                ReturnCondition VARCHAR(64) NOT NULL DEFAULT 'İyi',
                Notes TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS activity_logs (
                Id INT PRIMARY KEY,
                Title VARCHAR(120) NOT NULL,
                Description TEXT NOT NULL,
                ActorName VARCHAR(120) NOT NULL,
                Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS reservations (
                Id INT PRIMARY KEY,
                BookId INT NOT NULL,
                BookTitle VARCHAR(220) NOT NULL,
                MemberId INT NOT NULL,
                MemberName VARCHAR(140) NOT NULL,
                MemberNumber VARCHAR(40) NOT NULL,
                QueuePosition INT NOT NULL DEFAULT 1,
                ReservationDate DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ExpiryDate DATETIME NOT NULL,
                Status VARCHAR(40) NOT NULL DEFAULT 'Bekliyor'
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS users (
                Id INT AUTO_INCREMENT PRIMARY KEY,
                Username VARCHAR(64) NOT NULL UNIQUE,
                Password VARCHAR(128) NOT NULL,
                FullName VARCHAR(140) NOT NULL,
                Role VARCHAR(32) NOT NULL DEFAULT 'User',
                Email VARCHAR(120) NOT NULL DEFAULT '',
                PhoneNumber VARCHAR(40) NOT NULL DEFAULT '',
                MemberType VARCHAR(64) NOT NULL DEFAULT 'Öğrenci',
                CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            INSERT IGNORE INTO users (Username, Password, FullName, Role, Email, MemberType) VALUES 
            ('admin', '1234', 'Sistem Yöneticisi', 'Admin', 'admin@kutuphane.edu.tr', 'Personel'),
            ('kullanici', '1234', 'Örnek Öğrenci', 'User', 'ogrenci@kutuphane.edu.tr', 'Öğrenci');
        """;

        conn.Execute(ddl);
    }

    private MySqlConnection? GetOpenConnection()
    {
        if (string.IsNullOrEmpty(_activeConnectionString))
        {
            if (!Initialize()) return null;
        }

        try
        {
            var conn = new MySqlConnection(_activeConnectionString);
            conn.Open();
            IsConnected = true;
            return conn;
        }
        catch
        {
            IsConnected = false;
            return null;
        }
    }

    public static BookCategoryTheme ResolveTheme(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return BookCategoryTheme.ClassicLiterature;
        var c = category.ToLowerInvariant();
        if (c.Contains("bilim")) return BookCategoryTheme.ScienceFiction;
        if (c.Contains("fant")) return BookCategoryTheme.Fantasy;
        if (c.Contains("tarih") || c.Contains("felsefe")) return BookCategoryTheme.HistoryAndPhilosophy;
        if (c.Contains("sanat") || c.Contains("mimari")) return BookCategoryTheme.ArtAndArchitecture;
        if (c.Contains("akademik") || c.Contains("araştırma")) return BookCategoryTheme.Academic;
        if (c.Contains("çocuk")) return BookCategoryTheme.Childrens;
        if (c.Contains("şiir")) return BookCategoryTheme.Poetry;
        return BookCategoryTheme.ClassicLiterature;
    }

    public List<Book>? LoadBooks()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT 
                    Id, Title, Subtitle, AuthorName, Isbn, PublisherName, CategoryName,
                    PublicationYear, PageCount, ShelfLocation, TotalCopies, AvailableCopies,
                    CoverHex, Summary, TotalBorrowCount
                FROM books 
                ORDER BY Id ASC;").ToList();

            var list = new List<Book>();
            foreach (var r in rows)
            {
                var cat = (string)(r.CategoryName ?? "Roman");
                var hex = (string)(r.CoverHex ?? "#1D3550");
                if (string.IsNullOrWhiteSpace(hex)) hex = "#1D3550";

                list.Add(new Book
                {
                    Id = (int)r.Id,
                    Title = (string)(r.Title ?? ""),
                    Subtitle = (string)(r.Subtitle ?? ""),
                    AuthorName = (string)(r.AuthorName ?? ""),
                    Isbn = (string)(r.Isbn ?? ""),
                    PublisherName = (string)(r.PublisherName ?? ""),
                    CategoryName = cat,
                    PublicationYear = (int)(r.PublicationYear ?? 2024),
                    PageCount = (int)(r.PageCount ?? 320),
                    ShelfLocation = (string)(r.ShelfLocation ?? "A-01"),
                    TotalCopies = (int)(r.TotalCopies ?? 1),
                    AvailableCopies = (int)(r.AvailableCopies ?? 0),
                    PrimaryCoverHex = hex,
                    AccentFoilHex = "#D9B76A",
                    Synopsis = (string)(r.Summary ?? ""),
                    MonthlyBorrowCount = (int)(r.TotalBorrowCount ?? 0),
                    CategoryTheme = ResolveTheme(cat)
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadBooks error: {ex.Message}");
            return null;
        }
    }

    public List<Member>? LoadMembers()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT 
                    Id, MemberNumber, FullName, Email, Phone, MemberType,
                    RegistrationDate, Status, MaxLoanLimit, ActiveLoansCount,
                    OverdueCount, TotalBorrowedCount
                FROM members 
                ORDER BY Id ASC;").ToList();

            var list = new List<Member>();
            foreach (var r in rows)
            {
                list.Add(new Member
                {
                    Id = (int)r.Id,
                    MemberNumber = (string)(r.MemberNumber ?? ""),
                    FullName = (string)(r.FullName ?? ""),
                    Email = (string)(r.Email ?? ""),
                    PhoneNumber = (string)(r.Phone ?? ""),
                    TierTitle = (string)(r.MemberType ?? "Öğrenci"),
                    RegistrationDate = (DateTime)(r.RegistrationDate ?? DateTime.Today),
                    MaxLoanLimit = (int)(r.MaxLoanLimit ?? 5),
                    ActiveLoansCount = (int)(r.ActiveLoansCount ?? 0),
                    OverdueCount = (int)(r.OverdueCount ?? 0),
                    TotalBorrowedAllTime = (int)(r.TotalBorrowedCount ?? 0)
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadMembers error: {ex.Message}");
            return null;
        }
    }

    public List<ActiveLoanRecord>? LoadActiveLoans()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT 
                    br.Id,
                    br.BookId,
                    br.BookTitle,
                    br.BookAuthor,
                    br.BookBarcode,
                    br.MemberId,
                    br.MemberName,
                    br.MemberNumber,
                    br.BorrowDate,
                    br.DueDate,
                    br.ReturnDate,
                    br.RenewalCount,
                    br.Status,
                    COALESCE(b.PageCount, 320) AS PageCount,
                    COALESCE(b.CategoryName, 'Roman') AS CategoryName
                FROM borrow_records br
                LEFT JOIN books b ON b.Id = br.BookId
                ORDER BY br.BorrowDate DESC, br.Id DESC;").ToList();

            var list = new List<ActiveLoanRecord>();
            foreach (var r in rows)
            {
                list.Add(new ActiveLoanRecord
                {
                    Id = (int)r.Id,
                    BookId = (int)r.BookId,
                    BookTitle = (string)(r.BookTitle ?? ""),
                    AuthorName = (string)(r.BookAuthor ?? ""),
                    CopyBarcode = (string)(r.BookBarcode ?? ""),
                    MemberId = (int)r.MemberId,
                    MemberName = (string)(r.MemberName ?? ""),
                    MemberNumber = (string)(r.MemberNumber ?? ""),
                    BorrowDate = (DateTime)r.BorrowDate,
                    DueDate = (DateTime)r.DueDate,
                    ReturnDate = r.ReturnDate as DateTime?,
                    RenewalCount = (int)(r.RenewalCount ?? 0),
                    PageCount = (int)(r.PageCount ?? 320),
                    CategoryTheme = ResolveTheme((string)(r.CategoryName ?? ""))
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadActiveLoans error: {ex.Message}");
            return null;
        }
    }

    public List<ActivityLogEntry>? LoadActivityLogs()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT Id, Title, Description, ActorName, Timestamp
                FROM activity_logs
                ORDER BY Timestamp DESC, Id DESC
                LIMIT 40;").ToList();

            var list = new List<ActivityLogEntry>();
            foreach (var r in rows)
            {
                list.Add(new ActivityLogEntry
                {
                    Id = (int)r.Id,
                    Title = (string)(r.Title ?? ""),
                    Description = (string)(r.Description ?? ""),
                    ActorName = (string)(r.ActorName ?? ""),
                    Timestamp = (DateTime)r.Timestamp
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadActivityLogs error: {ex.Message}");
            return null;
        }
    }

    public List<ReservationRecord>? LoadReservations()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT 
                    Id, BookId, BookTitle, MemberId, MemberName, MemberNumber,
                    QueuePosition, ReservationDate, ExpiryDate, Status
                FROM reservations 
                ORDER BY QueuePosition ASC, Id DESC;").ToList();

            var list = new List<ReservationRecord>();
            foreach (var r in rows)
            {
                list.Add(new ReservationRecord
                {
                    Id = (int)r.Id,
                    BookId = (int)r.BookId,
                    BookTitle = (string)(r.BookTitle ?? ""),
                    MemberId = (int)r.MemberId,
                    MemberName = (string)(r.MemberName ?? ""),
                    MemberNumber = (string)(r.MemberNumber ?? ""),
                    QueuePosition = (int)(r.QueuePosition ?? 1),
                    RequestedAt = (DateTime)(r.ReservationDate ?? DateTime.Now),
                    ExpiresAt = (DateTime)(r.ExpiryDate ?? DateTime.Now.AddDays(7))
                });
            }
            return list;
        }
        catch
        {
            return new List<ReservationRecord>();
        }
    }

    public int SaveNewLoan(ActiveLoanRecord l)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return 0;

        try
        {
            int nextId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM borrow_records;");
            string trxCode = $"ODN-{DateTime.Now:yyyyMMdd}-{nextId:D4}";

            // Fetch book details if available
            var bookRow = conn.QueryFirstOrDefault("SELECT Isbn, Barcode, ShelfLocation, AuthorName, Title FROM books WHERE Id = @BookId;", new { l.BookId });
            string isbn = bookRow?.Isbn ?? "978-975-000-000-0";
            string barcode = bookRow?.Barcode ?? $"KTP-{l.BookId:D5}";
            string shelf = bookRow?.ShelfLocation ?? "A-01";
            string bookAuthor = string.IsNullOrWhiteSpace(l.AuthorName) ? (bookRow?.AuthorName ?? "Bilinmeyen Yazar") : l.AuthorName;
            string bookTitle = string.IsNullOrWhiteSpace(l.BookTitle) ? (bookRow?.Title ?? "Bilinmeyen Kitap") : l.BookTitle;

            // Fetch member details if available
            var memRow = conn.QueryFirstOrDefault("SELECT MemberNumber, FullName, Phone, Email, MemberType FROM members WHERE Id = @MemberId;", new { l.MemberId });
            string memNum = string.IsNullOrWhiteSpace(l.MemberNumber) ? (memRow?.MemberNumber ?? $"UYE-{l.MemberId:D4}") : l.MemberNumber;
            string memName = string.IsNullOrWhiteSpace(l.MemberName) ? (memRow?.FullName ?? "Kullanıcı") : l.MemberName;
            string memPhone = memRow?.Phone ?? "0532 000 0000";
            string memEmail = memRow?.Email ?? "kullanici@kutuphane.edu.tr";
            string memType = memRow?.MemberType ?? "Öğrenci";

            conn.Execute(@"
                INSERT INTO borrow_records (
                    Id, TransactionCode, BookId, BookTitle, BookAuthor, BookIsbn, BookBarcode,
                    ShelfLocation, MemberId, MemberNumber, MemberName, MemberPhone, MemberEmail, MemberType,
                    BorrowDate, DueDate, ReturnDate, RenewalCount, Status, FineAmount, ProcessedBy, ReturnCondition, Notes
                ) VALUES (
                    @Id, @TransactionCode, @BookId, @BookTitle, @BookAuthor, @BookIsbn, @BookBarcode,
                    @ShelfLocation, @MemberId, @MemberNumber, @MemberName, @MemberPhone, @MemberEmail, @MemberType,
                    @BorrowDate, @DueDate, NULL, @RenewalCount, 'Ödünçte', 0.00, 'Kullanıcı Kiosku', 'İyi', 'Kullanıcı paneli kiralama'
                );", new
            {
                Id = nextId,
                TransactionCode = trxCode,
                BookId = l.BookId,
                BookTitle = bookTitle,
                BookAuthor = bookAuthor,
                BookIsbn = isbn,
                BookBarcode = barcode,
                ShelfLocation = shelf,
                MemberId = l.MemberId,
                MemberNumber = memNum,
                MemberName = memName,
                MemberPhone = memPhone,
                MemberEmail = memEmail,
                MemberType = memType,
                BorrowDate = l.BorrowDate,
                DueDate = l.DueDate,
                RenewalCount = l.RenewalCount
            });

            // Update copies in books
            conn.Execute("UPDATE books SET AvailableCopies = GREATEST(0, AvailableCopies - 1), TotalBorrowCount = TotalBorrowCount + 1 WHERE Id = @BookId;", new { l.BookId });

            // Update members
            conn.Execute("UPDATE members SET ActiveLoansCount = ActiveLoansCount + 1, TotalBorrowedCount = TotalBorrowedCount + 1 WHERE Id = @MemberId;", new { l.MemberId });

            // Insert into activity_logs
            int nextActId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM activity_logs;");
            conn.Execute(@"
                INSERT INTO activity_logs (Id, Title, Description, ActorName, Timestamp)
                VALUES (@Id, 'Ödünç Verme', @Description, 'Kullanıcı Kiosku', CURRENT_TIMESTAMP);
            ", new
            {
                Id = nextActId,
                Description = $"{bookTitle} ({bookAuthor}) - {memName} tarafından ödünç alındı."
            });

            return nextId;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveNewLoan failed: {ex.Message}");
            return 0;
        }
    }

    public void MarkLoanReturned(int loanId)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return;

        try
        {
            var loan = conn.QueryFirstOrDefault("SELECT BookId, MemberId, BookTitle, MemberName FROM borrow_records WHERE Id = @loanId;", new { loanId });
            conn.Execute("UPDATE borrow_records SET ReturnDate = CURRENT_TIMESTAMP, Status = 'İade Edildi' WHERE Id = @loanId;", new { loanId });

            if (loan != null)
            {
                int bookId = loan.BookId;
                int memberId = loan.MemberId;
                string bookTitle = loan.BookTitle ?? "Kitap";
                string memberName = loan.MemberName ?? "Üye";

                conn.Execute("UPDATE books SET AvailableCopies = LEAST(TotalCopies, AvailableCopies + 1) WHERE Id = @bookId;", new { bookId });
                conn.Execute("UPDATE members SET ActiveLoansCount = GREATEST(0, ActiveLoansCount - 1) WHERE Id = @memberId;", new { memberId });

                int nextActId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM activity_logs;");
                conn.Execute(@"
                    INSERT INTO activity_logs (Id, Title, Description, ActorName, Timestamp)
                    VALUES (@Id, 'İade Alma', @Description, 'Yönetici', CURRENT_TIMESTAMP);
                ", new
                {
                    Id = nextActId,
                    Description = $"{bookTitle} kitabı {memberName} tarafından iade alındı."
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MarkLoanReturned error: {ex.Message}");
        }
    }

    public void ExtendLoan(int loanId, int days)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return;

        try
        {
            conn.Execute("UPDATE borrow_records SET DueDate = DATE_ADD(DueDate, INTERVAL @days DAY), RenewalCount = RenewalCount + 1 WHERE Id = @loanId;", new { loanId, days });

            var loan = conn.QueryFirstOrDefault("SELECT BookTitle, MemberName FROM borrow_records WHERE Id = @loanId;", new { loanId });
            if (loan != null)
            {
                int nextActId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM activity_logs;");
                conn.Execute(@"
                    INSERT INTO activity_logs (Id, Title, Description, ActorName, Timestamp)
                    VALUES (@Id, 'Süre Uzatma', @Description, 'Yönetici', CURRENT_TIMESTAMP);
                ", new
                {
                    Id = nextActId,
                    Description = $"{loan.BookTitle} ödünç süresi +{days} gün uzatıldı ({loan.MemberName})."
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ExtendLoan error: {ex.Message}");
        }
    }

    public void SaveNewBook(Book b)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return;

        try
        {
            int nextId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM books;");
            b.Id = nextId;
            conn.Execute(@"
                INSERT INTO books (
                    Id, Isbn, Barcode, Title, Subtitle, AuthorId, AuthorName, PublisherId, PublisherName,
                    CategoryId, CategoryName, DeweyCode, PublicationYear, Edition, PageCount, Language,
                    ShelfLocation, TotalCopies, AvailableCopies, ReservedCopies, TotalBorrowCount, PhysicalCondition,
                    Summary, CoverHex, AddedDate
                ) VALUES (
                    @Id, @Isbn, @Barcode, @Title, @Subtitle, 1, @AuthorName, 1, @PublisherName,
                    1, @CategoryName, '800', @PublicationYear, '1. Baskı', @PageCount, 'Türkçe',
                    @ShelfLocation, @TotalCopies, @AvailableCopies, 0, @MonthlyBorrowCount, 'İyi',
                    @Synopsis, @PrimaryCoverHex, CURRENT_TIMESTAMP
                );", new
            {
                b.Id,
                Isbn = string.IsNullOrWhiteSpace(b.Isbn) ? "978-0000000000" : b.Isbn,
                Barcode = $"KTP-{b.Id:D5}",
                b.Title,
                Subtitle = b.Subtitle ?? "",
                b.AuthorName,
                PublisherName = b.PublisherName ?? "Kütüphane Yayınları",
                b.CategoryName,
                PublicationYear = b.PublicationYear > 0 ? b.PublicationYear : 2024,
                PageCount = b.PageCount > 0 ? b.PageCount : 250,
                ShelfLocation = string.IsNullOrWhiteSpace(b.ShelfLocation) ? "A-01" : b.ShelfLocation,
                TotalCopies = b.TotalCopies > 0 ? b.TotalCopies : 3,
                AvailableCopies = b.AvailableCopies > 0 ? b.AvailableCopies : 3,
                MonthlyBorrowCount = b.MonthlyBorrowCount,
                Synopsis = b.Synopsis ?? "",
                PrimaryCoverHex = string.IsNullOrWhiteSpace(b.PrimaryCoverHex) ? "#1D3550" : b.PrimaryCoverHex
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveNewBook error: {ex.Message}");
        }
    }

    public void SaveNewMember(Member m)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return;

        try
        {
            int nextId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM members;");
            m.Id = nextId;
            string memNum = string.IsNullOrWhiteSpace(m.MemberNumber) ? $"UYE-2026-{nextId:D4}" : m.MemberNumber;
            m.MemberNumber = memNum;

            conn.Execute(@"
                INSERT INTO members (
                    Id, MemberNumber, FullName, IdentityNumber, MemberType, Email, Phone,
                    DepartmentOrInstitution, Address, RegistrationDate, Status, MaxLoanLimit,
                    ActiveLoansCount, OverdueCount, TotalBorrowedCount, FineBalance, Notes
                ) VALUES (
                    @Id, @MemberNumber, @FullName, '10000000000', @MemberType, @Email, @Phone,
                    'Kütüphane', 'İstanbul', CURRENT_TIMESTAMP, 'Aktif', @MaxLoanLimit,
                    @ActiveLoansCount, 0, 0, 0.00, 'Kayıt yapıldı'
                );", new
            {
                m.Id,
                MemberNumber = memNum,
                m.FullName,
                MemberType = string.IsNullOrWhiteSpace(m.TierTitle) ? "Öğrenci" : m.TierTitle,
                Email = string.IsNullOrWhiteSpace(m.Email) ? "uye@kutuphane.edu.tr" : m.Email,
                Phone = string.IsNullOrWhiteSpace(m.PhoneNumber) ? "0532 000 0000" : m.PhoneNumber,
                MaxLoanLimit = m.MaxLoanLimit > 0 ? m.MaxLoanLimit : 5,
                ActiveLoansCount = m.ActiveLoansCount
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveNewMember error: {ex.Message}");
        }
    }

    public void AddActivityLog(string title, string description, string actor)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return;

        try
        {
            int nextActId = conn.ExecuteScalar<int>("SELECT COALESCE(MAX(Id), 0) + 1 FROM activity_logs;");
            conn.Execute(@"
                INSERT INTO activity_logs (Id, Title, Description, ActorName, Timestamp)
                VALUES (@Id, @title, @description, @actor, CURRENT_TIMESTAMP);
            ", new { Id = nextActId, title, description, actor });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AddActivityLog error: {ex.Message}");
        }
    }

    public List<UserAccount>? LoadUsers()
    {
        using var conn = GetOpenConnection();
        if (conn == null) return null;

        try
        {
            var rows = conn.Query<dynamic>(@"
                SELECT Id, Username, Password, FullName, Role, Email, PhoneNumber, MemberType, CreatedAt
                FROM users
                ORDER BY Id ASC;
            ");

            var list = new List<UserAccount>();
            foreach (var r in rows)
            {
                list.Add(new UserAccount
                {
                    Id = (int)r.Id,
                    Username = (string)(r.Username ?? ""),
                    Password = (string)(r.Password ?? ""),
                    FullName = (string)(r.FullName ?? ""),
                    Role = (string)(r.Role ?? "User"),
                    Email = (string)(r.Email ?? ""),
                    PhoneNumber = (string)(r.PhoneNumber ?? ""),
                    MemberType = (string)(r.MemberType ?? "Öğrenci"),
                    CreatedAt = r.CreatedAt != null ? (DateTime)r.CreatedAt : DateTime.Now
                });
            }
            return list;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadUsers error: {ex.Message}");
            return null;
        }
    }

    public bool SaveUser(UserAccount u)
    {
        using var conn = GetOpenConnection();
        if (conn == null) return false;

        try
        {
            conn.Execute(@"
                INSERT INTO users (Username, Password, FullName, Role, Email, PhoneNumber, MemberType, CreatedAt)
                VALUES (@Username, @Password, @FullName, @Role, @Email, @PhoneNumber, @MemberType, @CreatedAt)
                ON DUPLICATE KEY UPDATE 
                    Password = VALUES(Password),
                    FullName = VALUES(FullName),
                    Role = VALUES(Role),
                    Email = VALUES(Email),
                    PhoneNumber = VALUES(PhoneNumber),
                    MemberType = VALUES(MemberType);
            ", u);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveUser error: {ex.Message}");
            return false;
        }
    }
}
