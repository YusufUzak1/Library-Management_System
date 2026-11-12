using LibraryManagement.Domain.Entities;

namespace LibraryManagement.Application.Interfaces;

public sealed record DashboardMetricsDto(
    int TotalBooks,
    int AvailableBooks,
    int RegisteredMembers,
    int CurrentlyBorrowedBooks,
    int OverdueReturns,
    int TodayTransactionsCount);

public interface ILibraryDataService
{
    IReadOnlyList<Book> GetBooks();
    IReadOnlyList<Member> GetMembers();
    IReadOnlyList<ActiveLoanRecord> GetActiveLoans();
    IReadOnlyList<ReservationRecord> GetReservations();
    IReadOnlyList<BookCopy> GetAllInventoryCopies();
    IReadOnlyList<ActivityLogEntry> GetRecentActivity();
    IReadOnlyList<MonthlyBorrowStat> GetMonthlyBorrowStats();
    IReadOnlyList<Book> GetMostBorrowedBooks(int count = 5);
    DashboardMetricsDto GetDashboardMetrics();

    Book AddNewBook(Book newBook);
    void DeleteBook(int bookId);
    Member AddNewMember(Member newMember);
    bool CommitCheckout(Member member, IReadOnlyList<Book> books, int durationDays, out string summaryMessage);
    bool ProcessReturn(int loanId, out string summaryMessage);
    bool RenewLoan(int loanId, int extraDays, out string summaryMessage);

    UserAccount? AuthenticateUser(string username, string password);
    bool RegisterUser(UserAccount account, out string errorMessage);
    IReadOnlyList<UserAccount> GetUsers();

    bool SyncFromDatabase();
    bool IsDatabaseConnected { get; }
    string DatabaseStatusText { get; }
}

