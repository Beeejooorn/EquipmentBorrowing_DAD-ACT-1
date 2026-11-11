namespace EquipmentBorrowing.Application.Interfaces;

public enum DatabaseErrorKind
{
    ConnectionFailed,     // server not running, network lost, wrong password...
    Conflict,             // another transaction changed the same data at the same time
    DuplicateValue,       // a unique student number / equipment code already exists
    InvalidReference,     // a foreign key points to a record that does not exist
    ConstraintViolation,  // a CHECK or NOT NULL rule in the database was broken
    Unknown
}

// The Application layer must not know about Npgsql or PostgreSQL error codes.
// The Infrastructure layer implements this interface and turns a database
// exception into one of the simple kinds above.
public interface IDatabaseErrorClassifier
{
    DatabaseErrorKind Classify(Exception exception);
}
