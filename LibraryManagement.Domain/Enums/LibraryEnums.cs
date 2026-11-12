namespace LibraryManagement.Domain.Enums;

public enum BookCategoryTheme
{
    Fantasy,
    Academic,
    ScienceFiction,
    Poetry,
    Childrens,
    ClassicLiterature,
    HistoryAndPhilosophy,
    ArtAndArchitecture
}

public enum BookPhysicalFormat
{
    PocketPaperback,
    StandardHardcover,
    RoyalOctavo,
    LargeQuartoFolio
}

public enum CopyStatus
{
    Available,
    InCheckoutQueue,
    Borrowed,
    Reserved,
    Maintenance
}

public enum MemberStatus
{
    Active,
    HonoraryVip,
    Restricted,
    Expired
}

public enum CustomerDisplayState
{
    IdleWelcome,
    SelectingBooks,
    PreparingBooks,
    WaitingForConfirmation,
    TransactionCompleted,
    TransactionCancelled
}

public enum ReservationStatus
{
    WaitingInQueue,
    ReadyAtDesk,
    Completed,
    Cancelled
}

