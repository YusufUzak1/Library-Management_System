using LibraryManagement.Application.Interfaces;
using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Infrastructure;

public sealed class LibraryDataService : ILibraryDataService
{
    private readonly List<Book> _books = new();
    private readonly List<Member> _members = new();
    private readonly List<ActiveLoanRecord> _loans = new();
    private readonly List<ReservationRecord> _reservations = new();
    private readonly List<ActivityLogEntry> _activities = new();
    private readonly List<UserAccount> _users = new();
    private readonly object _lock = new();
    private readonly MariaDbManager _mariaDb = new();

    public bool IsDatabaseConnected => _mariaDb.IsConnected;
    public string DatabaseStatusText => _mariaDb.StatusMessage;

    public LibraryDataService()
    {
        LoadUsersFromLocalCache();
        if (_mariaDb.Initialize())
        {
            SyncFromDatabase();
        }
        else
        {
            SeedInitialData();
            _ = Task.Run(InitializeMariaDb);
        }
    }

    private void InitializeMariaDb()
    {
        if (_mariaDb.Initialize())
        {
            SyncFromDatabase();
        }
    }

    public bool SyncFromDatabase()
    {
        if (!_mariaDb.IsConnected) return false;
        try
        {
            var dbBooks = _mariaDb.LoadBooks();
            var dbMembers = _mariaDb.LoadMembers();
            var dbLoans = _mariaDb.LoadActiveLoans();
            var dbActs = _mariaDb.LoadActivityLogs();
            var dbRes = _mariaDb.LoadReservations();
            var dbUsers = _mariaDb.LoadUsers();

            lock (_lock)
            {
                if (dbBooks != null && dbBooks.Count > 0)
                {
                    _books.Clear();
                    _books.AddRange(dbBooks);
                }
                if (dbMembers != null && dbMembers.Count > 0)
                {
                    _members.Clear();
                    _members.AddRange(dbMembers);
                }
                if (dbLoans != null)
                {
                    _loans.Clear();
                    _loans.AddRange(dbLoans);
                }
                if (dbActs != null && dbActs.Count > 0)
                {
                    _activities.Clear();
                    _activities.AddRange(dbActs);
                }
                if (dbRes != null && dbRes.Count > 0)
                {
                    _reservations.Clear();
                    _reservations.AddRange(dbRes);
                }
                if (dbUsers != null && dbUsers.Count > 0)
                {
                    _users.Clear();
                    _users.AddRange(dbUsers);
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void SeedInitialData()
    {
        _books.AddRange(new[]
        {
            new Book
            {
                Id = 1,
                Title = "Dune",
                Subtitle = "Çöl Gezegeni",
                AuthorName = "Frank Herbert",
                Isbn = "9786053754794",
                PublisherName = "İthaki Yayınları",
                CategoryName = "Bilim Kurgu",
                Genre = "Bilim Kurgu",
                CategoryTheme = BookCategoryTheme.ScienceFiction,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1965,
                PageCount = 688,
                ShelfLocation = "BK-101-A",
                TotalCopies = 5,
                AvailableCopies = 4,
                PrimaryCoverHex = "#1D3550",
                AccentFoilHex = "#D9B76A",
                Synopsis = "Arrakis çöl gezegeninde baharat ve iktidar mücadelesi.",
                MonthlyBorrowCount = 116
            },
            new Book
            {
                Id = 2,
                Title = "Yüzüklerin Efendisi",
                Subtitle = "Yüzük Kardeşliği",
                AuthorName = "J. R. R. Tolkien",
                Isbn = "9789753421805",
                PublisherName = "Metis Yayınları",
                CategoryName = "Fantastik",
                Genre = "Fantastik Kurgu",
                CategoryTheme = BookCategoryTheme.Fantasy,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1954,
                PageCount = 1178,
                ShelfLocation = "FK-204-B",
                TotalCopies = 6,
                AvailableCopies = 5,
                PrimaryCoverHex = "#6B1F2A",
                AccentFoilHex = "#D9B76A",
                Synopsis = "Tek Yüzük ve Orta Dünya'nın kaderini belirleyen epik yolculuk.",
                MonthlyBorrowCount = 128
            },
            new Book
            {
                Id = 3,
                Title = "Savaş ve Barış",
                Subtitle = "Cilt 1-2",
                AuthorName = "Lev Tolstoy",
                Isbn = "9789750500139",
                PublisherName = "İletişim Yayıncılık",
                CategoryName = "Klasik",
                Genre = "Dünya Klasiği",
                CategoryTheme = BookCategoryTheme.ClassicLiterature,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1869,
                PageCount = 1225,
                ShelfLocation = "KL-302-A",
                TotalCopies = 4,
                AvailableCopies = 3,
                PrimaryCoverHex = "#1F5A3A",
                AccentFoilHex = "#E2C47E",
                Synopsis = "Napolyon dönemi Rusya'sında aristokrasi ve harp panoraması.",
                MonthlyBorrowCount = 74
            },
            new Book
            {
                Id = 4,
                Title = "Küçük Prens",
                Subtitle = "Özel Resimli Baskı",
                AuthorName = "Antoine de Saint-Exupéry",
                Isbn = "9789750724855",
                PublisherName = "Can Yayınları",
                CategoryName = "Çocuk",
                Genre = "Felsefi Masal",
                CategoryTheme = BookCategoryTheme.Childrens,
                PhysicalFormat = BookPhysicalFormat.PocketPaperback,
                PublicationYear = 1943,
                PageCount = 96,
                ShelfLocation = "CK-012-C",
                TotalCopies = 8,
                AvailableCopies = 7,
                PrimaryCoverHex = "#F1E6C8",
                AccentFoilHex = "#C97B2E",
                Synopsis = "Sahra çölünde pilotla buluşan küçük prensin unutulmaz hikayesi.",
                MonthlyBorrowCount = 89
            },
            new Book
            {
                Id = 5,
                Title = "Sefiller",
                Subtitle = "Tam Metin",
                AuthorName = "Victor Hugo",
                Isbn = "9789754580211",
                PublisherName = "Türkiye İş Bankası",
                CategoryName = "Klasik",
                Genre = "Tarihi Roman",
                CategoryTheme = BookCategoryTheme.ClassicLiterature,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1862,
                PageCount = 1463,
                ShelfLocation = "KL-305-B",
                TotalCopies = 4,
                AvailableCopies = 4,
                PrimaryCoverHex = "#3A2A20",
                AccentFoilHex = "#D9B76A",
                Synopsis = "Jean Valjean'ın adalet, kefaret ve merhamet arayışı.",
                MonthlyBorrowCount = 92
            },
            new Book
            {
                Id = 6,
                Title = "1984",
                Subtitle = "Distopya",
                AuthorName = "George Orwell",
                Isbn = "9789750718533",
                PublisherName = "Can Yayınları",
                CategoryName = "Bilim Kurgu",
                Genre = "Distopya",
                CategoryTheme = BookCategoryTheme.ScienceFiction,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1949,
                PageCount = 352,
                ShelfLocation = "BK-108-D",
                TotalCopies = 7,
                AvailableCopies = 6,
                PrimaryCoverHex = "#7A1E1E",
                AccentFoilHex = "#E9C46A",
                Synopsis = "Büyük Birader gözetiminde totaliter bir dünya düzeni.",
                MonthlyBorrowCount = 97
            },
            new Book
            {
                Id = 7,
                Title = "Simyacı",
                Subtitle = "Hazine Arayışı",
                AuthorName = "Paulo Coelho",
                Isbn = "9789750726439",
                PublisherName = "Can Yayınları",
                CategoryName = "Roman",
                Genre = "Çağdaş Roman",
                CategoryTheme = BookCategoryTheme.HistoryAndPhilosophy,
                PhysicalFormat = BookPhysicalFormat.StandardHardcover,
                PublicationYear = 1988,
                PageCount = 208,
                ShelfLocation = "RM-401-A",
                TotalCopies = 5,
                AvailableCopies = 4,
                PrimaryCoverHex = "#2E5E6E",
                AccentFoilHex = "#E9C46A",
                Synopsis = "Endülüslü çoban Santiago'nun Mısır piramitlerine uzanan yolculuğu.",
                MonthlyBorrowCount = 81
            }
        });

        _members.AddRange(new[]
        {
            new Member { Id = 1, MemberNumber = "FU-2026-1001", FullName = "Yusuf Uzak", Email = "yusuf@firat.edu.tr", PhoneNumber = "0555 111 2233", TierTitle = "Kütüphane Görevlisi" },
            new Member { Id = 2, MemberNumber = "FU-2026-1002", FullName = "Prof. Dr. Ahmet Kaya", Email = "ahmetkaya@firat.edu.tr", PhoneNumber = "0532 222 3344", TierTitle = "Akademik Personel" },
            new Member { Id = 3, MemberNumber = "FU-2026-1003", FullName = "Zeynep Demir", Email = "zeynep.demir@ogrenci.firat.edu.tr", PhoneNumber = "0543 333 4455", TierTitle = "Lisans Öğrencisi" },
            new Member { Id = 4, MemberNumber = "FU-2026-1004", FullName = "Mehmet Çelik", Email = "mcelik@firat.edu.tr", PhoneNumber = "0505 444 5566", TierTitle = "Araştırma Görevlisi" }
        });

        if (_users.Count == 0)
        {
            _users.AddRange(new[]
            {
                new UserAccount { Id = 1, Username = "admin", Password = "1234", FullName = "Sistem Yöneticisi", Role = "Admin", Email = "admin@kutuphane.edu.tr", MemberType = "Personel" },
                new UserAccount { Id = 2, Username = "kullanici", Password = "1234", FullName = "Örnek Öğrenci", Role = "User", Email = "ogrenci@kutuphane.edu.tr", MemberType = "Öğrenci" },
                new UserAccount { Id = 3, Username = "yusuf", Password = "1234", FullName = "Yusuf Uzak", Role = "Admin", Email = "yusuf@firat.edu.tr", MemberType = "Personel" }
            });
        }

        _loans.AddRange(new[]
        {
            new ActiveLoanRecord
            {
                Id = 1,
                BookId = 1,
                BookTitle = "Dune",
                AuthorName = "Frank Herbert",
                CopyBarcode = "KTP-00001-01",
                MemberId = 2,
                MemberName = "Prof. Dr. Ahmet Kaya",
                MemberNumber = "FU-2026-1002",
                BorrowDate = DateTime.Today.AddDays(-5),
                DueDate = DateTime.Today.AddDays(25),
                CategoryTheme = BookCategoryTheme.ScienceFiction,
                PageCount = 688
            },
            new ActiveLoanRecord
            {
                Id = 2,
                BookId = 5,
                BookTitle = "Sefiller",
                AuthorName = "Victor Hugo",
                CopyBarcode = "KTP-00005-01",
                MemberId = 3,
                MemberName = "Zeynep Demir",
                MemberNumber = "FU-2026-1003",
                BorrowDate = DateTime.Today.AddDays(-12),
                DueDate = DateTime.Today.AddDays(2),
                CategoryTheme = BookCategoryTheme.ClassicLiterature,
                PageCount = 1463
            }
        });

        _activities.AddRange(new[]
        {
            new ActivityLogEntry { Id = 1, Title = "Ödünç Verme", Description = "Dune ödünç verildi (Prof. Dr. Ahmet Kaya)", ActorName = "Yusuf Uzak", Timestamp = DateTime.Now.AddMinutes(-2) },
            new ActivityLogEntry { Id = 2, Title = "İade Alma", Description = "Sefiller iade alındı (Mehmet Çelik)", ActorName = "Yusuf Uzak", Timestamp = DateTime.Now.AddMinutes(-7) },
            new ActivityLogEntry { Id = 3, Title = "Ödünç Verme", Description = "Küçük Prens ödünç verildi (Zeynep Demir)", ActorName = "Yusuf Uzak", Timestamp = DateTime.Now.AddMinutes(-12) }
        });
    }

    public IReadOnlyList<Book> GetBooks() { lock (_lock) return _books.ToList(); }
    public IReadOnlyList<Member> GetMembers() { lock (_lock) return _members.ToList(); }
    public IReadOnlyList<ActiveLoanRecord> GetActiveLoans() { lock (_lock) return _loans.ToList(); }
    public IReadOnlyList<ReservationRecord> GetReservations() { lock (_lock) return _reservations.ToList(); }
    public IReadOnlyList<BookCopy> GetAllInventoryCopies() => Array.Empty<BookCopy>();
    public IReadOnlyList<ActivityLogEntry> GetRecentActivity() { lock (_lock) return _activities.ToList(); }

    public IReadOnlyList<MonthlyBorrowStat> GetMonthlyBorrowStats() => new[]
    {
        new MonthlyBorrowStat { MonthLabel = "Oca", BorrowedCount = 140 },
        new MonthlyBorrowStat { MonthLabel = "Şub", BorrowedCount = 310 },
        new MonthlyBorrowStat { MonthLabel = "Mar", BorrowedCount = 390 },
        new MonthlyBorrowStat { MonthLabel = "Nis", BorrowedCount = 440 },
        new MonthlyBorrowStat { MonthLabel = "May", BorrowedCount = 260 },
        new MonthlyBorrowStat { MonthLabel = "Haz", BorrowedCount = 450 },
        new MonthlyBorrowStat { MonthLabel = "Tem", BorrowedCount = 650 },
        new MonthlyBorrowStat { MonthLabel = "Ağu", BorrowedCount = 440 },
        new MonthlyBorrowStat { MonthLabel = "Eyl", BorrowedCount = 520 },
        new MonthlyBorrowStat { MonthLabel = "Eki", BorrowedCount = 560 },
        new MonthlyBorrowStat { MonthLabel = "Kas", BorrowedCount = 730 }
    };

    public IReadOnlyList<Book> GetMostBorrowedBooks(int count = 5)
    {
        lock (_lock) return _books.OrderByDescending(b => b.MonthlyBorrowCount).Take(count).ToList();
    }

    public DashboardMetricsDto GetDashboardMetrics()
    {
        lock (_lock)
        {
            int total = _books.Sum(b => b.TotalCopies);
            int avail = _books.Sum(b => b.AvailableCopies);
            int members = _members.Count;
            int borrowed = _loans.Count(l => l.ReturnDate == null);
            int overdue = _loans.Count(l => l.IsOverdue);
            int today = _activities.Count(a => a.Timestamp.Date == DateTime.Today);
            return new DashboardMetricsDto(total, avail, members, borrowed, overdue, today);
        }
    }

    public Book AddNewBook(Book newBook)
    {
        lock (_lock)
        {
            newBook.Id = _books.Count > 0 ? _books.Max(b => b.Id) + 1 : 1;
            _books.Add(newBook);
            var log = new ActivityLogEntry
            {
                Id = _activities.Count + 1,
                Title = "Yeni Kitap",
                Description = $"{newBook.Title} ({newBook.AuthorName}) kataloğa eklendi.",
                ActorName = "Sistem",
                Timestamp = DateTime.Now
            };
            _activities.Insert(0, log);
            _ = Task.Run(() =>
            {
                _mariaDb.SaveNewBook(newBook);
                _mariaDb.AddActivityLog(log.Title, log.Description, log.ActorName);
            });
            return newBook;
        }
    }

    public void DeleteBook(int bookId)
    {
        lock (_lock)
        {
            var match = _books.FirstOrDefault(b => b.Id == bookId);
            if (match != null) _books.Remove(match);
        }
    }

    public Member AddNewMember(Member newMember)
    {
        lock (_lock)
        {
            newMember.Id = _members.Count > 0 ? _members.Max(m => m.Id) + 1 : 1;
            _members.Add(newMember);
            _ = Task.Run(() => _mariaDb.SaveNewMember(newMember));
            return newMember;
        }
    }

    public bool CommitCheckout(Member member, IReadOnlyList<Book> books, int durationDays, out string summaryMessage)
    {
        lock (_lock)
        {
            if (member == null || books.Count == 0)
            {
                summaryMessage = "Üye veya kitap seçilmedi.";
                return false;
            }

            var createdLoans = new List<ActiveLoanRecord>();

            foreach (var b in books)
            {
                if (b.AvailableCopies > 0) b.AvailableCopies--;
                var loan = new ActiveLoanRecord
                {
                    Id = _loans.Count + 1,
                    BookId = b.Id,
                    BookTitle = b.Title,
                    AuthorName = b.AuthorName,
                    MemberId = member.Id,
                    MemberName = member.FullName,
                    MemberNumber = member.MemberNumber,
                    BorrowDate = DateTime.Today,
                    DueDate = DateTime.Today.AddDays(durationDays),
                    CategoryTheme = b.CategoryTheme,
                    PageCount = b.PageCount
                };
                _loans.Insert(0, loan);
                createdLoans.Add(loan);

                var log = new ActivityLogEntry
                {
                    Id = _activities.Count + 1,
                    Title = "Ödünç Verme",
                    Description = $"{b.Title} kitabı {member.FullName} adına ödünç verildi.",
                    ActorName = "Kütüphane Kullanıcısı",
                    Timestamp = DateTime.Now
                };
                _activities.Insert(0, log);
            }

            _ = Task.Run(() =>
            {
                try
                {
                    foreach (var l in createdLoans)
                    {
                        int dbId = _mariaDb.SaveNewLoan(l);
                        if (dbId > 0) l.Id = dbId;
                    }
                    SyncFromDatabase();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CommitCheckout DB Error: {ex.Message}");
                }
            });

            summaryMessage = $"{books.Count} adet kitap {member.FullName} adına başarıyla ödünç verildi.";
            return true;
        }
    }

    public bool ProcessReturn(int loanId, out string summaryMessage)
    {
        lock (_lock)
        {
            var loan = _loans.FirstOrDefault(l => l.Id == loanId);
            if (loan == null)
            {
                summaryMessage = "Ödünç kaydı bulunamadı.";
                return false;
            }

            loan.ReturnDate = DateTime.Now;
            var book = _books.FirstOrDefault(b => b.Id == loan.BookId);
            if (book != null && book.AvailableCopies < book.TotalCopies)
                book.AvailableCopies++;

            var member = _members.FirstOrDefault(m => m.Id == loan.MemberId);
            if (member != null && member.ActiveLoansCount > 0)
                member.ActiveLoansCount--;

            var log = new ActivityLogEntry
            {
                Id = _activities.Count + 1,
                Title = "İade Alma",
                Description = $"{loan.BookTitle} iade alındı ({loan.MemberName}).",
                ActorName = "Kütüphane Görevlisi",
                Timestamp = DateTime.Now
            };
            _activities.Insert(0, log);

            _ = Task.Run(() =>
            {
                try
                {
                    _mariaDb.MarkLoanReturned(loanId);
                    SyncFromDatabase();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessReturn error: {ex.Message}");
                }
            });

            summaryMessage = $"{loan.BookTitle} başarıyla iade alındı.";
            return true;
        }
    }

    public bool RenewLoan(int loanId, int extraDays, out string summaryMessage)
    {
        lock (_lock)
        {
            var loan = _loans.FirstOrDefault(l => l.Id == loanId);
            if (loan == null)
            {
                summaryMessage = "Ödünç kaydı bulunamadı.";
                return false;
            }

            loan.DueDate = loan.DueDate.AddDays(extraDays);
            loan.RenewalCount++;

            var log = new ActivityLogEntry
            {
                Id = _activities.Count + 1,
                Title = "Süre Uzatıldı",
                Description = $"{loan.BookTitle} süresi {extraDays} gün uzatıldı ({loan.MemberName}).",
                ActorName = "Kütüphane Görevlisi",
                Timestamp = DateTime.Now
            };
            _activities.Insert(0, log);

            _ = Task.Run(() =>
            {
                try
                {
                    _mariaDb.ExtendLoan(loanId, extraDays);
                    SyncFromDatabase();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"RenewLoan error: {ex.Message}");
                }
            });

            summaryMessage = $"{loan.BookTitle} ödünç süresi {extraDays} gün uzatıldı.";
            return true;
        }
    }

    public IReadOnlyList<UserAccount> GetUsers()
    {
        lock (_lock) return _users.ToList();
    }

    public UserAccount? AuthenticateUser(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return null;
        lock (_lock)
        {
            var u = _users.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
            if (u != null && u.Password == password)
            {
                return u;
            }
            return null;
        }
    }

    public bool RegisterUser(UserAccount account, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(account.Username) || account.Username.Trim().Length < 3)
        {
            errorMessage = "Kullanıcı adı en az 3 karakter olmalıdır.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(account.Password) || account.Password.Length < 4)
        {
            errorMessage = "Şifre en az 4 karakter olmalıdır.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(account.FullName))
        {
            errorMessage = "Lütfen ad ve soyadınızı giriniz.";
            return false;
        }

        lock (_lock)
        {
            string uname = account.Username.Trim();
            if (_users.Any(u => string.Equals(u.Username, uname, StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = "Bu kullanıcı adı zaten kullanılmaktadır.";
                return false;
            }

            account.Id = _users.Count > 0 ? _users.Max(u => u.Id) + 1 : 1;
            account.Username = uname;
            account.CreatedAt = DateTime.Now;
            if (string.IsNullOrWhiteSpace(account.Role)) account.Role = "User";
            if (string.IsNullOrWhiteSpace(account.MemberType)) account.MemberType = "Öğrenci";

            _users.Add(account);

            // Kütüphane üyesi olarak da ekle (Kiosk ile ödünç alabilmesi için)
            var existingMember = _members.FirstOrDefault(m => string.Equals(m.FullName, account.FullName, StringComparison.OrdinalIgnoreCase));
            if (existingMember == null)
            {
                var newMem = new Member
                {
                    Id = _members.Count > 0 ? _members.Max(m => m.Id) + 1 : 1,
                    MemberNumber = $"FU-2026-{1000 + account.Id}",
                    FullName = account.FullName,
                    Email = string.IsNullOrWhiteSpace(account.Email) ? $"{account.Username}@ogrenci.firat.edu.tr" : account.Email,
                    PhoneNumber = string.IsNullOrWhiteSpace(account.PhoneNumber) ? "0555 000 0000" : account.PhoneNumber,
                    TierTitle = account.MemberType,
                    Status = MemberStatus.Active,
                    MaxLoanLimit = 5,
                    RegistrationDate = DateTime.Now
                };
                _members.Add(newMem);
                _ = Task.Run(() => _mariaDb.SaveNewMember(newMem));
            }

            // Sistem aktivite logu
            var act = new ActivityLogEntry
            {
                Id = _activities.Count > 0 ? _activities.Max(a => a.Id) + 1 : 1,
                Title = "Yeni Kullanıcı Kaydı",
                Description = $"{account.FullName} ({account.Username}) sisteme kaydoldu.",
                ActorName = account.FullName,
                Timestamp = DateTime.Now,
                AccentTag = "Emerald"
            };
            _activities.Insert(0, act);
            _ = Task.Run(() => _mariaDb.AddActivityLog(act.Title, act.Description, act.ActorName));
            _ = Task.Run(() => _mariaDb.SaveUser(account));
            SaveUsersToLocalCache();
        }

        errorMessage = string.Empty;
        return true;
    }

    private void LoadUsersFromLocalCache()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users_cache.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var cached = System.Text.Json.JsonSerializer.Deserialize<List<UserAccount>>(json);
                if (cached != null && cached.Count > 0)
                {
                    lock (_lock)
                    {
                        _users.Clear();
                        _users.AddRange(cached);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadUsersFromLocalCache error: {ex.Message}");
        }
    }

    private void SaveUsersToLocalCache()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "users_cache.json");
            string json = System.Text.Json.JsonSerializer.Serialize(_users, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SaveUsersToLocalCache error: {ex.Message}");
        }
    }
}
