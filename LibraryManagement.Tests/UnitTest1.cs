using System;
using Dapper;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace LibraryManagement.Tests;

public class UnitTest1
{
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestEndToEndCheckoutAndReturnWithMariaDb()
    {
        var dataService = new LibraryManagement.Infrastructure.LibraryDataService();
        Assert.True(dataService.IsDatabaseConnected, $"MariaDB is not connected: {dataService.DatabaseStatusText}");

        var books = dataService.GetBooks();
        _output.WriteLine($"Loaded {books.Count} books from MariaDB.");
        Assert.True(books.Count >= 14, "Expected at least 14 books loaded from MariaDB");

        var members = dataService.GetMembers();
        _output.WriteLine($"Loaded {members.Count} members from MariaDB.");
        Assert.True(members.Count >= 5, "Expected at least 5 members loaded from MariaDB");

        var testBook = books.First(b => b.Title == "Huzur");
        var testMember = members.First(m => m.Id == 1);
        int initialAvail = testBook.AvailableCopies;

        _output.WriteLine($"Testing checkout: Book='{testBook.Title}', Member='{testMember.FullName}', CurrentAvail={initialAvail}");

        bool checkoutSuccess = dataService.CommitCheckout(testMember, new[] { testBook }, 15, out string summaryMsg);
        Assert.True(checkoutSuccess, $"CommitCheckout failed: {summaryMsg}");
        _output.WriteLine($"Checkout Success: {summaryMsg}");

        // Wait a moment for background DB write to finish
        System.Threading.Thread.Sleep(800);

        // Verify that SyncFromDatabase brings the new loan
        bool syncOk = dataService.SyncFromDatabase();
        Assert.True(syncOk);

        var activeLoans = dataService.GetActiveLoans();
        var myLoan = activeLoans.FirstOrDefault(l => l.BookId == testBook.Id && l.MemberId == testMember.Id && l.ReturnDate == null);
        Assert.NotNull(myLoan);
        _output.WriteLine($"Found new active loan in DB: LoanId={myLoan.Id}, Book={myLoan.BookTitle}, DueDate={myLoan.DueDate:dd.MM.yyyy}");

        var recentActivities = dataService.GetRecentActivity();
        var myActivity = recentActivities.FirstOrDefault(a => a.Description.Contains("Huzur"));
        Assert.NotNull(myActivity);
        _output.WriteLine($"Found new activity log: {myActivity.Title} - {myActivity.Description}");

        // Test ProcessReturn
        bool returnSuccess = dataService.ProcessReturn(myLoan.Id, out string returnMsg);
        Assert.True(returnSuccess, $"ProcessReturn failed: {returnMsg}");
        _output.WriteLine($"Return Success: {returnMsg}");

        System.Threading.Thread.Sleep(800);
        dataService.SyncFromDatabase();

        var reloadedLoan = dataService.GetActiveLoans().FirstOrDefault(l => l.Id == myLoan.Id);
        Assert.NotNull(reloadedLoan);
        Assert.NotNull(reloadedLoan.ReturnDate);
        _output.WriteLine($"Loan successfully marked returned on: {reloadedLoan.ReturnDate}");
    }
}
