namespace EquipmentBorrowing.Application.Interfaces;

// One unit of work = one short conversation with the database for ONE user action.
// All three repositories inside it share the same database connection,
// so they can take part in the same transaction.
//
// Always use it with "await using" so it is closed when the action is finished.
// If CommitAsync is never reached, closing it rolls the transaction back.
public interface IUnitOfWork : IAsyncDisposable
{
    IStudentRepository Students { get; }
    IEquipmentRepository Equipment { get; }
    IBorrowingRepository Borrowings { get; }

    // Starts a database transaction. Nothing is permanent until CommitAsync.
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    // Sends the pending changes to PostgreSQL (inside the transaction if one was started).
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    // Makes all saved changes permanent.
    Task CommitAsync(CancellationToken cancellationToken = default);
}
