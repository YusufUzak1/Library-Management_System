using LibraryManagement.Application.Interfaces;
using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Application.Services;

public sealed class CheckoutStateService : ICheckoutStateService
{
    private const int MaxCounterStackCapacity = 8;
    private readonly List<Book> _selectedBooks = new();

    public CustomerDisplayState CurrentDisplayState { get; private set; } = CustomerDisplayState.IdleWelcome;
    public string StatusHeadline { get; private set; } = "Grand Scriptorium Kütüphanesine Hoş Geldiniz";
    public string StatusSubtext { get; private set; } = "Ödünç almak istediğiniz eserler bankoya eklendiğinde burada görünecektir.";
    public Member? ActiveMember { get; private set; }
    public int LoanDurationDays { get; private set; } = 14;
    public DateTime EstimatedDueDate => DateTime.Today.AddDays(LoanDurationDays);
    public IReadOnlyList<Book> SelectedBooks => _selectedBooks;

    public event EventHandler<CheckoutBookEventArgs>? BookAddedToCounter;
    public event EventHandler<CheckoutBookEventArgs>? BookRemovedFromCounter;
    public event EventHandler? CounterCleared;
    public event EventHandler<CheckoutSessionSnapshot>? SessionChanged;

    public void SelectMember(Member? member)
    {
        ActiveMember = member;

        if (member is null && _selectedBooks.Count == 0)
        {
            CurrentDisplayState = CustomerDisplayState.IdleWelcome;
            StatusHeadline = "Grand Scriptorium Kütüphanesine Hoş Geldiniz";
            StatusSubtext = "Ödünç almak istediğiniz eserler bankoya eklendiğinde burada görünecektir.";
        }
        else if (member is not null && _selectedBooks.Count == 0)
        {
            CurrentDisplayState = CustomerDisplayState.SelectingBooks;
            StatusHeadline = "Oturum Hazır — Eser Seçimi Bekleniyor";
            StatusSubtext = $"Ödünç süresi {LoanDurationDays} gün olarak ayarlandı. Eserler bankoya yerleştiriliyor.";
        }

        BroadcastSnapshot();
    }

    public bool AddBookToCounter(Book book, out string feedbackMessage)
    {
        ArgumentNullException.ThrowIfNull(book);

        if (_selectedBooks.Any(b => b.Id == book.Id))
        {
            feedbackMessage = $"«{book.Title}» zaten banko yığınında yer alıyor.";
            return false;
        }

        if (book.AvailableCopies <= 0)
        {
            feedbackMessage = $"«{book.Title}» için uygun fiziksel nüsha bulunmuyor.";
            return false;
        }

        if (_selectedBooks.Count >= MaxCounterStackCapacity)
        {
            feedbackMessage = $"Banko üzerinde tek seferde en fazla {MaxCounterStackCapacity} eser istiflenebilir.";
            return false;
        }

        _selectedBooks.Add(book);
        int stackIndex = _selectedBooks.Count - 1;

        CurrentDisplayState = _selectedBooks.Count >= 3
            ? CustomerDisplayState.PreparingBooks
            : CustomerDisplayState.SelectingBooks;

        StatusHeadline = CurrentDisplayState == CustomerDisplayState.PreparingBooks
            ? "Seçilen Eserler Hazırlanıyor"
            : "Eserler Bankoya Ekleniyor";

        StatusSubtext = $"Bankoda {_selectedBooks.Count} eser bulunuyor • İade tarihi: {EstimatedDueDate:dd MMMM yyyy}";

        BookAddedToCounter?.Invoke(this, new CheckoutBookEventArgs(book, stackIndex, _selectedBooks.Count));
        BroadcastSnapshot();

        feedbackMessage = $"«{book.Title}» banko yığınına eklendi.";
        return true;
    }

