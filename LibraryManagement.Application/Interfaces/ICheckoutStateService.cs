using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Application.Interfaces;

public sealed record CheckoutBookEventArgs(Book Book, int StackIndex, int TotalBooksInStack);

public sealed record CheckoutSessionSnapshot(
    CustomerDisplayState DisplayState,
    string StatusHeadline,
    string StatusSubtext,
    int SelectedBookCount,
    int TotalPagesInStack,
    int LoanDurationDays,
    DateTime EstimatedDueDate,
    IReadOnlyList<Book> SelectedBooks);

public interface ICheckoutStateService
{
    CustomerDisplayState CurrentDisplayState { get; }
    string StatusHeadline { get; }
    string StatusSubtext { get; }
    Member? ActiveMember { get; }
    int LoanDurationDays { get; }
    DateTime EstimatedDueDate { get; }
    IReadOnlyList<Book> SelectedBooks { get; }

    event EventHandler<CheckoutBookEventArgs>? BookAddedToCounter;
    event EventHandler<CheckoutBookEventArgs>? BookRemovedFromCounter;
    event EventHandler? CounterCleared;
    event EventHandler<CheckoutSessionSnapshot>? SessionChanged;

    void SelectMember(Member? member);
    bool AddBookToCounter(Book book, out string feedbackMessage);
    bool RemoveBookFromCounter(int bookId);
    void SetLoanDuration(int days);
    void AdvanceToPreparing();
    void AdvanceToWaitingApproval();
    void CompleteCheckoutTransaction();
    void CancelCheckoutTransaction();
    void ResetToWelcome();
}

