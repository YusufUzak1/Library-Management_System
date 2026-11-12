using System.Collections.ObjectModel;
using System.Windows.Threading;
using Scriptorium.Core;
using Scriptorium.Data;
using Scriptorium.Models;

namespace Scriptorium.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly DatabaseService _db;
    private readonly DispatcherTimer _toastTimer;

    // ==================== AUTHENTICATION & SHELL STATE ====================
    private bool _isAuthenticated;
    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    private string _loginUsername = "admin";
    public string LoginUsername
    {
        get => _loginUsername;
        set => SetProperty(ref _loginUsername, value);
    }

    private string _loginPassword = "1234";
    public string LoginPassword
    {
        get => _loginPassword;
        set => SetProperty(ref _loginPassword, value);
    }

    private string _loginErrorMessage = string.Empty;
    public string LoginErrorMessage
    {
        get => _loginErrorMessage;
        set => SetProperty(ref _loginErrorMessage, value);
    }

    private UserAccount _currentUser = new();
    public UserAccount CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
                OnPropertyChanged(nameof(SidebarWidth));
        }
    }

    public System.Windows.GridLength SidebarWidth => new(IsSidebarCollapsed ? 62 : 236);

    private string _currentScreen = "Dashboard";
    public string CurrentScreen
    {
        get => _currentScreen;
        set
        {
            if (SetProperty(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(CurrentBreadcrumbCategory));
                OnPropertyChanged(nameof(CurrentPageTitle));
                OnPropertyChanged(nameof(CurrentPageSubtitle));
            }
        }
    }

    public string CurrentBreadcrumbCategory => CurrentScreen switch
    {
        "Dashboard" => "GENEL BAKIŞ",
        "Catalog" or "Authors" or "Categories" or "Publishers" => "KOLEKSİYON VE ENVANTER",
        "Circulation" or "Members" or "Reservations" or "Overdue" => "DOLAŞIM VE OKUYUCU HİZMETLERİ",
        "Reports" or "Users" or "Settings" => "KURUMSAL YÖNETİM",
        _ => "SCRIPTORIUM"
    };

    public string CurrentPageTitle => CurrentScreen switch
    {
        "Dashboard" => "Yönetim ve Dolaşım Özeti",
        "Catalog" => "Kitap Kataloğu ve Bibliyografik Envanter",
        "Authors" => "Yazarlar ve Eser Külliyatı",
        "Categories" => "Kategoriler ve Dewey Raf Sınıflandırması",
        "Publishers" => "Yayınevleri ve Tedarik Kayıtları",
        "Circulation" => "Ödünç Verme ve İade Bankosu",
        "Members" => "Kütüphane Üyeleri ve Okur Dosyaları",
        "Reservations" => "Kitap Rezervasyonları ve Sıra Yönetimi",
        "Overdue" => "Gecikmiş Kitaplar ve İade Takibi",
        "Reports" => "Raporlar ve Kütüphane İstatistikleri",
        "Users" => "Personel Hesapları ve Yetki Yönetimi",
        "Settings" => "Kütüphane Kuralları ve Sistem Ayarları",
        _ => "Scriptorium"
    };

    public string CurrentPageSubtitle => CurrentScreen switch
    {
        "Dashboard" => $"Çarşamba, {DateTime.Now:dd MMMM yyyy} • Merkez Kütüphane Günlük Operasyon Masası",
        "Catalog" => $"{TotalTitlesCount} bibliyografik eser, {TotalPhysicalCopiesCount} fiziksel nüsha kayıtlı",
        "Circulation" => "Barkod, ISBN veya üye numarası ile hızlı ödünç verme, iade alma ve süre uzatma",
        "Members" => $"{RegisteredMembersCount} kayıtlı üye • Kota, aktif ödünç ve geçmiş takibi",
        "Overdue" => $"{OverdueBooksCount} gecikmiş nüsha • Öncelik ve gecikme gününe göre sıralı",
        _ => _db.Settings.LibraryName
    };

    private string _globalSearchQuery = string.Empty;
    public string GlobalSearchQuery
    {
        get => _globalSearchQuery;
        set => SetProperty(ref _globalSearchQuery, value);
    }

    // ==================== TOAST NOTIFICATION BANNER ====================
    private bool _isToastVisible;
    public bool IsToastVisible
    {
        get => _isToastVisible;
        set => SetProperty(ref _isToastVisible, value);
    }

    private string _toastMessage = string.Empty;
    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    private string _toastKind = "Success";
    public string ToastKind
    {
        get => _toastKind;
        set => SetProperty(ref _toastKind, value);
    }

    public void ShowToast(string message, string kind = "Success")
    {
        ToastMessage = message;
        ToastKind = kind;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    // ==================== DASHBOARD KPI PROPERTIES ====================
    public int TotalTitlesCount => _db.Books.Count;
    public int TotalPhysicalCopiesCount => _db.Books.Sum(b => b.TotalCopies);
    public int AvailableCopiesCount => _db.Books.Sum(b => b.AvailableCopies);
    public int BorrowedCopiesCount => _db.Books.Sum(b => b.BorrowedCopies);
    public int RegisteredMembersCount => _db.Members.Count;
    public int ActiveMembersCount => _db.Members.Count(m => m.Status == "Aktif");
    public int OverdueBooksCount => _db.BorrowRecords.Count(r => r.IsOverdue);
    public int CriticalOverdueCount => _db.BorrowRecords.Count(r => r.IsOverdue && r.OverdueDays >= 7);
    public int DueTodayCount => _db.BorrowRecords.Count(r => r.IsDueToday);
    public int ActiveReservationsCount => _db.Reservations.Count(r => r.Status == "Bekliyor" || r.Status == "Rafta Ayrıldı");
    public int ReadyOnShelfReservationsCount => _db.Reservations.Count(r => r.Status == "Rafta Ayrıldı");
    public decimal TotalUnpaidFines => _db.Members.Sum(m => m.FineBalance);
    public string TotalUnpaidFinesText => $"{TotalUnpaidFines:N2} ₺";
    public string AvailabilityPercentageText => TotalPhysicalCopiesCount > 0
        ? $"%{(double)AvailableCopiesCount / TotalPhysicalCopiesCount * 100:F1} rafta erişilebilir"
        : "%100";
    public string CirculationPercentageText => TotalPhysicalCopiesCount > 0
        ? $"%{(double)BorrowedCopiesCount / TotalPhysicalCopiesCount * 100:F1} aktif dolaşımda"
        : "%0";

    public string ConnectionStatusText => _db.ConnectionStatusText;
    public bool IsMariaDbConnected => _db.IsMariaDbConnected;
    public string RulesSummaryStatusText =>
        $"Standart Ödünç: {_db.Settings.StandardLoanDays} Gün ({_db.Settings.MaxBooksStandard} Kitap)  |  Akademik: {_db.Settings.AcademicLoanDays} Gün ({_db.Settings.MaxBooksAcademic} Kitap)  |  Günlük Gecikme: {_db.Settings.DailyOverdueFee:N2} ₺";

    // ==================== COLLECTIONS FOR VIEWS ====================
    public ObservableCollection<BorrowRecord> RecentActivityList { get; } = [];
    public ObservableCollection<BorrowRecord> DueTodayAndUrgentList { get; } = [];
    public ObservableCollection<Book> MostBorrowedBooksList { get; } = [];
    public ObservableCollection<MonthlyLoanStat> MonthlyStatsList { get; } = [];
    public ObservableCollection<CategoryStat> CategoryStatsList { get; } = [];

    // ==================== CATALOG & BOOK DETAILS STATE ====================
    public ObservableCollection<Book> FilteredBooks { get; } = [];
    public ObservableCollection<string> CategoryFilterOptions { get; } = [];
    public ObservableCollection<string> PublisherFilterOptions { get; } = [];
    public ObservableCollection<string> AvailabilityFilterOptions { get; } =
        ["Tüm Durumlar", "Mevcut (Rafta)", "Son 1 Nüsha", "Tümü Ödünçte"];
    public ObservableCollection<string> SortCatalogOptions { get; } =
        ["Başlık (A–Z)", "Yazar (A–Z)", "Yayın Yılı (Yeni→Eski)", "En Çok Ödünç Alınan", "Raf Konumu"];

    private string _catalogSearchText = string.Empty;
    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set { if (SetProperty(ref _catalogSearchText, value)) ApplyCatalogFilters(); }
    }

    private string _selectedCategoryFilter = "Tüm Kategoriler";
    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set { if (SetProperty(ref _selectedCategoryFilter, value)) ApplyCatalogFilters(); }
    }

    private string _selectedPublisherFilter = "Tüm Yayınevleri";
    public string SelectedPublisherFilter
    {
        get => _selectedPublisherFilter;
        set { if (SetProperty(ref _selectedPublisherFilter, value)) ApplyCatalogFilters(); }
    }

    private string _selectedAvailabilityFilter = "Tüm Durumlar";
    public string SelectedAvailabilityFilter
    {
        get => _selectedAvailabilityFilter;
        set { if (SetProperty(ref _selectedAvailabilityFilter, value)) ApplyCatalogFilters(); }
    }

    private string _selectedCatalogSort = "Başlık (A–Z)";
    public string SelectedCatalogSort
    {
        get => _selectedCatalogSort;
        set { if (SetProperty(ref _selectedCatalogSort, value)) ApplyCatalogFilters(); }
    }

    private Book? _selectedBook;
    public Book? SelectedBook
    {
        get => _selectedBook;
        set
        {
            if (SetProperty(ref _selectedBook, value))
            {
                RefreshSelectedBookHistory();
                if (value is not null && !IsEditingBook)
                    LoadBookIntoEditor(value);
            }
        }
    }

    public ObservableCollection<BorrowRecord> SelectedBookLoanHistory { get; } = [];

    private bool _isEditingBook;
    public bool IsEditingBook
    {
        get => _isEditingBook;
        set => SetProperty(ref _isEditingBook, value);
    }

    private Book _editableBook = new();
    public Book EditableBook
    {
        get => _editableBook;
        set => SetProperty(ref _editableBook, value);
    }

    private string _bookInspectorTab = "Kunye"; // Kunye, Gecmis, Duzenle
    public string BookInspectorTab
    {
        get => _bookInspectorTab;
        set => SetProperty(ref _bookInspectorTab, value);
    }

    // ==================== CIRCULATION DESK (BORROWING & RETURNING) ====================
    private string _circulationMode = "IssueDesk"; // IssueDesk, RapidReturn
    public string CirculationMode
    {
        get => _circulationMode;
        set => SetProperty(ref _circulationMode, value);
    }

    private string _circulationMemberSearch = string.Empty;
    public string CirculationMemberSearch
    {
        get => _circulationMemberSearch;
        set
        {
            if (SetProperty(ref _circulationMemberSearch, value))
                FilterCirculationMembers();
        }
    }

    public ObservableCollection<Member> CirculationMemberSuggestions { get; } = [];

    private Member? _selectedCirculationMember;
    public Member? SelectedCirculationMember
    {
        get => _selectedCirculationMember;
        set
        {
            if (SetProperty(ref _selectedCirculationMember, value))
            {
                RefreshCirculationMemberLoans();
                if (value is not null)
                {
                    var days = value.MemberType == "Akademik Personel"
                        ? _db.Settings.AcademicLoanDays
                        : _db.Settings.StandardLoanDays;
                    CirculationDueDate = DateTime.Today.AddDays(days);
                }
            }
        }
    }

    public ObservableCollection<BorrowRecord> CirculationMemberActiveLoans { get; } = [];

    private string _circulationBookSearch = string.Empty;
    public string CirculationBookSearch
    {
        get => _circulationBookSearch;
        set
        {
            if (SetProperty(ref _circulationBookSearch, value))
                FilterCirculationBooks();
        }
    }

    public ObservableCollection<Book> CirculationBookSuggestions { get; } = [];

    private Book? _selectedCirculationBook;
    public Book? SelectedCirculationBook
    {
        get => _selectedCirculationBook;
        set => SetProperty(ref _selectedCirculationBook, value);
    }

    private DateTime _circulationDueDate = DateTime.Today.AddDays(15);
    public DateTime CirculationDueDate
    {
        get => _circulationDueDate;
        set => SetProperty(ref _circulationDueDate, value);
    }

    private string _circulationNotes = string.Empty;
    public string CirculationNotes
    {
        get => _circulationNotes;
        set => SetProperty(ref _circulationNotes, value);
    }

    private string _rapidReturnQuery = string.Empty;
    public string RapidReturnQuery
    {
        get => _rapidReturnQuery;
        set
        {
            if (SetProperty(ref _rapidReturnQuery, value))
                FilterActiveLoansForReturn();
        }
    }

    public ObservableCollection<BorrowRecord> ActiveLoansForReturnList { get; } = [];

    // ==================== MEMBERS MANAGEMENT STATE ====================
    public ObservableCollection<Member> FilteredMembers { get; } = [];
    public ObservableCollection<string> MemberTypeOptions { get; } =
        ["Akademik Personel", "Lisansüstü Öğrenci", "Üniversite Öğrencisi", "Araştırmacı", "Standart Okur"];
    public ObservableCollection<string> MemberStatusOptions { get; } =
        ["Aktif", "Askıda", "Üyelik Süresi Dolmuş"];

    private string _memberSearchText = string.Empty;
    public string MemberSearchText
    {
        get => _memberSearchText;
        set { if (SetProperty(ref _memberSearchText, value)) ApplyMemberFilters(); }
    }

    private string _selectedMemberStatusFilter = "Tüm Üyeler";
    public string SelectedMemberStatusFilter
    {
        get => _selectedMemberStatusFilter;
        set { if (SetProperty(ref _selectedMemberStatusFilter, value)) ApplyMemberFilters(); }
    }

    private Member? _selectedMember;
    public Member? SelectedMember
    {
        get => _selectedMember;
        set
        {
            if (SetProperty(ref _selectedMember, value))
            {
                RefreshSelectedMemberLoans();
                if (value is not null)
                    LoadMemberIntoEditor(value);
            }
        }
    }

    public ObservableCollection<BorrowRecord> SelectedMemberActiveLoans { get; } = [];
    public ObservableCollection<BorrowRecord> SelectedMemberHistory { get; } = [];

    private Member _editableMember = new();
    public Member EditableMember
    {
        get => _editableMember;
        set => SetProperty(ref _editableMember, value);
    }

    private string _memberInspectorTab = "Profil"; // Profil, Odunc, Duzenle
    public string MemberInspectorTab
    {
        get => _memberInspectorTab;
        set => SetProperty(ref _memberInspectorTab, value);
    }

    // ==================== AUTHORS, CATEGORIES, PUBLISHERS ====================
    public ObservableCollection<Author> AuthorsList { get; } = [];
    private Author? _selectedAuthor;
    public Author? SelectedAuthor
    {
        get => _selectedAuthor;
        set
        {
            if (SetProperty(ref _selectedAuthor, value) && value is not null)
            {
                EditableAuthor = new Author
                {
                    Id = value.Id,
                    FullName = value.FullName,
                    Country = value.Country,
                    BirthDeathYears = value.BirthDeathYears,
                    LiteraryMovement = value.LiteraryMovement,
                    Biography = value.Biography
                };
            }
        }
    }

    private Author _editableAuthor = new();
    public Author EditableAuthor
    {
        get => _editableAuthor;
        set => SetProperty(ref _editableAuthor, value);
    }

    public ObservableCollection<Category> CategoriesList { get; } = [];
    private Category? _selectedCategory;
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value) && value is not null)
            {
                EditableCategory = new Category
                {
                    Id = value.Id,
                    DeweyCode = value.DeweyCode,
                    Name = value.Name,
                    ShelfCorridor = value.ShelfCorridor,
                    Description = value.Description
                };
            }
        }
    }

    private Category _editableCategory = new();
    public Category EditableCategory
    {
        get => _editableCategory;
        set => SetProperty(ref _editableCategory, value);
    }

    public ObservableCollection<Publisher> PublishersList { get; } = [];
    private Publisher? _selectedPublisher;
    public Publisher? SelectedPublisher
    {
        get => _selectedPublisher;
        set
        {
            if (SetProperty(ref _selectedPublisher, value) && value is not null)
            {
                EditablePublisher = new Publisher
                {
                    Id = value.Id,
                    Name = value.Name,
                    City = value.City,
                    FoundedYear = value.FoundedYear,
                    ContactEmail = value.ContactEmail,
                    Phone = value.Phone,
                    Website = value.Website,
                    Notes = value.Notes
                };
            }
        }
    }

    private Publisher _editablePublisher = new();
    public Publisher EditablePublisher
    {
        get => _editablePublisher;
        set => SetProperty(ref _editablePublisher, value);
    }

    // ==================== RESERVATIONS & OVERDUE BOOKS ====================
    public ObservableCollection<Reservation> ReservationsList { get; } = [];
    private Reservation? _selectedReservation;
    public Reservation? SelectedReservation
    {
        get => _selectedReservation;
        set => SetProperty(ref _selectedReservation, value);
    }

    private Book? _newReservationBook;
    public Book? NewReservationBook
    {
        get => _newReservationBook;
        set => SetProperty(ref _newReservationBook, value);
    }

    private Member? _newReservationMember;
    public Member? NewReservationMember
    {
        get => _newReservationMember;
        set => SetProperty(ref _newReservationMember, value);
    }

    private string _newReservationNotes = string.Empty;
    public string NewReservationNotes
    {
        get => _newReservationNotes;
        set => SetProperty(ref _newReservationNotes, value);
    }

    public ObservableCollection<BorrowRecord> OverdueRecordsList { get; } = [];
    private string _overdueSearchText = string.Empty;
    public string OverdueSearchText
    {
        get => _overdueSearchText;
        set { if (SetProperty(ref _overdueSearchText, value)) RefreshOverdueList(); }
    }

    // ==================== USERS & SETTINGS ====================
    public ObservableCollection<UserAccount> UsersList { get; } = [];
    private UserAccount _editableUser = new();
    public UserAccount EditableUser
    {
        get => _editableUser;
        set => SetProperty(ref _editableUser, value);
    }

    public LibrarySettings EditableSettings => _db.Settings;

    // ==================== COMMANDS ====================
    public RelayCommand LoginCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ExecuteGlobalSearchCommand { get; }
    public RelayCommand DismissToastCommand { get; }

    // Catalog Commands
    public RelayCommand ResetCatalogFiltersCommand { get; }
    public RelayCommand SelectBookInspectorTabCommand { get; }
    public RelayCommand StartNewBookCommand { get; }
    public RelayCommand SaveBookCommand { get; }
    public RelayCommand DeleteSelectedBookCommand { get; }
    public RelayCommand QuickIssueSelectedBookCommand { get; }

    // Circulation Commands
    public RelayCommand SetCirculationModeCommand { get; }
    public RelayCommand SetLoanDurationDaysCommand { get; }
    public RelayCommand ExecuteBorrowCommand { get; }
    public RelayCommand ProcessReturnCommand { get; }
    public RelayCommand ProcessRenewalCommand { get; }

    // Member Commands
    public RelayCommand SelectMemberInspectorTabCommand { get; }
    public RelayCommand StartNewMemberCommand { get; }
    public RelayCommand SaveMemberCommand { get; }
    public RelayCommand OpenMemberInCirculationCommand { get; }

    // Author / Category / Publisher Commands
    public RelayCommand StartNewAuthorCommand { get; }
    public RelayCommand SaveAuthorCommand { get; }
    public RelayCommand StartNewCategoryCommand { get; }
    public RelayCommand SaveCategoryCommand { get; }
    public RelayCommand StartNewPublisherCommand { get; }
    public RelayCommand SavePublisherCommand { get; }

    // Reservation Commands
    public RelayCommand CreateReservationCommand { get; }
    public RelayCommand FulfillReservationToLoanCommand { get; }
    public RelayCommand MarkReservationReadyCommand { get; }
    public RelayCommand CancelReservationCommand { get; }

    // User & Settings Commands
    public RelayCommand StartNewUserCommand { get; }
    public RelayCommand SaveUserCommand { get; }
    public RelayCommand SaveSettingsCommand { get; }
    public RelayCommand TestMariaDbConnectionCommand { get; }

    public MainViewModel()
    {
        _db = new DatabaseService();
        _db.Initialize();

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _toastTimer.Tick += (_, _) =>
        {
            IsToastVisible = false;
            _toastTimer.Stop();
        };

        CurrentUser = _db.Users.First();
        IsAuthenticated = false; // Starts on minimal, sophisticated Login Screen

        LoginCommand = new RelayCommand(ExecuteLogin);
        LogoutCommand = new RelayCommand(() =>
        {
            IsAuthenticated = false;
            LoginPassword = string.Empty;
            LoginErrorMessage = string.Empty;
        });

        NavigateCommand = new RelayCommand(param =>
        {
            if (param is string screen && !string.IsNullOrWhiteSpace(screen))
                CurrentScreen = screen;
        });

        ToggleSidebarCommand = new RelayCommand(() => IsSidebarCollapsed = !IsSidebarCollapsed);
        ExecuteGlobalSearchCommand = new RelayCommand(ExecuteGlobalSearch);
        DismissToastCommand = new RelayCommand(() => IsToastVisible = false);

        ResetCatalogFiltersCommand = new RelayCommand(() =>
        {
            CatalogSearchText = string.Empty;
            SelectedCategoryFilter = "Tüm Kategoriler";
            SelectedPublisherFilter = "Tüm Yayınevleri";
            SelectedAvailabilityFilter = "Tüm Durumlar";
            SelectedCatalogSort = "Başlık (A–Z)";
        });

        SelectBookInspectorTabCommand = new RelayCommand(p =>
        {
            if (p is string tab) BookInspectorTab = tab;
        });

        StartNewBookCommand = new RelayCommand(StartNewBook);
        SaveBookCommand = new RelayCommand(SaveBook);
        DeleteSelectedBookCommand = new RelayCommand(DeleteSelectedBook);
        QuickIssueSelectedBookCommand = new RelayCommand(param =>
        {
            var book = param as Book ?? SelectedBook;
            if (book is null) return;
            SelectedCirculationBook = book;
            CirculationMode = "IssueDesk";
            CurrentScreen = "Circulation";
            ShowToast($"'{book.Title}' ödünç bankosuna aktarıldı. Lütfen üye seçin.", "Info");
        });

        SetCirculationModeCommand = new RelayCommand(p =>
        {
            if (p is string mode) CirculationMode = mode;
        });

        SetLoanDurationDaysCommand = new RelayCommand(p =>
        {
            if (int.TryParse(p?.ToString(), out var days))
                CirculationDueDate = DateTime.Today.AddDays(days);
        });

        ExecuteBorrowCommand = new RelayCommand(ExecuteBorrow);
        ProcessReturnCommand = new RelayCommand(ProcessReturn);
        ProcessRenewalCommand = new RelayCommand(ProcessRenewal);

        SelectMemberInspectorTabCommand = new RelayCommand(p =>
        {
            if (p is string tab) MemberInspectorTab = tab;
        });
        StartNewMemberCommand = new RelayCommand(StartNewMember);
        SaveMemberCommand = new RelayCommand(SaveMember);
        OpenMemberInCirculationCommand = new RelayCommand(param =>
        {
            var member = param as Member ?? SelectedMember;
            if (member is null) return;
            SelectedCirculationMember = member;
            CirculationMode = "IssueDesk";
            CurrentScreen = "Circulation";
            ShowToast($"{member.FullName} ödünç/iade bankosuna seçildi.", "Info");
        });

        StartNewAuthorCommand = new RelayCommand(() =>
        {
            EditableAuthor = new Author { Country = "Türkiye", LiteraryMovement = "Çağdaş Edebiyat" };
        });
        SaveAuthorCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(EditableAuthor.FullName))
            {
                ShowToast("Yazar adı ve soyadı boş bırakılamaz.", "Danger");
                return;
            }
            var saved = _db.SaveAuthor(EditableAuthor);
            RefreshAllData();
            SelectedAuthor = saved;
            ShowToast($"Yazar kaydı güncellendi: {saved.FullName}", "Success");
        });

        StartNewCategoryCommand = new RelayCommand(() =>
        {
            EditableCategory = new Category { DeweyCode = "810.00", ShelfCorridor = "Salon A • Koridor 1" };
        });
        SaveCategoryCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(EditableCategory.Name))
            {
                ShowToast("Kategori adı boş bırakılamaz.", "Danger");
                return;
            }
            var saved = _db.SaveCategory(EditableCategory);
            RefreshAllData();
            SelectedCategory = saved;
            ShowToast($"Kategori kaydedildi: {saved.DeweyCode} — {saved.Name}", "Success");
        });

        StartNewPublisherCommand = new RelayCommand(() =>
        {
            EditablePublisher = new Publisher { City = "İstanbul", FoundedYear = 2000 };
        });
        SavePublisherCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(EditablePublisher.Name))
            {
                ShowToast("Yayınevi adı boş bırakılamaz.", "Danger");
                return;
            }
            var saved = _db.SavePublisher(EditablePublisher);
            RefreshAllData();
            SelectedPublisher = saved;
            ShowToast($"Yayınevi kaydedildi: {saved.Name}", "Success");
        });

        CreateReservationCommand = new RelayCommand(ExecuteCreateReservation);
        FulfillReservationToLoanCommand = new RelayCommand(ExecuteFulfillReservation);
        MarkReservationReadyCommand = new RelayCommand(param =>
        {
            if (param is Reservation res)
            {
                var result = _db.UpdateReservationStatus(res.Id, "Rafta Ayrıldı");
                RefreshAllData();
                ShowToast(result.Message, result.Success ? "Success" : "Danger");
            }
        });
        CancelReservationCommand = new RelayCommand(param =>
        {
            if (param is Reservation res)
            {
                var result = _db.UpdateReservationStatus(res.Id, "İptal Edildi");
                RefreshAllData();
                ShowToast(result.Message, "Warning");
            }
        });

        StartNewUserCommand = new RelayCommand(() =>
        {
            EditableUser = new UserAccount { Role = "Kütüphaneci", Title = "Kütüphane Uzmanı", IsActive = true, PasswordHash = "1234" };
        });
        SaveUserCommand = new RelayCommand(() =>
        {
            if (string.IsNullOrWhiteSpace(EditableUser.Username) || string.IsNullOrWhiteSpace(EditableUser.FullName))
            {
                ShowToast("Kullanıcı adı ve ad soyad zorunludur.", "Danger");
                return;
            }
            var saved = _db.SaveUserAccount(EditableUser);
            RefreshAllData();
            ShowToast($"Personel hesabı kaydedildi: {saved.FullName} ({saved.Role})", "Success");
        });

        SaveSettingsCommand = new RelayCommand(() =>
        {
            _db.SaveSettingsConfig();
            _db.RecalculateAggregates();
            RefreshAllData();
            OnPropertyChanged(nameof(RulesSummaryStatusText));
            ShowToast("Kütüphane kuralları ve yapılandırma ayarları kaydedildi.", "Success");
        });

        TestMariaDbConnectionCommand = new RelayCommand(() =>
        {
            var s = _db.Settings;
            var (ok, msg) = _db.TestAndReconnect(s.DbHost, s.DbPort, s.DbName, s.DbUser, s.DbPassword);
            RefreshAllData();
            OnPropertyChanged(nameof(ConnectionStatusText));
            OnPropertyChanged(nameof(IsMariaDbConnected));
            ShowToast(msg, ok ? "Success" : "Danger");
        });

        RefreshAllData();
        SelectedBook = FilteredBooks.FirstOrDefault();
        SelectedMember = FilteredMembers.FirstOrDefault();
        SelectedCirculationMember = _db.Members.FirstOrDefault();
        SelectedCirculationBook = _db.Books.FirstOrDefault(b => b.IsAvailable);
        SelectedAuthor = AuthorsList.FirstOrDefault();
        SelectedCategory = CategoriesList.FirstOrDefault();
        SelectedPublisher = PublishersList.FirstOrDefault();
        EditableUser = UsersList.FirstOrDefault() ?? new UserAccount();
    }

    private void ExecuteLogin()
    {
        var username = LoginUsername.Trim();
        var password = LoginPassword.Trim();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            LoginErrorMessage = "Lütfen personel kullanıcı adı ve şifrenizi girin.";
            return;
        }

        var match = _db.Users.FirstOrDefault(u =>
            u.IsActive &&
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase) &&
            u.PasswordHash == password);

        if (match is null)
        {
            LoginErrorMessage = "Kullanıcı adı veya parola hatalı. (Demo: admin / 1234)";
            return;
        }

        match.LastLogin = DateTime.Now;
        CurrentUser = match;
        LoginErrorMessage = string.Empty;
        IsAuthenticated = true;
        ShowToast($"Hoş geldiniz, {match.FullName} ({match.Title}). Vardiya oturumu açıldı.", "Success");
    }

    private void ExecuteGlobalSearch()
    {
        var q = GlobalSearchQuery.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        // Check if query matches a member number
        var memberMatch = _db.Members.FirstOrDefault(m =>
            m.MemberNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase));

        var bookMatch = _db.Books.FirstOrDefault(b =>
            b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            b.Isbn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            b.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            b.AuthorName.Contains(q, StringComparison.OrdinalIgnoreCase));

        if (bookMatch is not null)
        {
            CatalogSearchText = q;
            CurrentScreen = "Catalog";
            SelectedBook = bookMatch;
            ShowToast($"Katalogda '{q}' için {FilteredBooks.Count} kayıt bulundu.", "Info");
        }
        else if (memberMatch is not null)
        {
            MemberSearchText = q;
            CurrentScreen = "Members";
            SelectedMember = memberMatch;
            ShowToast($"Üye rehberinde '{memberMatch.FullName}' bulundu.", "Info");
        }
        else
        {
            CatalogSearchText = q;
            CurrentScreen = "Catalog";
            ShowToast($"'{q}' için eşleşen kayıt aranıyor.", "Warning");
        }
    }

    public void RefreshAllData()
    {
        _db.RecalculateAggregates();

        // Filter dropdown options
        var prevCat = SelectedCategoryFilter;
        CategoryFilterOptions.Clear();
        CategoryFilterOptions.Add("Tüm Kategoriler");
        foreach (var c in _db.Categories.OrderBy(x => x.Name))
            CategoryFilterOptions.Add(c.Name);
        SelectedCategoryFilter = CategoryFilterOptions.Contains(prevCat) ? prevCat : "Tüm Kategoriler";

        var prevPub = SelectedPublisherFilter;
        PublisherFilterOptions.Clear();
        PublisherFilterOptions.Add("Tüm Yayınevleri");
        foreach (var p in _db.Publishers.OrderBy(x => x.Name))
            PublisherFilterOptions.Add(p.Name);
        SelectedPublisherFilter = PublisherFilterOptions.Contains(prevPub) ? prevPub : "Tüm Yayınevleri";

        // Dashboard collections
        RecentActivityList.Clear();
        foreach (var br in _db.BorrowRecords.OrderByDescending(r => r.BorrowDate).Take(8))
            RecentActivityList.Add(br);

        DueTodayAndUrgentList.Clear();
        foreach (var br in _db.BorrowRecords
            .Where(r => !r.IsReturned && (r.IsDueToday || r.IsOverdue))
            .OrderByDescending(r => r.OverdueDays)
            .ThenBy(r => r.DueDate))
        {
            DueTodayAndUrgentList.Add(br);
        }

        MostBorrowedBooksList.Clear();
        foreach (var b in _db.Books.OrderByDescending(x => x.TotalBorrowCount).Take(6))
            MostBorrowedBooksList.Add(b);

        MonthlyStatsList.Clear();
        foreach (var ms in _db.MonthlyStats)
            MonthlyStatsList.Add(ms);

        CategoryStatsList.Clear();
        var totalCopies = Math.Max(1, _db.Books.Sum(b => b.TotalCopies));
        foreach (var c in _db.Categories)
        {
            var catBooks = _db.Books.Where(b => b.CategoryId == c.Id).ToList();
            var copies = catBooks.Sum(b => b.TotalCopies);
            CategoryStatsList.Add(new CategoryStat
            {
                CategoryName = c.Name,
                DeweyCode = c.DeweyCode,
                TitleCount = catBooks.Count,
                CopyCount = copies,
                LoanCount = catBooks.Sum(b => b.TotalBorrowCount),
                SharePercentage = Math.Round((double)copies / totalCopies * 100, 1)
            });
        }

        AuthorsList.Clear();
        foreach (var a in _db.Authors.OrderBy(x => x.FullName))
            AuthorsList.Add(a);

        CategoriesList.Clear();
        foreach (var c in _db.Categories.OrderBy(x => x.DeweyCode))
            CategoriesList.Add(c);

        PublishersList.Clear();
        foreach (var p in _db.Publishers.OrderBy(x => x.Name))
            PublishersList.Add(p);

        ReservationsList.Clear();
        foreach (var r in _db.Reservations.OrderBy(x => x.Status == "Rafta Ayrıldı" ? 0 : x.Status == "Bekliyor" ? 1 : 2).ThenBy(x => x.QueuePosition))
            ReservationsList.Add(r);

        UsersList.Clear();
        foreach (var u in _db.Users)
            UsersList.Add(u);

        ApplyCatalogFilters();
        ApplyMemberFilters();
        FilterCirculationMembers();
        FilterCirculationBooks();
        FilterActiveLoansForReturn();
        RefreshOverdueList();
        RefreshCirculationMemberLoans();
        RefreshSelectedBookHistory();
        RefreshSelectedMemberLoans();

        // Notify KPI bindings
        OnPropertyChanged(nameof(TotalTitlesCount));
        OnPropertyChanged(nameof(TotalPhysicalCopiesCount));
        OnPropertyChanged(nameof(AvailableCopiesCount));
        OnPropertyChanged(nameof(BorrowedCopiesCount));
        OnPropertyChanged(nameof(RegisteredMembersCount));
        OnPropertyChanged(nameof(ActiveMembersCount));
        OnPropertyChanged(nameof(OverdueBooksCount));
        OnPropertyChanged(nameof(CriticalOverdueCount));
        OnPropertyChanged(nameof(DueTodayCount));
        OnPropertyChanged(nameof(ActiveReservationsCount));
        OnPropertyChanged(nameof(ReadyOnShelfReservationsCount));
        OnPropertyChanged(nameof(TotalUnpaidFines));
        OnPropertyChanged(nameof(TotalUnpaidFinesText));
        OnPropertyChanged(nameof(AvailabilityPercentageText));
        OnPropertyChanged(nameof(CirculationPercentageText));
        OnPropertyChanged(nameof(CurrentPageSubtitle));
    }

    public void ApplyCatalogFilters()
    {
        IEnumerable<Book> query = _db.Books;

        if (!string.IsNullOrWhiteSpace(CatalogSearchText))
        {
            var q = CatalogSearchText.Trim();
            query = query.Where(b =>
                b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.AuthorName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Isbn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.ShelfLocation.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SelectedCategoryFilter) && SelectedCategoryFilter != "Tüm Kategoriler")
            query = query.Where(b => b.CategoryName == SelectedCategoryFilter);

        if (!string.IsNullOrWhiteSpace(SelectedPublisherFilter) && SelectedPublisherFilter != "Tüm Yayınevleri")
            query = query.Where(b => b.PublisherName == SelectedPublisherFilter);

        query = SelectedAvailabilityFilter switch
        {
            "Mevcut (Rafta)" => query.Where(b => b.AvailableCopies > 0),
            "Son 1 Nüsha" => query.Where(b => b.AvailableCopies == 1 && b.TotalCopies > 1),
            "Tümü Ödünçte" => query.Where(b => b.AvailableCopies <= 0),
            _ => query
        };

        query = SelectedCatalogSort switch
        {
            "Yazar (A–Z)" => query.OrderBy(b => b.AuthorName).ThenBy(b => b.Title),
            "Yayın Yılı (Yeni→Eski)" => query.OrderByDescending(b => b.PublicationYear).ThenBy(b => b.Title),
            "En Çok Ödünç Alınan" => query.OrderByDescending(b => b.TotalBorrowCount),
            "Raf Konumu" => query.OrderBy(b => b.ShelfLocation),
            _ => query.OrderBy(b => b.Title)
        };

        var prevSelectedId = SelectedBook?.Id;
        FilteredBooks.Clear();
        foreach (var b in query)
            FilteredBooks.Add(b);

        if (prevSelectedId.HasValue)
            SelectedBook = FilteredBooks.FirstOrDefault(b => b.Id == prevSelectedId.Value) ?? FilteredBooks.FirstOrDefault();
    }

    private void RefreshSelectedBookHistory()
    {
        SelectedBookLoanHistory.Clear();
        if (SelectedBook is null) return;
        foreach (var r in _db.BorrowRecords.Where(x => x.BookId == SelectedBook.Id).OrderByDescending(x => x.BorrowDate))
            SelectedBookLoanHistory.Add(r);
    }

    private void LoadBookIntoEditor(Book source)
    {
        EditableBook = new Book
        {
            Id = source.Id,
            Isbn = source.Isbn,
            Barcode = source.Barcode,
            Title = source.Title,
            Subtitle = source.Subtitle,
            AuthorId = source.AuthorId,
            AuthorName = source.AuthorName,
            PublisherId = source.PublisherId,
            PublisherName = source.PublisherName,
            CategoryId = source.CategoryId,
            CategoryName = source.CategoryName,
            DeweyCode = source.DeweyCode,
            PublicationYear = source.PublicationYear,
            Edition = source.Edition,
            PageCount = source.PageCount,
            Language = source.Language,
            ShelfLocation = source.ShelfLocation,
            TotalCopies = source.TotalCopies,
            AvailableCopies = source.AvailableCopies,
            PhysicalCondition = source.PhysicalCondition,
            Summary = source.Summary,
            CoverHex = source.CoverHex
        };
    }

    private void StartNewBook()
    {
        var nextNum = _db.Books.Count + 1;
        EditableBook = new Book
        {
            Id = 0,
            Isbn = $"978975{1000000 + nextNum:D7}",
            Barcode = $"KTP-{nextNum:D5}-01",
            Title = string.Empty,
            Subtitle = string.Empty,
            AuthorId = _db.Authors.First().Id,
            PublisherId = _db.Publishers.First().Id,
            CategoryId = _db.Categories.First().Id,
            PublicationYear = DateTime.Today.Year,
            Edition = "1. Basım",
            PageCount = 240,
            Language = "Türkçe",
            ShelfLocation = "A1-10-A",
            TotalCopies = 3,
            AvailableCopies = 3,
            PhysicalCondition = "Yeni",
            CoverHex = "#1E4D3B",
            Summary = string.Empty
        };
        BookInspectorTab = "Duzenle";
        ShowToast("Yeni kitap kaydı formu hazırlandı.", "Info");
    }

    private void SaveBook()
    {
        if (string.IsNullOrWhiteSpace(EditableBook.Title) || string.IsNullOrWhiteSpace(EditableBook.Isbn))
        {
            ShowToast("Kitap adı ve ISBN-13 numarası zorunludur.", "Danger");
            return;
        }

        var isNew = EditableBook.Id == 0;
        var saved = _db.SaveBook(EditableBook);
        RefreshAllData();
        SelectedBook = saved;
        BookInspectorTab = "Kunye";
        ShowToast(isNew
            ? $"Yeni kitap kataloğa eklendi: '{saved.Title}' ({saved.Barcode})"
            : $"Kitap künye ve envanter bilgileri güncellendi: '{saved.Title}'", "Success");
    }

    private void DeleteSelectedBook()
    {
        if (SelectedBook is null) return;
        var (ok, msg) = _db.DeleteBook(SelectedBook.Id);
        RefreshAllData();
        SelectedBook = FilteredBooks.FirstOrDefault();
        ShowToast(msg, ok ? "Warning" : "Danger");
    }

    // ==================== CIRCULATION LOGIC ====================

    private void FilterCirculationMembers()
    {
        CirculationMemberSuggestions.Clear();
        var q = CirculationMemberSearch.Trim();
        var matches = string.IsNullOrWhiteSpace(q)
            ? _db.Members
            : _db.Members.Where(m =>
                m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.MemberNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Phone.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var m in matches)
            CirculationMemberSuggestions.Add(m);
    }

    private void RefreshCirculationMemberLoans()
    {
        CirculationMemberActiveLoans.Clear();
        if (SelectedCirculationMember is null) return;
        foreach (var r in _db.BorrowRecords.Where(x => x.MemberId == SelectedCirculationMember.Id && !x.IsReturned).OrderBy(x => x.DueDate))
            CirculationMemberActiveLoans.Add(r);
    }

    private void FilterCirculationBooks()
    {
        CirculationBookSuggestions.Clear();
        var q = CirculationBookSearch.Trim();
        var matches = string.IsNullOrWhiteSpace(q)
            ? _db.Books
            : _db.Books.Where(b =>
                b.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Isbn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                b.AuthorName.Contains(q, StringComparison.OrdinalIgnoreCase));

        foreach (var b in matches)
            CirculationBookSuggestions.Add(b);
    }

    private void FilterActiveLoansForReturn()
    {
        ActiveLoansForReturnList.Clear();
        var q = RapidReturnQuery.Trim();
        var active = _db.BorrowRecords.Where(r => !r.IsReturned);
        if (!string.IsNullOrWhiteSpace(q))
        {
            active = active.Where(r =>
                r.BookTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.BookBarcode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.BookIsbn.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.MemberName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.MemberNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TransactionCode.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in active.OrderBy(x => x.DueDate))
            ActiveLoansForReturnList.Add(r);
    }

    private void ExecuteBorrow()
    {
        if (SelectedCirculationMember is null)
        {
            ShowToast("Lütfen ödünç verilecek üyeyi seçin.", "Danger");
            return;
        }
        if (SelectedCirculationBook is null)
        {
            ShowToast("Lütfen ödünç verilecek kitabı seçin.", "Danger");
            return;
        }

        var (ok, msg, _) = _db.BorrowBook(
            SelectedCirculationMember.Id,
            SelectedCirculationBook.Id,
            CirculationDueDate,
            CurrentUser.FullName,
            CirculationNotes);

        if (ok)
        {
            CirculationNotes = string.Empty;
            RefreshAllData();
            ShowToast(msg, "Success");
        }
        else
        {
            ShowToast(msg, "Danger");
        }
    }

    private void ProcessReturn(object? param)
    {
        if (param is not BorrowRecord record) return;
        var (ok, msg) = _db.ReturnBook(record.Id);
        RefreshAllData();
        ShowToast(msg, ok ? "Success" : "Danger");
    }

    private void ProcessRenewal(object? param)
    {
        if (param is not BorrowRecord record) return;
        var (ok, msg) = _db.RenewLoan(record.Id, _db.Settings.StandardLoanDays);
        RefreshAllData();
        ShowToast(msg, ok ? "Success" : "Warning");
    }

    // ==================== MEMBERS LOGIC ====================

    public void ApplyMemberFilters()
    {
        IEnumerable<Member> query = _db.Members;

        if (!string.IsNullOrWhiteSpace(MemberSearchText))
        {
            var q = MemberSearchText.Trim();
            query = query.Where(m =>
                m.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.MemberNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Email.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Phone.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.DepartmentOrInstitution.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedMemberStatusFilter switch
        {
            "Aktif Üyeler" => query.Where(m => m.Status == "Aktif"),
            "Gecikmesi Olanlar" => query.Where(m => m.OverdueCount > 0),
            "Askıdaki Üyeler" => query.Where(m => m.Status == "Askıda"),
            _ => query
        };

        var prevId = SelectedMember?.Id;
        FilteredMembers.Clear();
        foreach (var m in query)
            FilteredMembers.Add(m);

        if (prevId.HasValue)
            SelectedMember = FilteredMembers.FirstOrDefault(m => m.Id == prevId.Value) ?? FilteredMembers.FirstOrDefault();
    }

    private void RefreshSelectedMemberLoans()
    {
        SelectedMemberActiveLoans.Clear();
        SelectedMemberHistory.Clear();
        if (SelectedMember is null) return;

        foreach (var r in _db.BorrowRecords.Where(x => x.MemberId == SelectedMember.Id))
        {
            SelectedMemberHistory.Add(r);
            if (!r.IsReturned)
                SelectedMemberActiveLoans.Add(r);
        }
    }

    private void LoadMemberIntoEditor(Member m)
    {
        EditableMember = new Member
        {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            FullName = m.FullName,
            IdentityNumber = m.IdentityNumber,
            MemberType = m.MemberType,
            Email = m.Email,
            Phone = m.Phone,
            DepartmentOrInstitution = m.DepartmentOrInstitution,
            Address = m.Address,
            RegistrationDate = m.RegistrationDate,
            Status = m.Status,
            MaxLoanLimit = m.MaxLoanLimit,
            Notes = m.Notes
        };
    }

    private void StartNewMember()
    {
        var nextNum = 1000 + _db.Members.Count + 1;
        EditableMember = new Member
        {
            Id = 0,
            MemberNumber = $"UYE-2026-{nextNum:D4}",
            FullName = string.Empty,
            IdentityNumber = string.Empty,
            MemberType = "Standart Okur",
            Email = string.Empty,
            Phone = "05",
            DepartmentOrInstitution = string.Empty,
            Address = "İstanbul",
            Status = "Aktif",
            MaxLoanLimit = _db.Settings.MaxBooksStandard,
            Notes = string.Empty
        };
        MemberInspectorTab = "Duzenle";
        ShowToast("Yeni üye kayıt formu açıldı.", "Info");
    }

    private void SaveMember()
    {
        if (string.IsNullOrWhiteSpace(EditableMember.FullName))
        {
            ShowToast("Üye adı ve soyadı zorunludur.", "Danger");
            return;
        }
        var isNew = EditableMember.Id == 0;
        var saved = _db.SaveMember(EditableMember);
        RefreshAllData();
        SelectedMember = saved;
        MemberInspectorTab = "Profil";
        ShowToast(isNew
            ? $"Yeni üye kaydı tamamlandı: {saved.FullName} ({saved.MemberNumber})"
            : $"Üye bilgileri güncellendi: {saved.FullName}", "Success");
    }

    // ==================== RESERVATIONS & OVERDUE LOGIC ====================

    private void ExecuteCreateReservation()
    {
        if (NewReservationMember is null || NewReservationBook is null)
        {
            ShowToast("Rezervasyon için lütfen bir üye ve kitap seçin.", "Danger");
            return;
        }
        var (ok, msg) = _db.CreateReservation(NewReservationMember.Id, NewReservationBook.Id, NewReservationNotes);
        if (ok)
        {
            NewReservationNotes = string.Empty;
            RefreshAllData();
        }
        ShowToast(msg, ok ? "Success" : "Danger");
    }

    private void ExecuteFulfillReservation(object? param)
    {
        var res = param as Reservation ?? SelectedReservation;
        if (res is null) return;

        var dueDays = _db.Settings.StandardLoanDays;
        var (ok, msg, _) = _db.BorrowBook(res.MemberId, res.BookId, DateTime.Today.AddDays(dueDays), CurrentUser.FullName, $"Rezervasyon ({res.ReservationCode}) üzerinden ödünç verildi.");
        RefreshAllData();
        ShowToast(msg, ok ? "Success" : "Danger");
    }

    private void RefreshOverdueList()
    {
        OverdueRecordsList.Clear();
        var q = OverdueSearchText.Trim();
        var overdues = _db.BorrowRecords.Where(r => r.IsOverdue);
        if (!string.IsNullOrWhiteSpace(q))
        {
            overdues = overdues.Where(r =>
                r.BookTitle.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.MemberName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.MemberNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.BookBarcode.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var r in overdues.OrderByDescending(x => x.OverdueDays))
            OverdueRecordsList.Add(r);
    }
}