    public bool RemoveBookFromCounter(int bookId)
    {
        int index = _selectedBooks.FindIndex(b => b.Id == bookId);
        if (index < 0)
        {
            return false;
        }

        Book removed = _selectedBooks[index];
        _selectedBooks.RemoveAt(index);

        if (_selectedBooks.Count == 0)
        {
            CurrentDisplayState = ActiveMember is not null
                ? CustomerDisplayState.SelectingBooks
                : CustomerDisplayState.IdleWelcome;

            StatusHeadline = ActiveMember is not null
                ? "Eser Seçimi Bekleniyor"
                : "Grand Scriptorium Kütüphanesine Hoş Geldiniz";

            StatusSubtext = "Banko üzerindeki tüm eserler kaldırıldı.";
        }
        else
        {
            StatusHeadline = "Seçilen Eser Listesi Güncellendi";
            StatusSubtext = $"Bankoda {_selectedBooks.Count} eser bulunuyor • İade tarihi: {EstimatedDueDate:dd MMMM yyyy}";
        }

        BookRemovedFromCounter?.Invoke(this, new CheckoutBookEventArgs(removed, index, _selectedBooks.Count));
        BroadcastSnapshot();
        return true;
    }

    public void SetLoanDuration(int days)
    {
        LoanDurationDays = Math.Clamp(days, 7, 60);
        if (_selectedBooks.Count > 0)
        {
            StatusSubtext = $"Bankoda {_selectedBooks.Count} eser bulunuyor • İade tarihi: {EstimatedDueDate:dd MMMM yyyy}";
        }
        BroadcastSnapshot();
    }

    public void AdvanceToPreparing()
    {
        if (_selectedBooks.Count == 0) return;
        CurrentDisplayState = CustomerDisplayState.PreparingBooks;
        StatusHeadline = "Eserleriniz Kontrol Ediliyor ve Hazırlanıyor";
        StatusSubtext = $"Toplam {_selectedBooks.Count} eser barkod ve nüsha kontrolünden geçiriliyor.";
        BroadcastSnapshot();
    }

    public void AdvanceToWaitingApproval()
    {
        if (_selectedBooks.Count == 0) return;
        CurrentDisplayState = CustomerDisplayState.WaitingForConfirmation;
        StatusHeadline = "Kütüphaneci Onayı Bekleniyor";
        StatusSubtext = $"{_selectedBooks.Count} eserlik ödünç alma işlemi son onay aşamasında ({EstimatedDueDate:dd.MM.yyyy} vadeli).";
        BroadcastSnapshot();
    }

    public void CompleteCheckoutTransaction()
    {
        int count = _selectedBooks.Count;
        CurrentDisplayState = CustomerDisplayState.TransactionCompleted;
        StatusHeadline = "Ödünç İşlemi Başarıyla Tamamlandı";
        StatusSubtext = $"Keyifli okumalar dileriz! {count} eser {EstimatedDueDate:dd MMMM yyyy} tarihine kadar adınıza kaydedilmiştir.";
        _selectedBooks.Clear();
        CounterCleared?.Invoke(this, EventArgs.Empty);
        BroadcastSnapshot();
    }

    public void CancelCheckoutTransaction()
    {
        CurrentDisplayState = CustomerDisplayState.TransactionCancelled;
        StatusHeadline = "İşlem İptal Edildi";
        StatusSubtext = "Seçilen eserler bankodan kaldırıldı. Yeni bir işlem için hazır.";
        _selectedBooks.Clear();
        CounterCleared?.Invoke(this, EventArgs.Empty);
        BroadcastSnapshot();
    }

    public void ResetToWelcome()
    {
        _selectedBooks.Clear();
        ActiveMember = null;
        CurrentDisplayState = CustomerDisplayState.IdleWelcome;
        StatusHeadline = "Grand Scriptorium Kütüphanesine Hoş Geldiniz";
        StatusSubtext = "Ödünç almak istediğiniz eserler bankoya eklendiğinde burada görünecektir.";
        CounterCleared?.Invoke(this, EventArgs.Empty);
        BroadcastSnapshot();
    }

    private void BroadcastSnapshot()
    {
        var snapshot = new CheckoutSessionSnapshot(
            DisplayState: CurrentDisplayState,
            StatusHeadline: StatusHeadline,
            StatusSubtext: StatusSubtext,
            SelectedBookCount: _selectedBooks.Count,
            TotalPagesInStack: _selectedBooks.Sum(b => b.PageCount),
            LoanDurationDays: LoanDurationDays,
            EstimatedDueDate: EstimatedDueDate,
            SelectedBooks: _selectedBooks.ToList());

        SessionChanged?.Invoke(this, snapshot);
    }
}

