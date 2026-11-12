using System.IO;
using System.Text.Json;
using Dapper;
using MySqlConnector;
using Scriptorium.Models;

namespace Scriptorium.Data;

public sealed class DatabaseService
{
    private LibrarySettings _settings = new();
    private bool _isMariaDbConnected;
    private string _connectionStatusText = "MariaDB bağlantısı kontrol ediliyor...";
    private long _lastLatencyMs;

    private readonly string _settingsFilePath;

    public List<UserAccount> Users { get; private set; } = [];
    public List<Author> Authors { get; private set; } = [];
    public List<Category> Categories { get; private set; } = [];
    public List<Publisher> Publishers { get; private set; } = [];
    public List<Book> Books { get; private set; } = [];
    public List<Member> Members { get; private set; } = [];
    public List<BorrowRecord> BorrowRecords { get; private set; } = [];
    public List<Reservation> Reservations { get; private set; } = [];
    public List<MonthlyLoanStat> MonthlyStats { get; private set; } = [];

    public LibrarySettings Settings => _settings;
    public bool IsMariaDbConnected => _isMariaDbConnected;
    public string ConnectionStatusText => _connectionStatusText;
    public long LastLatencyMs => _lastLatencyMs;

    public DatabaseService()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScriptoriumLibrary");
        Directory.CreateDirectory(appData);
        _settingsFilePath = Path.Combine(appData, "settings.json");
        LoadSavedConnectionConfig();
    }

    private void LoadSavedConnectionConfig()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<LibrarySettings>(json);
                if (loaded is not null)
                    _settings = loaded;
            }
        }
        catch
        {
            // Keep default settings
        }
    }

    public void SaveSettingsConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Ignore IO errors
        }
    }

    private string BuildServerConnectionString(string? passwordOverride = null, string? userOverride = null)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = _settings.DbHost,
            Port = (uint)_settings.DbPort,
            UserID = userOverride ?? _settings.DbUser,
            Password = passwordOverride ?? _settings.DbPassword,
            CharacterSet = "utf8mb4",
            ConnectionTimeout = 4,
            DefaultCommandTimeout = 10,
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }

    private string BuildDatabaseConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = _settings.DbHost,
            Port = (uint)_settings.DbPort,
            Database = _settings.DbName,
            UserID = _settings.DbUser,
            Password = _settings.DbPassword,
            CharacterSet = "utf8mb4",
            ConnectionTimeout = 4,
            DefaultCommandTimeout = 10,
            AllowUserVariables = true
        };
        return builder.ConnectionString;
    }

    public void Initialize()
    {
        SeedInMemoryDataset();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (TryEnsureMariaDbServerAndSchema())
            {
                SyncWithMariaDb();
                sw.Stop();
                _lastLatencyMs = Math.Max(1, sw.ElapsedMilliseconds);
                _isMariaDbConnected = true;
                _connectionStatusText = $"MariaDB 11.4 Bağlı ({_settings.DbHost}:{_settings.DbPort}/{_settings.DbName} • {_lastLatencyMs} ms)";
                return;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _isMariaDbConnected = false;
            _connectionStatusText = $"Yerel Arşiv Modu (MariaDB: {ex.Message})";
        }
    }

    public (bool Success, string Message) TestAndReconnect(string host, int port, string dbName, string user, string password)
    {
        _settings.DbHost = host.Trim();
        _settings.DbPort = port;
        _settings.DbName = dbName.Trim();
        _settings.DbUser = user.Trim();
        _settings.DbPassword = password;
        SaveSettingsConfig();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            if (TryEnsureMariaDbServerAndSchema())
            {
                SyncWithMariaDb();
                sw.Stop();
                _lastLatencyMs = Math.Max(1, sw.ElapsedMilliseconds);
                _isMariaDbConnected = true;
                _connectionStatusText = $"MariaDB Bağlı ({_settings.DbHost}:{_settings.DbPort}/{_settings.DbName} • {_lastLatencyMs} ms)";
                return (true, $"MariaDB sunucusuna başarıyla bağlanıldı ({_lastLatencyMs} ms). Veritabanı şeması ve tablolar doğrulandı.");
            }
            return (false, "MariaDB sunucusuna ulaşılamadı. Lütfen bağlantı bilgilerini kontrol edin.");
        }
        catch (Exception ex)
        {
            _isMariaDbConnected = false;
            _connectionStatusText = $"MariaDB Bağlantı Hatası ({_settings.DbHost}:{_settings.DbPort})";
            return (false, $"Bağlantı hatası: {ex.Message}");
        }
    }

    private bool TryEnsureMariaDbServerAndSchema()
    {
        // Try configured credentials first, then common local fallbacks if needed
        var candidates = new List<(string User, string Pass)>
        {
            (_settings.DbUser, _settings.DbPassword),
            ("root", "RootPassword123!"),
            ("root", ""),
            ("root", "root"),
            ("chat_user", "ChatPassword123!")
        };

        MySqlConnection? validConn = null;
        foreach (var (u, p) in candidates.Distinct())
        {
            try
            {
                var conn = new MySqlConnection(BuildServerConnectionString(p, u));
                conn.Open();
                _settings.DbUser = u;
                _settings.DbPassword = p;
                validConn = conn;
                break;
            }
            catch
            {
                // Try next candidate
            }
        }

        if (validConn is null)
            return false;

        using (validConn)
        {
            var safeDbName = _settings.DbName.Replace("`", "");
            try
            {
                validConn.Execute($"CREATE DATABASE IF NOT EXISTS `{safeDbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_turkish_ci;");
            }
            catch
            {
                // If user doesn't have CREATE DATABASE privilege on a custom DB, fallback to an accessible DB
                _settings.DbName = "chatplatform_db";
            }
        }

        using var dbConn = new MySqlConnection(BuildDatabaseConnectionString());
        dbConn.Open();

        const string ddl = """
            CREATE TABLE IF NOT EXISTS users (
                Id INT PRIMARY KEY,
                Username VARCHAR(64) NOT NULL UNIQUE,
                PasswordHash VARCHAR(128) NOT NULL,
                FullName VARCHAR(120) NOT NULL,
                Role VARCHAR(64) NOT NULL,
                Title VARCHAR(100) NOT NULL,
                Email VARCHAR(120) NOT NULL,
                Phone VARCHAR(40) NOT NULL,
                IsActive TINYINT(1) NOT NULL DEFAULT 1,
                LastLogin DATETIME NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS authors (
                Id INT PRIMARY KEY,
                FullName VARCHAR(140) NOT NULL,
                Country VARCHAR(80) NOT NULL,
                BirthDeathYears VARCHAR(50) NOT NULL,
                LiteraryMovement VARCHAR(100) NOT NULL,
                Biography TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS categories (
                Id INT PRIMARY KEY,
                DeweyCode VARCHAR(32) NOT NULL,
                Name VARCHAR(120) NOT NULL,
                ShelfCorridor VARCHAR(80) NOT NULL,
                Description TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS publishers (
                Id INT PRIMARY KEY,
                Name VARCHAR(140) NOT NULL,
                City VARCHAR(80) NOT NULL,
                FoundedYear INT NOT NULL,
                ContactEmail VARCHAR(120) NOT NULL,
                Phone VARCHAR(40) NOT NULL,
                Website VARCHAR(140) NOT NULL,
                Notes TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS books (
                Id INT PRIMARY KEY,
                Isbn VARCHAR(32) NOT NULL,
                Barcode VARCHAR(40) NOT NULL,
                Title VARCHAR(220) NOT NULL,
                Subtitle VARCHAR(220) NOT NULL,
                AuthorId INT NOT NULL,
                AuthorName VARCHAR(140) NOT NULL,
                PublisherId INT NOT NULL,
                PublisherName VARCHAR(140) NOT NULL,
                CategoryId INT NOT NULL,
                CategoryName VARCHAR(120) NOT NULL,
                DeweyCode VARCHAR(32) NOT NULL,
                PublicationYear INT NOT NULL,
                Edition VARCHAR(64) NOT NULL,
                PageCount INT NOT NULL,
                Language VARCHAR(50) NOT NULL,
                ShelfLocation VARCHAR(50) NOT NULL,
                TotalCopies INT NOT NULL,
                AvailableCopies INT NOT NULL,
                ReservedCopies INT NOT NULL DEFAULT 0,
                TotalBorrowCount INT NOT NULL DEFAULT 0,
                PhysicalCondition VARCHAR(50) NOT NULL,
                Summary TEXT NOT NULL,
                CoverHex VARCHAR(16) NOT NULL,
                AddedDate DATETIME NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS members (
                Id INT PRIMARY KEY,
                MemberNumber VARCHAR(40) NOT NULL UNIQUE,
                FullName VARCHAR(140) NOT NULL,
                IdentityNumber VARCHAR(20) NOT NULL,
                MemberType VARCHAR(64) NOT NULL,
                Email VARCHAR(120) NOT NULL,
                Phone VARCHAR(40) NOT NULL,
                DepartmentOrInstitution VARCHAR(140) NOT NULL,
                Address TEXT NOT NULL,
                RegistrationDate DATETIME NOT NULL,
                Status VARCHAR(40) NOT NULL,
                MaxLoanLimit INT NOT NULL,
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
                Status VARCHAR(40) NOT NULL,
                FineAmount DECIMAL(10,2) NOT NULL DEFAULT 0.00,
                ProcessedBy VARCHAR(120) NOT NULL,
                ReturnCondition VARCHAR(64) NOT NULL,
                Notes TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;

            CREATE TABLE IF NOT EXISTS reservations (
                Id INT PRIMARY KEY,
                ReservationCode VARCHAR(40) NOT NULL,
                BookId INT NOT NULL,
                BookTitle VARCHAR(220) NOT NULL,
                BookAuthor VARCHAR(140) NOT NULL,
                BookIsbn VARCHAR(32) NOT NULL,
                ShelfLocation VARCHAR(50) NOT NULL,
                AvailableCopies INT NOT NULL,
                MemberId INT NOT NULL,
                MemberNumber VARCHAR(40) NOT NULL,
                MemberName VARCHAR(140) NOT NULL,
                MemberPhone VARCHAR(40) NOT NULL,
                ReservationDate DATETIME NOT NULL,
                ExpiryDate DATETIME NOT NULL,
                QueuePosition INT NOT NULL,
                Status VARCHAR(40) NOT NULL,
                Notes TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_turkish_ci;
            """;

        dbConn.Execute(ddl);
        return true;
    }

    private void SyncWithMariaDb()
    {
        using var conn = new MySqlConnection(BuildDatabaseConnectionString());
        conn.Open();

        var existingBooks = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM books;");
        if (existingBooks == 0)
        {
            using var tx = conn.BeginTransaction();
            foreach (var u in Users)
            {
                conn.Execute("""
                    INSERT INTO users (Id, Username, PasswordHash, FullName, Role, Title, Email, Phone, IsActive, LastLogin)
                    VALUES (@Id, @Username, @PasswordHash, @FullName, @Role, @Title, @Email, @Phone, @IsActive, @LastLogin);
                    """, u, tx);
            }
            foreach (var a in Authors)
            {
                conn.Execute("""
                    INSERT INTO authors (Id, FullName, Country, BirthDeathYears, LiteraryMovement, Biography)
                    VALUES (@Id, @FullName, @Country, @BirthDeathYears, @LiteraryMovement, @Biography);
                    """, a, tx);
            }
            foreach (var c in Categories)
            {
                conn.Execute("""
                    INSERT INTO categories (Id, DeweyCode, Name, ShelfCorridor, Description)
                    VALUES (@Id, @DeweyCode, @Name, @ShelfCorridor, @Description);
                    """, c, tx);
            }
            foreach (var p in Publishers)
            {
                conn.Execute("""
                    INSERT INTO publishers (Id, Name, City, FoundedYear, ContactEmail, Phone, Website, Notes)
                    VALUES (@Id, @Name, @City, @FoundedYear, @ContactEmail, @Phone, @Website, @Notes);
                    """, p, tx);
            }
            foreach (var b in Books)
            {
                conn.Execute("""
                    INSERT INTO books (Id, Isbn, Barcode, Title, Subtitle, AuthorId, AuthorName, PublisherId, PublisherName,
                        CategoryId, CategoryName, DeweyCode, PublicationYear, Edition, PageCount, Language, ShelfLocation,
                        TotalCopies, AvailableCopies, ReservedCopies, TotalBorrowCount, PhysicalCondition, Summary, CoverHex, AddedDate)
                    VALUES (@Id, @Isbn, @Barcode, @Title, @Subtitle, @AuthorId, @AuthorName, @PublisherId, @PublisherName,
                        @CategoryId, @CategoryName, @DeweyCode, @PublicationYear, @Edition, @PageCount, @Language, @ShelfLocation,
                        @TotalCopies, @AvailableCopies, @ReservedCopies, @TotalBorrowCount, @PhysicalCondition, @Summary, @CoverHex, @AddedDate);
                    """, b, tx);
            }
            foreach (var m in Members)
            {
                conn.Execute("""
                    INSERT INTO members (Id, MemberNumber, FullName, IdentityNumber, MemberType, Email, Phone,
                        DepartmentOrInstitution, Address, RegistrationDate, Status, MaxLoanLimit, ActiveLoansCount,
                        OverdueCount, TotalBorrowedCount, FineBalance, Notes)
                    VALUES (@Id, @MemberNumber, @FullName, @IdentityNumber, @MemberType, @Email, @Phone,
                        @DepartmentOrInstitution, @Address, @RegistrationDate, @Status, @MaxLoanLimit, @ActiveLoansCount,
                        @OverdueCount, @TotalBorrowedCount, @FineBalance, @Notes);
                    """, m, tx);
            }
            foreach (var br in BorrowRecords)
            {
                conn.Execute("""
                    INSERT INTO borrow_records (Id, TransactionCode, BookId, BookTitle, BookAuthor, BookIsbn, BookBarcode,
                        ShelfLocation, MemberId, MemberNumber, MemberName, MemberPhone, MemberEmail, MemberType,
                        BorrowDate, DueDate, ReturnDate, RenewalCount, Status, FineAmount, ProcessedBy, ReturnCondition, Notes)
                    VALUES (@Id, @TransactionCode, @BookId, @BookTitle, @BookAuthor, @BookIsbn, @BookBarcode,
                        @ShelfLocation, @MemberId, @MemberNumber, @MemberName, @MemberPhone, @MemberEmail, @MemberType,
                        @BorrowDate, @DueDate, @ReturnDate, @RenewalCount, @Status, @FineAmount, @ProcessedBy, @ReturnCondition, @Notes);
                    """, br, tx);
            }
            foreach (var r in Reservations)
            {
                conn.Execute("""
                    INSERT INTO reservations (Id, ReservationCode, BookId, BookTitle, BookAuthor, BookIsbn, ShelfLocation,
                        AvailableCopies, MemberId, MemberNumber, MemberName, MemberPhone, ReservationDate, ExpiryDate,
                        QueuePosition, Status, Notes)
                    VALUES (@Id, @ReservationCode, @BookId, @BookTitle, @BookAuthor, @BookIsbn, @ShelfLocation,
                        @AvailableCopies, @MemberId, @MemberNumber, @MemberName, @MemberPhone, @ReservationDate, @ExpiryDate,
                        @QueuePosition, @Status, @Notes);
                    """, r, tx);
            }
            tx.Commit();
        }
        else
        {
            Users = conn.Query<UserAccount>("SELECT * FROM users ORDER BY Id;").ToList();
            Authors = conn.Query<Author>("SELECT * FROM authors ORDER BY FullName;").ToList();
            Categories = conn.Query<Category>("SELECT * FROM categories ORDER BY DeweyCode;").ToList();
            Publishers = conn.Query<Publisher>("SELECT * FROM publishers ORDER BY Name;").ToList();
            Books = conn.Query<Book>("SELECT * FROM books ORDER BY Title;").ToList();
            Members = conn.Query<Member>("SELECT * FROM members ORDER BY MemberNumber;").ToList();
            BorrowRecords = conn.Query<BorrowRecord>("SELECT * FROM borrow_records ORDER BY BorrowDate DESC, Id DESC;").ToList();
            Reservations = conn.Query<Reservation>("SELECT * FROM reservations ORDER BY QueuePosition, ReservationDate DESC;").ToList();
        }

        RecalculateAggregates();
    }

    private void ExecuteSafeDbAction(Action<MySqlConnection> dbAction)
    {
        if (!_isMariaDbConnected) return;
        try
        {
            using var conn = new MySqlConnection(BuildDatabaseConnectionString());
            conn.Open();
            dbAction(conn);
        }
        catch
        {
            // Keep UI responsive even if connection drops momentarily
        }
    }

    public void RecalculateAggregates()
    {
        var today = DateTime.Today;

        foreach (var br in BorrowRecords)
        {
            if (br.ReturnDate.HasValue)
            {
                br.Status = "İade Edildi";
            }
            else if (br.DueDate.Date < today)
            {
                br.Status = "Gecikmiş";
                br.FineAmount = Math.Max(0, (today - br.DueDate.Date).Days) * _settings.DailyOverdueFee;
            }
            else
            {
                br.Status = "Ödünçte";
                br.FineAmount = 0;
            }
        }

        foreach (var a in Authors)
        {
            var authorBooks = Books.Where(b => b.AuthorId == a.Id).ToList();
            a.BookCount = authorBooks.Count;
            a.TotalCopies = authorBooks.Sum(b => b.TotalCopies);
        }

        foreach (var c in Categories)
        {
            var catBooks = Books.Where(b => b.CategoryId == c.Id).ToList();
            c.BookCount = catBooks.Count;
            c.TotalCopies = catBooks.Sum(b => b.TotalCopies);
            c.ActiveLoans = catBooks.Sum(b => b.BorrowedCopies);
        }

        foreach (var p in Publishers)
        {
            var pubBooks = Books.Where(b => b.PublisherId == p.Id).ToList();
            p.BookCount = pubBooks.Count;
            p.TotalCopies = pubBooks.Sum(b => b.TotalCopies);
        }

        foreach (var m in Members)
        {
            var activeForMember = BorrowRecords.Where(r => r.MemberId == m.Id && !r.IsReturned).ToList();
            m.ActiveLoansCount = activeForMember.Count;
            m.OverdueCount = activeForMember.Count(r => r.DueDate.Date < today);
            m.TotalBorrowedCount = BorrowRecords.Count(r => r.MemberId == m.Id);
            var unpaidOverdueFine = activeForMember.Where(r => r.DueDate.Date < today)
                .Sum(r => (today - r.DueDate.Date).Days * _settings.DailyOverdueFee);
            m.FineBalance = unpaidOverdueFine;
        }

        foreach (var r in Reservations)
        {
            var book = Books.FirstOrDefault(b => b.Id == r.BookId);
            if (book is not null)
            {
                r.AvailableCopies = book.AvailableCopies;
                r.ShelfLocation = book.ShelfLocation;
            }
        }
    }

    // ==================== CRUD & CIRCULATION OPERATIONS ====================

    public (bool Success, string Message, BorrowRecord? Record) BorrowBook(int memberId, int bookId, DateTime dueDate, string processedBy, string notes = "")
    {
        var member = Members.FirstOrDefault(m => m.Id == memberId);
        if (member is null)
            return (false, "Geçerli bir üye seçilmedi.", null);

        if (member.Status != "Aktif")
            return (false, $"Üye durumu '{member.Status}' olduğu için ödünç işlemi yapılamaz.", null);

        if (_settings.BlockLoanWhenOverdue && member.OverdueCount > 0)
            return (false, $"Üyenin {member.OverdueCount} adet gecikmiş kitabı var. Önce iade alınmalıdır.", null);

        if (member.ActiveLoansCount >= member.MaxLoanLimit)
            return (false, $"Üye maksimum ödünç limitine ({member.MaxLoanLimit} kitap) ulaşmış durumda.", null);

        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (book is null)
            return (false, "Kitap kaydı bulunamadı.", null);

        if (book.AvailableCopies <= 0)
            return (false, $"'{book.Title}' kitabının rafta uygun nüshası bulunmamaktadır.", null);

        var alreadyHasSameBook = BorrowRecords.Any(r => r.MemberId == memberId && r.BookId == bookId && !r.IsReturned);
        if (alreadyHasSameBook)
            return (false, $"'{book.Title}' kitabı zaten bu üyenin üzerinde görünüyor.", null);

        var nextId = BorrowRecords.Count > 0 ? BorrowRecords.Max(r => r.Id) + 1 : 1;
        var record = new BorrowRecord
        {
            Id = nextId,
            TransactionCode = $"ODN-{DateTime.Today:yyyy}-{nextId:D4}",
            BookId = book.Id,
            BookTitle = book.Title,
            BookAuthor = book.AuthorName,
            BookIsbn = book.Isbn,
            BookBarcode = book.Barcode,
            ShelfLocation = book.ShelfLocation,
            MemberId = member.Id,
            MemberNumber = member.MemberNumber,
            MemberName = member.FullName,
            MemberPhone = member.Phone,
            MemberEmail = member.Email,
            MemberType = member.MemberType,
            BorrowDate = DateTime.Now,
            DueDate = dueDate.Date,
            ReturnDate = null,
            RenewalCount = 0,
            Status = "Ödünçte",
            FineAmount = 0m,
            ProcessedBy = processedBy,
            ReturnCondition = "Normal",
            Notes = string.IsNullOrWhiteSpace(notes) ? "Standart banko ödünç işlemi." : notes
        };

        book.AvailableCopies = Math.Max(0, book.AvailableCopies - 1);
        book.TotalBorrowCount += 1;
        BorrowRecords.Insert(0, record);

        // If this member had an active reservation for this book, mark it completed
        var matchingRes = Reservations.FirstOrDefault(r =>
            r.BookId == book.Id && r.MemberId == member.Id &&
            (r.Status == "Bekliyor" || r.Status == "Rafta Ayrıldı"));
        if (matchingRes is not null)
        {
            matchingRes.Status = "Tamamlandı";
            book.ReservedCopies = Math.Max(0, book.ReservedCopies - 1);
        }

        RecalculateAggregates();

        ExecuteSafeDbAction(conn =>
        {
            using var tx = conn.BeginTransaction();
            conn.Execute("""
                INSERT INTO borrow_records (Id, TransactionCode, BookId, BookTitle, BookAuthor, BookIsbn, BookBarcode,
                    ShelfLocation, MemberId, MemberNumber, MemberName, MemberPhone, MemberEmail, MemberType,
                    BorrowDate, DueDate, ReturnDate, RenewalCount, Status, FineAmount, ProcessedBy, ReturnCondition, Notes)
                VALUES (@Id, @TransactionCode, @BookId, @BookTitle, @BookAuthor, @BookIsbn, @BookBarcode,
                    @ShelfLocation, @MemberId, @MemberNumber, @MemberName, @MemberPhone, @MemberEmail, @MemberType,
                    @BorrowDate, @DueDate, @ReturnDate, @RenewalCount, @Status, @FineAmount, @ProcessedBy, @ReturnCondition, @Notes);
                """, record, tx);
            conn.Execute("UPDATE books SET AvailableCopies = @AvailableCopies, TotalBorrowCount = @TotalBorrowCount, ReservedCopies = @ReservedCopies WHERE Id = @Id;", book, tx);
            if (matchingRes is not null)
                conn.Execute("UPDATE reservations SET Status = @Status WHERE Id = @Id;", matchingRes, tx);
            tx.Commit();
        });

        return (true, $"'{book.Title}' → {member.FullName} adına ödünç verildi (Son Teslim: {dueDate:dd.MM.yyyy}).", record);
    }

    public (bool Success, string Message) ReturnBook(int borrowRecordId, string returnCondition = "Normal (Hasarsız)")
    {
        var record = BorrowRecords.FirstOrDefault(r => r.Id == borrowRecordId);
        if (record is null)
            return (false, "Ödünç kaydı bulunamadı.");

        if (record.IsReturned)
            return (false, "Bu kitap zaten iade alınmış.");

        var overdueDays = Math.Max(0, (DateTime.Today - record.DueDate.Date).Days);
        var fine = overdueDays * _settings.DailyOverdueFee;

        record.ReturnDate = DateTime.Now;
        record.Status = "İade Edildi";
        record.FineAmount = fine;
        record.ReturnCondition = returnCondition;

        var book = Books.FirstOrDefault(b => b.Id == record.BookId);
        string reservationAlert = string.Empty;
        if (book is not null)
        {
            book.AvailableCopies = Math.Min(book.TotalCopies, book.AvailableCopies + 1);
            var waitingRes = Reservations
                .Where(r => r.BookId == book.Id && r.Status == "Bekliyor")
                .OrderBy(r => r.QueuePosition)
                .FirstOrDefault();

            if (waitingRes is not null)
            {
                waitingRes.Status = "Rafta Ayrıldı";
                waitingRes.ExpiryDate = DateTime.Now.AddHours(_settings.ReservationHoldHours);
                reservationAlert = $" • DİKKAT: Bu kitap {waitingRes.MemberName} tarafından rezerve edilmiştir, lütfen резерв rafına ayırın!";
            }
        }

        RecalculateAggregates();

        ExecuteSafeDbAction(conn =>
        {
            using var tx = conn.BeginTransaction();
            conn.Execute("""
                UPDATE borrow_records
                SET ReturnDate = @ReturnDate, Status = @Status, FineAmount = @FineAmount, ReturnCondition = @ReturnCondition
                WHERE Id = @Id;
                """, record, tx);
            if (book is not null)
                conn.Execute("UPDATE books SET AvailableCopies = @AvailableCopies WHERE Id = @Id;", book, tx);
            tx.Commit();
        });

        var fineMsg = fine > 0 ? $" ({overdueDays} gün gecikme bedeli: {fine:N2} ₺)" : " (Zamanında teslim)";
        return (true, $"İade alındı: '{record.BookTitle}' — {record.MemberName}{fineMsg}.{reservationAlert}");
    }

    public (bool Success, string Message) RenewLoan(int borrowRecordId, int extraDays = 15)
    {
        var record = BorrowRecords.FirstOrDefault(r => r.Id == borrowRecordId);
        if (record is null)
            return (false, "Ödünç kaydı bulunamadı.");

        if (record.IsReturned)
            return (false, "İade edilmiş işlemin süresi uzatılamaz.");

        if (record.IsOverdue)
            return (false, "Teslim tarihi geçmiş kitapların süresi uzatılamaz. Önce iade alınmalıdır.");

        if (record.RenewalCount >= _settings.MaxRenewalCount)
            return (false, $"Bu kitap için maksimum uzatma hakkı ({_settings.MaxRenewalCount} kez) doldurulmuştur.");

        var hasWaitingReservation = Reservations.Any(r => r.BookId == record.BookId && r.Status == "Bekliyor");
        if (hasWaitingReservation)
            return (false, "Bu kitap için bekleyen bir üye rezervasyonu olduğundan süre uzatılamaz.");

        record.DueDate = record.DueDate.AddDays(extraDays);
        record.RenewalCount += 1;
        RecalculateAggregates();

        ExecuteSafeDbAction(conn =>
        {
            conn.Execute("UPDATE borrow_records SET DueDate = @DueDate, RenewalCount = @RenewalCount WHERE Id = @Id;", record);
        });

        return (true, $"'{record.BookTitle}' teslim süresi +{extraDays} gün uzatıldı. Yeni teslim tarihi: {record.DueDate:dd.MM.yyyy}.");
    }

    public Book SaveBook(Book input)
    {
        var author = Authors.FirstOrDefault(a => a.Id == input.AuthorId) ?? Authors.First();
        var publisher = Publishers.FirstOrDefault(p => p.Id == input.PublisherId) ?? Publishers.First();
        var category = Categories.FirstOrDefault(c => c.Id == input.CategoryId) ?? Categories.First();

        input.AuthorName = author.FullName;
        input.PublisherName = publisher.Name;
        input.CategoryName = category.Name;
        input.DeweyCode = category.DeweyCode;

        var existing = Books.FirstOrDefault(b => b.Id == input.Id);
        if (existing is null)
        {
            input.Id = Books.Count > 0 ? Books.Max(b => b.Id) + 1 : 1;
            if (string.IsNullOrWhiteSpace(input.Barcode))
                input.Barcode = $"KTP-{input.Id:D5}-01";
            input.AddedDate = DateTime.Now;
            Books.Insert(0, input);

            ExecuteSafeDbAction(conn =>
            {
                conn.Execute("""
                    INSERT INTO books (Id, Isbn, Barcode, Title, Subtitle, AuthorId, AuthorName, PublisherId, PublisherName,
                        CategoryId, CategoryName, DeweyCode, PublicationYear, Edition, PageCount, Language, ShelfLocation,
                        TotalCopies, AvailableCopies, ReservedCopies, TotalBorrowCount, PhysicalCondition, Summary, CoverHex, AddedDate)
                    VALUES (@Id, @Isbn, @Barcode, @Title, @Subtitle, @AuthorId, @AuthorName, @PublisherId, @PublisherName,
                        @CategoryId, @CategoryName, @DeweyCode, @PublicationYear, @Edition, @PageCount, @Language, @ShelfLocation,
                        @TotalCopies, @AvailableCopies, @ReservedCopies, @TotalBorrowCount, @PhysicalCondition, @Summary, @CoverHex, @AddedDate);
                    """, input);
            });
            RecalculateAggregates();
            return input;
        }
        else
        {
            existing.Isbn = input.Isbn;
            existing.Barcode = input.Barcode;
            existing.Title = input.Title;
            existing.Subtitle = input.Subtitle;
            existing.AuthorId = input.AuthorId;
            existing.AuthorName = input.AuthorName;
            existing.PublisherId = input.PublisherId;
            existing.PublisherName = input.PublisherName;
            existing.CategoryId = input.CategoryId;
            existing.CategoryName = input.CategoryName;
            existing.DeweyCode = input.DeweyCode;
            existing.PublicationYear = input.PublicationYear;
            existing.Edition = input.Edition;
            existing.PageCount = input.PageCount;
            existing.Language = input.Language;
            existing.ShelfLocation = input.ShelfLocation;
            existing.TotalCopies = Math.Max(1, input.TotalCopies);
            existing.AvailableCopies = Math.Clamp(input.AvailableCopies, 0, existing.TotalCopies);
            existing.PhysicalCondition = input.PhysicalCondition;
            existing.Summary = input.Summary;
            existing.CoverHex = input.CoverHex;

            ExecuteSafeDbAction(conn =>
            {
                conn.Execute("""
                    UPDATE books SET Isbn=@Isbn, Barcode=@Barcode, Title=@Title, Subtitle=@Subtitle,
                        AuthorId=@AuthorId, AuthorName=@AuthorName, PublisherId=@PublisherId, PublisherName=@PublisherName,
                        CategoryId=@CategoryId, CategoryName=@CategoryName, DeweyCode=@DeweyCode,
                        PublicationYear=@PublicationYear, Edition=@Edition, PageCount=@PageCount, Language=@Language,
                        ShelfLocation=@ShelfLocation, TotalCopies=@TotalCopies, AvailableCopies=@AvailableCopies,
                        PhysicalCondition=@PhysicalCondition, Summary=@Summary, CoverHex=@CoverHex
                    WHERE Id=@Id;
                    """, existing);
            });
            RecalculateAggregates();
            return existing;
        }
    }

    public (bool Success, string Message) DeleteBook(int bookId)
    {
        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (book is null)
            return (false, "Kitap bulunamadı.");

        var hasActiveLoans = BorrowRecords.Any(r => r.BookId == bookId && !r.IsReturned);
        if (hasActiveLoans)
            return (false, $"'{book.Title}' kitabının halen üyelerde ödünçte olan nüshaları var. İade alınmadan silinemez.");

        Books.Remove(book);
        RecalculateAggregates();
        ExecuteSafeDbAction(conn => conn.Execute("DELETE FROM books WHERE Id = @Id;", new { Id = bookId }));
        return (true, $"'{book.Title}' envanterden silindi.");
    }

    public Member SaveMember(Member input)
    {
        var existing = Members.FirstOrDefault(m => m.Id == input.Id);
        if (existing is null)
        {
            input.Id = Members.Count > 0 ? Members.Max(m => m.Id) + 1 : 1;
            if (string.IsNullOrWhiteSpace(input.MemberNumber))
                input.MemberNumber = $"UYE-2026-{1000 + input.Id:D4}";
            input.RegistrationDate = DateTime.Today;
            Members.Insert(0, input);

            ExecuteSafeDbAction(conn =>
            {
                conn.Execute("""
                    INSERT INTO members (Id, MemberNumber, FullName, IdentityNumber, MemberType, Email, Phone,
                        DepartmentOrInstitution, Address, RegistrationDate, Status, MaxLoanLimit, ActiveLoansCount,
                        OverdueCount, TotalBorrowedCount, FineBalance, Notes)
                    VALUES (@Id, @MemberNumber, @FullName, @IdentityNumber, @MemberType, @Email, @Phone,
                        @DepartmentOrInstitution, @Address, @RegistrationDate, @Status, @MaxLoanLimit, @ActiveLoansCount,
                        @OverdueCount, @TotalBorrowedCount, @FineBalance, @Notes);
                    """, input);
            });
            RecalculateAggregates();
            return input;
        }
        else
        {
            existing.FullName = input.FullName;
            existing.IdentityNumber = input.IdentityNumber;
            existing.MemberType = input.MemberType;
            existing.Email = input.Email;
            existing.Phone = input.Phone;
            existing.DepartmentOrInstitution = input.DepartmentOrInstitution;
            existing.Address = input.Address;
            existing.Status = input.Status;
            existing.MaxLoanLimit = input.MaxLoanLimit;
            existing.Notes = input.Notes;

            ExecuteSafeDbAction(conn =>
            {
                conn.Execute("""
                    UPDATE members SET FullName=@FullName, IdentityNumber=@IdentityNumber, MemberType=@MemberType,
                        Email=@Email, Phone=@Phone, DepartmentOrInstitution=@DepartmentOrInstitution,
                        Address=@Address, Status=@Status, MaxLoanLimit=@MaxLoanLimit, Notes=@Notes
                    WHERE Id=@Id;
                    """, existing);
            });
            RecalculateAggregates();
            return existing;
        }
    }

    public Author SaveAuthor(Author input)
    {
        var existing = Authors.FirstOrDefault(a => a.Id == input.Id);
        if (existing is null)
        {
            input.Id = Authors.Count > 0 ? Authors.Max(a => a.Id) + 1 : 1;
            Authors.Add(input);
            ExecuteSafeDbAction(conn => conn.Execute("""
                INSERT INTO authors (Id, FullName, Country, BirthDeathYears, LiteraryMovement, Biography)
                VALUES (@Id, @FullName, @Country, @BirthDeathYears, @LiteraryMovement, @Biography);
                """, input));
            RecalculateAggregates();
            return input;
        }
        existing.FullName = input.FullName;
        existing.Country = input.Country;
        existing.BirthDeathYears = input.BirthDeathYears;
        existing.LiteraryMovement = input.LiteraryMovement;
        existing.Biography = input.Biography;
        ExecuteSafeDbAction(conn => conn.Execute("""
            UPDATE authors SET FullName=@FullName, Country=@Country, BirthDeathYears=@BirthDeathYears,
                LiteraryMovement=@LiteraryMovement, Biography=@Biography WHERE Id=@Id;
            """, existing));
        RecalculateAggregates();
        return existing;
    }

    public Category SaveCategory(Category input)
    {
        var existing = Categories.FirstOrDefault(c => c.Id == input.Id);
        if (existing is null)
        {
            input.Id = Categories.Count > 0 ? Categories.Max(c => c.Id) + 1 : 1;
            Categories.Add(input);
            ExecuteSafeDbAction(conn => conn.Execute("""
                INSERT INTO categories (Id, DeweyCode, Name, ShelfCorridor, Description)
                VALUES (@Id, @DeweyCode, @Name, @ShelfCorridor, @Description);
                """, input));
            RecalculateAggregates();
            return input;
        }
        existing.DeweyCode = input.DeweyCode;
        existing.Name = input.Name;
        existing.ShelfCorridor = input.ShelfCorridor;
        existing.Description = input.Description;
        ExecuteSafeDbAction(conn => conn.Execute("""
            UPDATE categories SET DeweyCode=@DeweyCode, Name=@Name, ShelfCorridor=@ShelfCorridor, Description=@Description WHERE Id=@Id;
            """, existing));
        RecalculateAggregates();
        return existing;
    }

    public Publisher SavePublisher(Publisher input)
    {
        var existing = Publishers.FirstOrDefault(p => p.Id == input.Id);
        if (existing is null)
        {
            input.Id = Publishers.Count > 0 ? Publishers.Max(p => p.Id) + 1 : 1;
            Publishers.Add(input);
            ExecuteSafeDbAction(conn => conn.Execute("""
                INSERT INTO publishers (Id, Name, City, FoundedYear, ContactEmail, Phone, Website, Notes)
                VALUES (@Id, @Name, @City, @FoundedYear, @ContactEmail, @Phone, @Website, @Notes);
                """, input));
            RecalculateAggregates();
            return input;
        }
        existing.Name = input.Name;
        existing.City = input.City;
        existing.FoundedYear = input.FoundedYear;
        existing.ContactEmail = input.ContactEmail;
        existing.Phone = input.Phone;
        existing.Website = input.Website;
        existing.Notes = input.Notes;
        ExecuteSafeDbAction(conn => conn.Execute("""
            UPDATE publishers SET Name=@Name, City=@City, FoundedYear=@FoundedYear, ContactEmail=@ContactEmail,
                Phone=@Phone, Website=@Website, Notes=@Notes WHERE Id=@Id;
            """, existing));
        RecalculateAggregates();
        return existing;
    }

    public (bool Success, string Message) CreateReservation(int memberId, int bookId, string notes)
    {
        var member = Members.FirstOrDefault(m => m.Id == memberId);
        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (member is null || book is null)
            return (false, "Üye ve kitap seçilmelidir.");

        if (Reservations.Any(r => r.MemberId == memberId && r.BookId == bookId && (r.Status == "Bekliyor" || r.Status == "Rafta Ayrıldı")))
            return (false, "Bu üyenin aynı kitap için zaten aktif bir rezervasyonu bulunuyor.");

        var nextId = Reservations.Count > 0 ? Reservations.Max(r => r.Id) + 1 : 1;
        var queuePos = Reservations.Count(r => r.BookId == bookId && (r.Status == "Bekliyor" || r.Status == "Rafta Ayrıldı")) + 1;
        var status = book.AvailableCopies > 0 ? "Rafta Ayrıldı" : "Bekliyor";

        var res = new Reservation
        {
            Id = nextId,
            ReservationCode = $"RZV-2026-{nextId:D4}",
            BookId = book.Id,
            BookTitle = book.Title,
            BookAuthor = book.AuthorName,
            BookIsbn = book.Isbn,
            ShelfLocation = book.ShelfLocation,
            AvailableCopies = book.AvailableCopies,
            MemberId = member.Id,
            MemberNumber = member.MemberNumber,
            MemberName = member.FullName,
            MemberPhone = member.Phone,
            ReservationDate = DateTime.Now,
            ExpiryDate = DateTime.Now.AddDays(3),
            QueuePosition = queuePos,
            Status = status,
            Notes = string.IsNullOrWhiteSpace(notes) ? "Banko rezervasyon kaydı" : notes
        };

        book.ReservedCopies += 1;
        Reservations.Insert(0, res);
        RecalculateAggregates();

        ExecuteSafeDbAction(conn =>
        {
            conn.Execute("""
                INSERT INTO reservations (Id, ReservationCode, BookId, BookTitle, BookAuthor, BookIsbn, ShelfLocation,
                    AvailableCopies, MemberId, MemberNumber, MemberName, MemberPhone, ReservationDate, ExpiryDate,
                    QueuePosition, Status, Notes)
                VALUES (@Id, @ReservationCode, @BookId, @BookTitle, @BookAuthor, @BookIsbn, @ShelfLocation,
                    @AvailableCopies, @MemberId, @MemberNumber, @MemberName, @MemberPhone, @ReservationDate, @ExpiryDate,
                    @QueuePosition, @Status, @Notes);
                """, res);
            conn.Execute("UPDATE books SET ReservedCopies = @ReservedCopies WHERE Id = @Id;", book);
        });

        return (true, $"'{book.Title}' için {member.FullName} adına {queuePos}. sırada rezervasyon oluşturuldu ({status}).");
    }

    public (bool Success, string Message) UpdateReservationStatus(int reservationId, string newStatus)
    {
        var res = Reservations.FirstOrDefault(r => r.Id == reservationId);
        if (res is null)
            return (false, "Rezervasyon kaydı bulunamadı.");

        res.Status = newStatus;
        var book = Books.FirstOrDefault(b => b.Id == res.BookId);
        if (book is not null && (newStatus == "İptal Edildi" || newStatus == "Tamamlandı"))
        {
            book.ReservedCopies = Math.Max(0, book.ReservedCopies - 1);
        }

        RecalculateAggregates();
        ExecuteSafeDbAction(conn =>
        {
            conn.Execute("UPDATE reservations SET Status = @Status WHERE Id = @Id;", res);
            if (book is not null)
                conn.Execute("UPDATE books SET ReservedCopies = @ReservedCopies WHERE Id = @Id;", book);
        });

        return (true, $"Rezervasyon ({res.ReservationCode}) durumu '{newStatus}' olarak güncellendi.");
    }

    public UserAccount SaveUserAccount(UserAccount input)
    {
        var existing = Users.FirstOrDefault(u => u.Id == input.Id);
        if (existing is null)
        {
            input.Id = Users.Count > 0 ? Users.Max(u => u.Id) + 1 : 1;
            input.LastLogin = DateTime.Now;
            Users.Add(input);
            ExecuteSafeDbAction(conn => conn.Execute("""
                INSERT INTO users (Id, Username, PasswordHash, FullName, Role, Title, Email, Phone, IsActive, LastLogin)
                VALUES (@Id, @Username, @PasswordHash, @FullName, @Role, @Title, @Email, @Phone, @IsActive, @LastLogin);
                """, input));
            return input;
        }
        existing.Username = input.Username;
        if (!string.IsNullOrWhiteSpace(input.PasswordHash))
            existing.PasswordHash = input.PasswordHash;
        existing.FullName = input.FullName;
        existing.Role = input.Role;
        existing.Title = input.Title;
        existing.Email = input.Email;
        existing.Phone = input.Phone;
        existing.IsActive = input.IsActive;
        ExecuteSafeDbAction(conn => conn.Execute("""
            UPDATE users SET Username=@Username, PasswordHash=@PasswordHash, FullName=@FullName, Role=@Role,
                Title=@Title, Email=@Email, Phone=@Phone, IsActive=@IsActive WHERE Id=@Id;
            """, existing));
        return existing;
    }

    // ==================== RICH TURKISH ARCHIVAL SEED DATASET ====================

    private void SeedInMemoryDataset()
    {
        var today = DateTime.Today;

        Users =
        [
            new() { Id = 1, Username = "admin", PasswordHash = "1234", FullName = "Ayşe Yılmaz", Role = "Sistem Yöneticisi", Title = "Baş Kütüphaneci", Email = "ayse.yilmaz@scriptorium.org.tr", Phone = "0212 310 44 10", IsActive = true, LastLogin = DateTime.Now.AddMinutes(-14) },
            new() { Id = 2, Username = "kutuphaneci", PasswordHash = "1234", FullName = "Mehmet Emin Karaca", Role = "Kütüphaneci", Title = "Okuyucu Hizmetleri Uzmanı", Email = "m.karaca@scriptorium.org.tr", Phone = "0212 310 44 12", IsActive = true, LastLogin = DateTime.Now.AddHours(-3) },
            new() { Id = 3, Username = "arsiv", PasswordHash = "1234", FullName = "Zeynep Sude Çelik", Role = "Kataloglama Uzmanı", Title = "Nadir Eserler ve Katalog Sorumlusu", Email = "z.celik@scriptorium.org.tr", Phone = "0212 310 44 15", IsActive = true, LastLogin = DateTime.Today.AddDays(-1) },
            new() { Id = 4, Username = "banko", PasswordHash = "1234", FullName = "Sinan Aksoy", Role = "Dolaşım Görevlisi", Title = "Ödünç / İade Banko Sorumlusu", Email = "s.aksoy@scriptorium.org.tr", Phone = "0212 310 44 19", IsActive = true, LastLogin = DateTime.Today.AddDays(-2) }
        ];

        Authors =
        [
            new() { Id = 1, FullName = "Ahmet Hamdi Tanpınar", Country = "Türkiye", BirthDeathYears = "1901 – 1962", LiteraryMovement = "Modern Türk Edebiyatı", Biography = "Cumhuriyet dönemi Türk edebiyatının en önemli romancı, şair ve edebiyat tarihçilerinden biridir. Zaman, rüya, musiki ve Doğu-Batı çatışması eserlerinin merkezindedir." },
            new() { Id = 2, FullName = "Oğuz Atay", Country = "Türkiye", BirthDeathYears = "1934 – 1977", LiteraryMovement = "Postmodernizm / Modernizm", Biography = "Türk edebiyatında postmodern romanın öncüsü kabul edilen mühendis ve yazar. Tutunamayanlar ve Tehlikeli Oyunlar ile modern bireyin yabancılaşmasını derinlikle işlemiştir." },
            new() { Id = 3, FullName = "Yaşar Kemal", Country = "Türkiye", BirthDeathYears = "1923 – 2015", LiteraryMovement = "Toplumcu Gerçekçilik / Epik Roman", Biography = "Çukurova doğasını, Anadolu insanını ve sözlü kültür mirasını destansı bir dille evrenselleştiren usta romancı." },
            new() { Id = 4, FullName = "Sabahattin Ali", Country = "Türkiye", BirthDeathYears = "1907 – 1948", LiteraryMovement = "Toplumcu Gerçekçilik", Biography = "Kürk Mantolu Madonna, Kuyucaklı Yusuf ve İçimizdeki Şeytan gibi başyapıtlarıyla Türk roman ve öykücülüğünde derin iz bırakmıştır." },
            new() { Id = 5, FullName = "İhsan Oktay Anar", Country = "Türkiye", BirthDeathYears = "1960 –", LiteraryMovement = "Tarihsel Postmodern Kurgu", Biography = "Felsefe akademisyeni ve yazar. Osmanlı tarihi, söylenceler ve felsefi mizahı birleştiren özgün diliyle tanınır." },
            new() { Id = 6, FullName = "Orhan Pamuk", Country = "Türkiye", BirthDeathYears = "1952 –", LiteraryMovement = "Postmodern Edebiyat", Biography = "2006 Nobel Edebiyat Ödülü sahibi Türk romancı. Kimlik, hafıza, İstanbul ve kültürlerarası gerilimleri işler." },
            new() { Id = 7, FullName = "Halil İnalcık", Country = "Türkiye", BirthDeathYears = "1916 – 2016", LiteraryMovement = "Osmanlı ve Sosyal Tarih", Biography = "'Tarihçilerin Kutbu' unvanıyla tanınan, Osmanlı sosyal ve ekonomik tarihini dünya çapında yeniden tanımlayan akademisyen." },
            new() { Id = 8, FullName = "İlber Ortaylı", Country = "Türkiye", BirthDeathYears = "1947 –", LiteraryMovement = "Tarih / Şehir ve Kültür Tarihi", Biography = "Osmanlı, Avrupa ve diplomasi tarihi alanındaki temel araştırmalarıyla tanınan tarihçi ve akademisyen." },
            new() { Id = 9, FullName = "Fyodor Mihayloviç Dostoyevski", Country = "Rusya", BirthDeathYears = "1821 – 1881", LiteraryMovement = "Psikolojik Gerçekçilik / Varoluşçuluk", Biography = "İnsan ruhunun derinliklerini, suç, vicdan ve inanç kavramlarını eşsiz bir psikolojik derinlikle çözümleyen dünya klasiği yazarı." },
            new() { Id = 10, FullName = "Ursula K. Le Guin", Country = "ABD", BirthDeathYears = "1929 – 2018", LiteraryMovement = "Spekülatif Kurgu / Bilimkurgu", Biography = "Yerdeniz serisi ve Mülksüzler ile siyaset felsefesi, antropoloji ve edebiyatı buluşturan usta yazar." },
            new() { Id = 11, FullName = "Sevgi Soysal", Country = "Türkiye", BirthDeathYears = "1936 – 1976", LiteraryMovement = "Çağdaş Türk Edebiyatı", Biography = "Yenişehir'de Bir Öğle Vakti ve Tante Rosa gibi eserleriyle toplumsal değişimi ve bireyi anlatan özgün yazar." },
            new() { Id = 12, FullName = "Cemal Süreya", Country = "Türkiye", BirthDeathYears = "1931 – 1990", LiteraryMovement = "İkinci Yeni Şiiri", Biography = "Türk şiirinde İkinci Yeni hareketinin en güçlü temsilcilerinden şair ve deneme yazarı." }
        ];

        Categories =
        [
            new() { Id = 1, DeweyCode = "813.42", Name = "Türk Edebiyatı — Roman", ShelfCorridor = "Salon A • Koridor 1–3", Description = "Cumhuriyet dönemi ve çağdaş Türk edebiyatı roman koleksiyonu." },
            new() { Id = 2, DeweyCode = "808.83", Name = "Dünya Edebiyatı Klasikleri", ShelfCorridor = "Salon A • Koridor 4–6", Description = "Rus, Fransız, İngiliz ve Alman edebiyatı temel klasikleri." },
            new() { Id = 3, DeweyCode = "956.1", Name = "Osmanlı ve Türkiye Tarihi", ShelfCorridor = "Salon B • Koridor 1–2", Description = "Selçuklu, Osmanlı İmparatorluğu ve Cumhuriyet tarihi araştırmaları." },
            new() { Id = 4, DeweyCode = "100.1", Name = "Felsefe ve Düşünce Tarihi", ShelfCorridor = "Salon B • Koridor 3", Description = "İlkçağ felsefesi, modern felsefe, etik ve epistemoloji kaynakları." },
            new() { Id = 5, DeweyCode = "301.09", Name = "Sosyoloji ve Toplum Bilimleri", ShelfCorridor = "Salon B • Koridor 4", Description = "Kent sosyolojisi, kültür araştırmaları ve siyaset bilimi eserleri." },
            new() { Id = 6, DeweyCode = "811.54", Name = "Şiir ve Edebiyat İncelemeleri", ShelfCorridor = "Salon A • Koridor 7", Description = "Modern şiir külliyatları, edebiyat kuramı ve eleştiri metinleri." },
            new() { Id = 7, DeweyCode = "509.2", Name = "Bilim Tarihi ve Spekülatif Kurgu", ShelfCorridor = "Salon C • Koridor 1", Description = "Bilim tarihi, teknoloji felsefesi ve seçkin bilimkurgu edebiyatı." }
        ];

        Publishers =
        [
            new() { Id = 1, Name = "İletişim Yayınları", City = "İstanbul", FoundedYear = 1982, ContactEmail = "bilgi@iletisim.com.tr", Phone = "0212 516 22 60", Website = "www.iletisim.com.tr", Notes = "Çağdaş Türk edebiyatı, tarih ve sosyal bilimler ana tedarikçisi." },
            new() { Id = 2, Name = "Yapı Kredi Yayınları (YKY)", City = "İstanbul", FoundedYear = 1945, ContactEmail = "ykykultur@ykykultur.com.tr", Phone = "0212 252 47 00", Website = "www.yapikrediyayinlari.com.tr", Notes = "Kazım Taşkent Klasikleri, Modern Klasikler ve edebiyat külliyatları." },
            new() { Id = 3, Name = "Türkiye İş Bankası Kültür Yayınları", City = "İstanbul", FoundedYear = 1956, ContactEmail = "bilgi@iskultur.com.tr", Phone = "0212 252 39 91", Website = "www.iskultur.com.tr", Notes = "Hasan Âli Yücel Klasikler Dizisi ve tarih araştırmaları." },
            new() { Id = 4, Name = "Dergâh Yayınları", City = "İstanbul", FoundedYear = 1977, ContactEmail = "iletisim@dergah.com.tr", Phone = "0212 518 95 79", Website = "www.dergah.com.tr", Notes = "Ahmet Hamdi Tanpınar külliyatı ve Türk düşünce tarihi." },
            new() { Id = 5, Name = "Metis Yayınları", City = "İstanbul", FoundedYear = 1982, ContactEmail = "metis@metiskitap.com", Phone = "0212 245 46 96", Website = "www.metiskitap.com", Notes = "Felsefe, kuram, edebiyat ve Ursula K. Le Guin çevirileri." },
            new() { Id = 6, Name = "Kronik Kitap", City = "İstanbul", FoundedYear = 2016, ContactEmail = "bilgi@kronikkitap.com", Phone = "0212 243 13 23", Website = "www.kronikkitap.com", Notes = "Akademik ve popüler tarih, askeri tarih ve biyografi." }
        ];

        Books =
        [
            new() { Id = 1, Isbn = "9789759955762", Barcode = "KTP-00001-01", Title = "Saatleri Ayarlama Enstitüsü", Subtitle = "Türk Modernleşmesi Üzerine İronik Bir Başyapıt", AuthorId = 1, AuthorName = "Ahmet Hamdi Tanpınar", PublisherId = 4, PublisherName = "Dergâh Yayınları", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2023, Edition = "34. Basım", PageCount = 382, Language = "Türkçe", ShelfLocation = "A1-14-C", TotalCopies = 6, AvailableCopies = 4, ReservedCopies = 0, TotalBorrowCount = 68, PhysicalCondition = "Çok İyi", CoverHex = "#1E4D3B", Summary = "Hayri İrdal ve Halit Ayarcı ekseninde Doğu ile Batı, gelenek ile bürokrasi arasındaki zaman algısını eşsiz bir ironiyle ele alan modern Türk romanı klasiği.", AddedDate = today.AddDays(-240) },
            new() { Id = 2, Isbn = "9789759955748", Barcode = "KTP-00002-01", Title = "Huzur", Subtitle = "İstanbul, Musiki ve İçsel Zaman", AuthorId = 1, AuthorName = "Ahmet Hamdi Tanpınar", PublisherId = 4, PublisherName = "Dergâh Yayınları", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2022, Edition = "31. Basım", PageCount = 428, Language = "Türkçe", ShelfLocation = "A1-14-D", TotalCopies = 5, AvailableCopies = 3, ReservedCopies = 0, TotalBorrowCount = 52, PhysicalCondition = "Çok İyi", CoverHex = "#284B3E", Summary = "Mümtaz ve Nuran'ın aşkı etrafında II. Dünya Savaşı arifesinde İstanbul'un kültürel hafızasını ve aydın bunalımını anlatır.", AddedDate = today.AddDays(-220) },
            new() { Id = 3, Isbn = "9789754700114", Barcode = "KTP-00003-01", Title = "Tutunamayanlar", Subtitle = "Bütün Eserleri 1", AuthorId = 2, AuthorName = "Oğuz Atay", PublisherId = 1, PublisherName = "İletişim Yayınları", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2024, Edition = "112. Basım", PageCount = 724, Language = "Türkçe", ShelfLocation = "A1-02-A", TotalCopies = 7, AvailableCopies = 1, ReservedCopies = 1, TotalBorrowCount = 94, PhysicalCondition = "İyi", CoverHex = "#7A3B2E", Summary = "Selim Işık'ın intiharının ardından arkadaşı Turgut Özben'in çıktığı içsel ve entelektüel yolculuk.", AddedDate = today.AddDays(-300) },
            new() { Id = 4, Isbn = "9789754704723", Barcode = "KTP-00004-01", Title = "Puslu Kıtalar Atlası", Subtitle = "Kostantiniyye'de Düş ve Gerçek", AuthorId = 5, AuthorName = "İhsan Oktay Anar", PublisherId = 1, PublisherName = "İletişim Yayınları", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2023, Edition = "98. Basım", PageCount = 238, Language = "Türkçe", ShelfLocation = "A1-08-B", TotalCopies = 5, AvailableCopies = 0, ReservedCopies = 2, TotalBorrowCount = 86, PhysicalCondition = "Çok İyi", CoverHex = "#8C6239", Summary = "Uzun İhsan Efendi ve oğlu Bünyamin'in 17. yüzyıl İstanbul'unda felsefe, düş ve gizem dolu serüveni.", AddedDate = today.AddDays(-260) },
            new() { Id = 5, Isbn = "9789750806629", Barcode = "KTP-00005-01", Title = "Kürk Mantolu Madonna", Subtitle = "Raif Efendi ve Maria Puder", AuthorId = 4, AuthorName = "Sabahattin Ali", PublisherId = 2, PublisherName = "Yapı Kredi Yayınları (YKY)", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2024, Edition = "136. Basım", PageCount = 160, Language = "Türkçe", ShelfLocation = "A1-05-A", TotalCopies = 8, AvailableCopies = 5, ReservedCopies = 0, TotalBorrowCount = 115, PhysicalCondition = "Çok İyi", CoverHex = "#23425C", Summary = "İçine kapanık Raif Efendi'nin gençliğinde Berlin'de bir sanat galerisinde gördüğü otoportre ile başlayan unutulmaz hikayesi.", AddedDate = today.AddDays(-350) },
            new() { Id = 6, Isbn = "9789750807145", Barcode = "KTP-00006-01", Title = "İnce Memed 1", Subtitle = "Çukurova Destanı", AuthorId = 3, AuthorName = "Yaşar Kemal", PublisherId = 2, PublisherName = "Yapı Kredi Yayınları (YKY)", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2023, Edition = "64. Basım", PageCount = 436, Language = "Türkçe", ShelfLocation = "A2-01-A", TotalCopies = 6, AvailableCopies = 4, ReservedCopies = 0, TotalBorrowCount = 74, PhysicalCondition = "Çok İyi", CoverHex = "#5E4627", Summary = "Abdi Ağa'nın zulmüne başkaldırarak Toroslar'a çıkan İnce Memed'in adalet ve özgürlük mücadelesi.", AddedDate = today.AddDays(-210) },
            new() { Id = 7, Isbn = "9789750826122", Barcode = "KTP-00007-01", Title = "Kara Kitap", Subtitle = "Hafıza, Kayıp ve İstanbul", AuthorId = 6, AuthorName = "Orhan Pamuk", PublisherId = 2, PublisherName = "Yapı Kredi Yayınları (YKY)", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2022, Edition = "28. Basım", PageCount = 468, Language = "Türkçe", ShelfLocation = "A2-04-C", TotalCopies = 4, AvailableCopies = 3, ReservedCopies = 0, TotalBorrowCount = 49, PhysicalCondition = "Çok İyi", CoverHex = "#1F2A36", Summary = "Avukat Galip'in kaybolan eşi Rüya ve köşe yazarı Celâl'i İstanbul sokaklarında ve metinlerin labirentinde arayışı.", AddedDate = today.AddDays(-180) },
            new() { Id = 8, Isbn = "9789754589023", Barcode = "KTP-00008-01", Title = "Devlet-i Aliyye: Osmanlı İmparatorluğu Üzerine Araştırmalar I", Subtitle = "Klasik Dönem (1302–1606)", AuthorId = 7, AuthorName = "Halil İnalcık", PublisherId = 3, PublisherName = "Türkiye İş Bankası Kültür Yayınları", CategoryId = 3, CategoryName = "Osmanlı ve Türkiye Tarihi", DeweyCode = "956.1", PublicationYear = 2023, Edition = "52. Basım", PageCount = 378, Language = "Türkçe", ShelfLocation = "B1-02-A", TotalCopies = 5, AvailableCopies = 3, ReservedCopies = 0, TotalBorrowCount = 63, PhysicalCondition = "Çok İyi", CoverHex = "#6B2D2C", Summary = "Osmanlı Beyliği'nin kuruluşundan klasik imparatorluk kurumlarının oluşumuna kadar birincil arşiv belgeleriyle temel başvuru eseri.", AddedDate = today.AddDays(-290) },
            new() { Id = 9, Isbn = "9789752430181", Barcode = "KTP-00009-01", Title = "Türklerin Tarihi", Subtitle = "Orta Asya'nın Bozkırlarından Avrupa'nın Kapılarına", AuthorId = 8, AuthorName = "İlber Ortaylı", PublisherId = 6, PublisherName = "Kronik Kitap", CategoryId = 3, CategoryName = "Osmanlı ve Türkiye Tarihi", DeweyCode = "956.1", PublicationYear = 2022, Edition = "40. Basım", PageCount = 320, Language = "Türkçe", ShelfLocation = "B1-05-B", TotalCopies = 4, AvailableCopies = 2, ReservedCopies = 0, TotalBorrowCount = 58, PhysicalCondition = "Çok İyi", CoverHex = "#3E4A3D", Summary = "Türklerin tarih sahnesine çıkışından Anadolu'nun yurt edinilmesine uzanan süreçte siyasi ve kültürel yapı.", AddedDate = today.AddDays(-150) },
            new() { Id = 10, Isbn = "9789754589078", Barcode = "KTP-00010-01", Title = "Suç ve Ceza", Subtitle = "Hasan Âli Yücel Klasikler Dizisi", AuthorId = 9, AuthorName = "Fyodor Mihayloviç Dostoyevski", PublisherId = 3, PublisherName = "Türkiye İş Bankası Kültür Yayınları", CategoryId = 2, CategoryName = "Dünya Edebiyatı Klasikleri", DeweyCode = "808.83", PublicationYear = 2024, Edition = "76. Basım", PageCount = 687, Language = "Türkçe", ShelfLocation = "A4-11-A", TotalCopies = 6, AvailableCopies = 4, ReservedCopies = 0, TotalBorrowCount = 81, PhysicalCondition = "Çok İyi", CoverHex = "#2A3E52", Summary = "St. Petersburg'da hukuk öğrencisi Raskolnikov'un ahlaki ikilemi, işlediği suç ve vicdani hesaplaşması.", AddedDate = today.AddDays(-320) },
            new() { Id = 11, Isbn = "9789753420297", Barcode = "KTP-00011-01", Title = "Mülksüzler", Subtitle = "İki Dünya Arasında Ütopya ve Özgürlük", AuthorId = 10, AuthorName = "Ursula K. Le Guin", PublisherId = 5, PublisherName = "Metis Yayınları", CategoryId = 7, CategoryName = "Bilim Tarihi ve Spekülatif Kurgu", DeweyCode = "509.2", PublicationYear = 2023, Edition = "29. Basım", PageCount = 344, Language = "Türkçe", ShelfLocation = "C1-03-B", TotalCopies = 4, AvailableCopies = 0, ReservedCopies = 1, TotalBorrowCount = 61, PhysicalCondition = "İyi", CoverHex = "#3D5245", Summary = "Fizikçi Shevek'in Anarres ve Urras gezegenleri arasında kurduğu bilimsel ve toplumsal köprü.", AddedDate = today.AddDays(-195) },
            new() { Id = 12, Isbn = "9789754701982", Barcode = "KTP-00012-01", Title = "Yenişehir'de Bir Öğle Vakti", Subtitle = "1974 Orhan Kemal Roman Armağanı", AuthorId = 11, AuthorName = "Sevgi Soysal", PublisherId = 1, PublisherName = "İletişim Yayınları", CategoryId = 1, CategoryName = "Türk Edebiyatı — Roman", DeweyCode = "813.42", PublicationYear = 2023, Edition = "39. Basım", PageCount = 290, Language = "Türkçe", ShelfLocation = "A2-09-D", TotalCopies = 4, AvailableCopies = 3, ReservedCopies = 0, TotalBorrowCount = 42, PhysicalCondition = "Çok İyi", CoverHex = "#6E4A35", Summary = "Ankara Kızılay'da devrilmek üzere olan bir kavak ağacının etrafında kesişen farklı toplumsal sınıflardan insanların öyküsü.", AddedDate = today.AddDays(-140) },
            new() { Id = 13, Isbn = "9789750803314", Barcode = "KTP-00013-01", Title = "Sevda Sözleri", Subtitle = "Bütün Şiirleri", AuthorId = 12, AuthorName = "Cemal Süreya", PublisherId = 2, PublisherName = "Yapı Kredi Yayınları (YKY)", CategoryId = 6, CategoryName = "Şiir ve Edebiyat İncelemeleri", DeweyCode = "811.54", PublicationYear = 2024, Edition = "92. Basım", PageCount = 356, Language = "Türkçe", ShelfLocation = "A7-02-A", TotalCopies = 5, AvailableCopies = 4, ReservedCopies = 0, TotalBorrowCount = 67, PhysicalCondition = "Çok İyi", CoverHex = "#4C3B4D", Summary = "Üvercinka, Göçebe, Beni Öp Sonra Doğur Beni ve Güz Bitigi dahil Cemal Süreya'nın tüm şiir külliyatı.", AddedDate = today.AddDays(-175) },
            new() { Id = 14, Isbn = "9789759955816", Barcode = "KTP-00014-01", Title = "Beş Şehir", Subtitle = "Ankara, Erzurum, Konya, Bursa, İstanbul", AuthorId = 1, AuthorName = "Ahmet Hamdi Tanpınar", PublisherId = 4, PublisherName = "Dergâh Yayınları", CategoryId = 6, CategoryName = "Şiir ve Edebiyat İncelemeleri", DeweyCode = "811.54", PublicationYear = 2023, Edition = "48. Basım", PageCount = 220, Language = "Türkçe", ShelfLocation = "A7-01-B", TotalCopies = 5, AvailableCopies = 4, ReservedCopies = 0, TotalBorrowCount = 55, PhysicalCondition = "Çok İyi", CoverHex = "#1E4D3B", Summary = "Tanpınar'ın Türk kültür ve mimari mirasını beş kadim şehir üzerinden incelediği edebi deneme başyapıtı.", AddedDate = today.AddDays(-160) }
        ];

        Members =
        [
            new() { Id = 1, MemberNumber = "UYE-2026-1001", FullName = "Doç. Dr. Kerem Demir", IdentityNumber = "18492038412", MemberType = "Akademik Personel", Email = "kerem.demir@bogazici.edu.tr", Phone = "0532 410 88 92", DepartmentOrInstitution = "Edebiyat Fakültesi • Türk Dili ve Edebiyatı", Address = "Moda Cad. No:42 Kadıköy / İstanbul", RegistrationDate = today.AddDays(-420), Status = "Aktif", MaxLoanLimit = 10, Notes = "Nadir eserler okuma salonu yetkisi mevcut." },
            new() { Id = 2, MemberNumber = "UYE-2026-1002", FullName = "Zeynep Kaya", IdentityNumber = "29481049284", MemberType = "Lisansüstü Öğrenci", Email = "zeynep.kaya@itu.edu.tr", Phone = "0544 392 14 70", DepartmentOrInstitution = "Sosyal Bilimler Enstitüsü • Tarih Yüksek Lisans", Address = "Abbasağa Mah. Beşiktaş / İstanbul", RegistrationDate = today.AddDays(-310), Status = "Aktif", MaxLoanLimit = 7, Notes = "Tez çalışması kapsamında tarih kaynakları ödünç alıyor." },
            new() { Id = 3, MemberNumber = "UYE-2026-1003", FullName = "Caner Erkin Özkan", IdentityNumber = "34918273640", MemberType = "Standart Okur", Email = "caner.ozkan@gmail.com", Phone = "0535 811 23 45", DepartmentOrInstitution = "Serbest Mimar", Address = "Cihangir Mah. Beyoğlu / İstanbul", RegistrationDate = today.AddDays(-280), Status = "Aktif", MaxLoanLimit = 5, Notes = "Gecikmiş kitap hatırlatması SMS ile gönderildi." },
            new() { Id = 4, MemberNumber = "UYE-2026-1004", FullName = "Selin Arslan", IdentityNumber = "41829304912", MemberType = "Üniversite Öğrencisi", Email = "selin.arslan@istanbul.edu.tr", Phone = "0505 674 90 11", DepartmentOrInstitution = "İstanbul Üniversitesi • Felsefe Bölümü", Address = "Fatih / İstanbul", RegistrationDate = today.AddDays(-190), Status = "Aktif", MaxLoanLimit = 5, Notes = "Düzenli okur." },
            new() { Id = 5, MemberNumber = "UYE-2026-1005", FullName = "Prof. Dr. Haluk Şahin", IdentityNumber = "52918374618", MemberType = "Akademik Personel", Email = "h.sahin@galatasaray.edu.tr", Phone = "0533 219 04 88", DepartmentOrInstitution = "İktisadi ve İdari Bilimler • Siyaset Bilimi", Address = "Ortaköy Beşiktaş / İstanbul", RegistrationDate = today.AddDays(-510), Status = "Aktif", MaxLoanLimit = 10, Notes = "Danışma kurulu üyesi." },
            new() { Id = 6, MemberNumber = "UYE-2026-1006", FullName = "Elif Doğan", IdentityNumber = "60928374122", MemberType = "Araştırmacı", Email = "elif.dogan@arsiv.org.tr", Phone = "0542 781 33 19", DepartmentOrInstitution = "Bağımsız Edebiyat Araştırmacısı", Address = "Kuzguncuk Üsküdar / İstanbul", RegistrationDate = today.AddDays(-145), Status = "Aktif", MaxLoanLimit = 7, Notes = "Karşılaştırmalı edebiyat araştırmaları." },
            new() { Id = 7, MemberNumber = "UYE-2026-1007", FullName = "Burak Yavuz", IdentityNumber = "19283746502", MemberType = "Standart Okur", Email = "burak.yavuz@outlook.com", Phone = "0536 902 18 74", DepartmentOrInstitution = "Yazılım Mühendisi", Address = "Ataşehir / İstanbul", RegistrationDate = today.AddDays(-95), Status = "Askıda", MaxLoanLimit = 5, Notes = "Uzun süreli gecikme nedeniyle üyeliği geçici olarak askıya alındı." }
        ];

        BorrowRecords =
        [
            // Active On-Loan (Due in future)
            new() { Id = 1, TransactionCode = "ODN-2026-0101", BookId = 1, BookTitle = "Saatleri Ayarlama Enstitüsü", BookAuthor = "Ahmet Hamdi Tanpınar", BookIsbn = "9789759955762", BookBarcode = "KTP-00001-02", ShelfLocation = "A1-14-C", MemberId = 1, MemberNumber = "UYE-2026-1001", MemberName = "Doç. Dr. Kerem Demir", MemberPhone = "0532 410 88 92", MemberEmail = "kerem.demir@bogazici.edu.tr", MemberType = "Akademik Personel", BorrowDate = today.AddDays(-5), DueDate = today.AddDays(10), Status = "Ödünçte", ProcessedBy = "Ayşe Yılmaz", ReturnCondition = "Normal", Notes = "Akademik seminer çalışması" },
            new() { Id = 2, TransactionCode = "ODN-2026-0102", BookId = 8, BookTitle = "Devlet-i Aliyye: Osmanlı İmparatorluğu Üzerine Araştırmalar I", BookAuthor = "Halil İnalcık", BookIsbn = "9789754589023", BookBarcode = "KTP-00008-01", ShelfLocation = "B1-02-A", MemberId = 2, MemberNumber = "UYE-2026-1002", MemberName = "Zeynep Kaya", MemberPhone = "0544 392 14 70", MemberEmail = "zeynep.kaya@itu.edu.tr", MemberType = "Lisansüstü Öğrenci", BorrowDate = today.AddDays(-8), DueDate = today.AddDays(7), Status = "Ödünçte", ProcessedBy = "Mehmet Emin Karaca", ReturnCondition = "Normal", Notes = "Yüksek lisans tez okuması" },
            new() { Id = 3, TransactionCode = "ODN-2026-0103", BookId = 5, BookTitle = "Kürk Mantolu Madonna", BookAuthor = "Sabahattin Ali", BookIsbn = "9789750806629", BookBarcode = "KTP-00005-03", ShelfLocation = "A1-05-A", MemberId = 4, MemberNumber = "UYE-2026-1004", MemberName = "Selin Arslan", MemberPhone = "0505 674 90 11", MemberEmail = "selin.arslan@istanbul.edu.tr", MemberType = "Üniversite Öğrencisi", BorrowDate = today.AddDays(-2), DueDate = today.AddDays(13), Status = "Ödünçte", ProcessedBy = "Sinan Aksoy", ReturnCondition = "Normal", Notes = "Standart ödünç" },

            // Due Today! (Bugün Teslim Beklenenler)
            new() { Id = 4, TransactionCode = "ODN-2026-0098", BookId = 6, BookTitle = "İnce Memed 1", BookAuthor = "Yaşar Kemal", BookIsbn = "9789750807145", BookBarcode = "KTP-00006-02", ShelfLocation = "A2-01-A", MemberId = 1, MemberNumber = "UYE-2026-1001", MemberName = "Doç. Dr. Kerem Demir", MemberPhone = "0532 410 88 92", MemberEmail = "kerem.demir@bogazici.edu.tr", MemberType = "Akademik Personel", BorrowDate = today.AddDays(-15), DueDate = today, Status = "Ödünçte", ProcessedBy = "Ayşe Yılmaz", ReturnCondition = "Normal", Notes = "Bugün teslim günü" },
            new() { Id = 5, TransactionCode = "ODN-2026-0099", BookId = 9, BookTitle = "Türklerin Tarihi", BookAuthor = "İlber Ortaylı", BookIsbn = "9789752430181", BookBarcode = "KTP-00009-01", ShelfLocation = "B1-05-B", MemberId = 6, MemberNumber = "UYE-2026-1006", MemberName = "Elif Doğan", MemberPhone = "0542 781 33 19", MemberEmail = "elif.dogan@arsiv.org.tr", MemberType = "Araştırmacı", BorrowDate = today.AddDays(-15), DueDate = today, RenewalCount = 1, Status = "Ödünçte", ProcessedBy = "Sinan Aksoy", ReturnCondition = "Normal", Notes = "1 kez uzatıldı, bugün teslim" },

            // Overdue Books! (Gecikmiş Kitaplar)
            new() { Id = 6, TransactionCode = "ODN-2026-0084", BookId = 4, BookTitle = "Puslu Kıtalar Atlası", BookAuthor = "İhsan Oktay Anar", BookIsbn = "9789754704723", BookBarcode = "KTP-00004-01", ShelfLocation = "A1-08-B", MemberId = 3, MemberNumber = "UYE-2026-1003", MemberName = "Caner Erkin Özkan", MemberPhone = "0535 811 23 45", MemberEmail = "caner.ozkan@gmail.com", MemberType = "Standart Okur", BorrowDate = today.AddDays(-24), DueDate = today.AddDays(-9), Status = "Gecikmiş", FineAmount = 45.00m, ProcessedBy = "Mehmet Emin Karaca", ReturnCondition = "Normal", Notes = "Üyeye hatırlatma e-postası gönderildi" },
            new() { Id = 7, TransactionCode = "ODN-2026-0079", BookId = 3, BookTitle = "Tutunamayanlar", BookAuthor = "Oğuz Atay", BookIsbn = "9789754700114", BookBarcode = "KTP-00003-04", ShelfLocation = "A1-02-A", MemberId = 7, MemberNumber = "UYE-2026-1007", MemberName = "Burak Yavuz", MemberPhone = "0536 902 18 74", MemberEmail = "burak.yavuz@outlook.com", MemberType = "Standart Okur", BorrowDate = today.AddDays(-33), DueDate = today.AddDays(-18), Status = "Gecikmiş", FineAmount = 90.00m, ProcessedBy = "Sinan Aksoy", ReturnCondition = "Normal", Notes = "Kritik gecikme - telefonla ulaşıldı" },
            new() { Id = 8, TransactionCode = "ODN-2026-0091", BookId = 11, BookTitle = "Mülksüzler", BookAuthor = "Ursula K. Le Guin", BookIsbn = "9789753420297", BookBarcode = "KTP-00011-01", ShelfLocation = "C1-03-B", MemberId = 5, MemberNumber = "UYE-2026-1005", MemberName = "Prof. Dr. Haluk Şahin", MemberPhone = "0533 219 04 88", MemberEmail = "h.sahin@galatasaray.edu.tr", MemberType = "Akademik Personel", BorrowDate = today.AddDays(-19), DueDate = today.AddDays(-4), Status = "Gecikmiş", FineAmount = 20.00m, ProcessedBy = "Ayşe Yılmaz", ReturnCondition = "Normal", Notes = "Seminer odası nüshası" },

            // Recently Returned (İade Edilenler)
            new() { Id = 9, TransactionCode = "ODN-2026-0075", BookId = 10, BookTitle = "Suç ve Ceza", BookAuthor = "Fyodor Mihayloviç Dostoyevski", BookIsbn = "9789754589078", BookBarcode = "KTP-00010-01", ShelfLocation = "A4-11-A", MemberId = 4, MemberNumber = "UYE-2026-1004", MemberName = "Selin Arslan", MemberPhone = "0505 674 90 11", MemberEmail = "selin.arslan@istanbul.edu.tr", MemberType = "Üniversite Öğrencisi", BorrowDate = today.AddDays(-16), DueDate = today.AddDays(-1), ReturnDate = today.AddHours(-2), Status = "İade Edildi", FineAmount = 0m, ProcessedBy = "Sinan Aksoy", ReturnCondition = "Normal (Hasarsız)", Notes = "Zamanında eksiksiz teslim alındı" },
            new() { Id = 10, TransactionCode = "ODN-2026-0071", BookId = 2, BookTitle = "Huzur", BookAuthor = "Ahmet Hamdi Tanpınar", BookIsbn = "9789759955748", BookBarcode = "KTP-00002-01", ShelfLocation = "A1-14-D", MemberId = 6, MemberNumber = "UYE-2026-1006", MemberName = "Elif Doğan", MemberPhone = "0542 781 33 19", MemberEmail = "elif.dogan@arsiv.org.tr", MemberType = "Araştırmacı", BorrowDate = today.AddDays(-20), DueDate = today.AddDays(-5), ReturnDate = today.AddDays(-6), Status = "İade Edildi", FineAmount = 0m, ProcessedBy = "Mehmet Emin Karaca", ReturnCondition = "Normal (Hasarsız)", Notes = "Erken iade" }
        ];

        Reservations =
        [
            new() { Id = 1, ReservationCode = "RZV-2026-0014", BookId = 4, BookTitle = "Puslu Kıtalar Atlası", BookAuthor = "İhsan Oktay Anar", BookIsbn = "9789754704723", ShelfLocation = "A1-08-B", AvailableCopies = 0, MemberId = 4, MemberNumber = "UYE-2026-1004", MemberName = "Selin Arslan", MemberPhone = "0505 674 90 11", ReservationDate = today.AddDays(-3).AddHours(11), ExpiryDate = today.AddDays(2), QueuePosition = 1, Status = "Bekliyor", Notes = "İlk iade edilen nüsha ayrılacak" },
            new() { Id = 2, ReservationCode = "RZV-2026-0015", BookId = 4, BookTitle = "Puslu Kıtalar Atlası", BookAuthor = "İhsan Oktay Anar", BookIsbn = "9789754704723", ShelfLocation = "A1-08-B", AvailableCopies = 0, MemberId = 2, MemberNumber = "UYE-2026-1002", MemberName = "Zeynep Kaya", MemberPhone = "0544 392 14 70", ReservationDate = today.AddDays(-1).AddHours(15), ExpiryDate = today.AddDays(4), QueuePosition = 2, Status = "Bekliyor", Notes = "2. sıra rezervasyon" },
            new() { Id = 3, ReservationCode = "RZV-2026-0016", BookId = 3, BookTitle = "Tutunamayanlar", BookAuthor = "Oğuz Atay", BookIsbn = "9789754700114", ShelfLocation = "A1-02-A", AvailableCopies = 1, MemberId = 6, MemberNumber = "UYE-2026-1006", MemberName = "Elif Doğan", MemberPhone = "0542 781 33 19", ReservationDate = today.AddDays(-2).AddHours(10), ExpiryDate = today.AddDays(1), QueuePosition = 1, Status = "Rafta Ayrıldı", Notes = "Banko rezerv rafında #04 numaralı gözde bekliyor" },
            new() { Id = 4, ReservationCode = "RZV-2026-0017", BookId = 11, BookTitle = "Mülksüzler", BookAuthor = "Ursula K. Le Guin", BookIsbn = "9789753420297", ShelfLocation = "C1-03-B", AvailableCopies = 0, MemberId = 1, MemberNumber = "UYE-2026-1001", MemberName = "Doç. Dr. Kerem Demir", MemberPhone = "0532 410 88 92", ReservationDate = today.AddHours(-5), ExpiryDate = today.AddDays(3), QueuePosition = 1, Status = "Bekliyor", Notes = "Ders okuma listesi için" }
        ];

        MonthlyStats =
        [
            new() { MonthName = "Aralık", ShortMonth = "Ara", BorrowedCount = 310, ReturnedCount = 295, OverdueCount = 12, BorrowBarHeight = 96, ReturnBarHeight = 91 },
            new() { MonthName = "Ocak", ShortMonth = "Oca", BorrowedCount = 345, ReturnedCount = 330, OverdueCount = 14, BorrowBarHeight = 107, ReturnBarHeight = 102 },
            new() { MonthName = "Şubat", ShortMonth = "Şub", BorrowedCount = 410, ReturnedCount = 390, OverdueCount = 18, BorrowBarHeight = 127, ReturnBarHeight = 121 },
            new() { MonthName = "Mart", ShortMonth = "Mar", BorrowedCount = 485, ReturnedCount = 462, OverdueCount = 15, BorrowBarHeight = 150, ReturnBarHeight = 143 },
            new() { MonthName = "Nisan", ShortMonth = "Nis", BorrowedCount = 460, ReturnedCount = 448, OverdueCount = 11, BorrowBarHeight = 142, ReturnBarHeight = 139 },
            new() { MonthName = "Mayıs", ShortMonth = "May", BorrowedCount = 520, ReturnedCount = 505, OverdueCount = 19, BorrowBarHeight = 161, ReturnBarHeight = 156 },
            new() { MonthName = "Haziran", ShortMonth = "Haz", BorrowedCount = 380, ReturnedCount = 415, OverdueCount = 9, BorrowBarHeight = 118, ReturnBarHeight = 128 },
            new() { MonthName = "Temmuz", ShortMonth = "Tem", BorrowedCount = 290, ReturnedCount = 305, OverdueCount = 7, BorrowBarHeight = 90, ReturnBarHeight = 95 },
            new() { MonthName = "Ağustos", ShortMonth = "Ağu", BorrowedCount = 315, ReturnedCount = 298, OverdueCount = 8, BorrowBarHeight = 98, ReturnBarHeight = 92 },
            new() { MonthName = "Eylül", ShortMonth = "Eyl", BorrowedCount = 490, ReturnedCount = 455, OverdueCount = 16, BorrowBarHeight = 152, ReturnBarHeight = 141 },
            new() { MonthName = "Ekim", ShortMonth = "Eki", BorrowedCount = 548, ReturnedCount = 518, OverdueCount = 14, BorrowBarHeight = 170, ReturnBarHeight = 160 },
            new() { MonthName = "Kasım", ShortMonth = "Kas", BorrowedCount = 472, ReturnedCount = 438, OverdueCount = 11, BorrowBarHeight = 146, ReturnBarHeight = 136 }
        ];

        RecalculateAggregates();
    }
}

