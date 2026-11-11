namespace EquipmentBorrowing.Application.Services;

public enum SimulatedCheckoutFailure
{
    None,                       // normal behaviour
    AfterFirstWrite,            // fail on purpose after the first item was written inside the transaction
    LostCommitAcknowledgement   // commit, then act as if the connection dropped before the answer arrived
}

// CONTROLLED TEST SEAM for the Lab 4 safe atomicity demonstration (Appendix C A06, BR-15, BR-18).
// It is only used by the multi-item checkout, and it is OFF (None) unless someone turns it on
// in the "Demo / test options" of the Multi-Item Checkout screen or in an automated test.
// Use it only with a disposable database.
public class CheckoutFailureSimulator
{
    public SimulatedCheckoutFailure Mode { get; set; } = SimulatedCheckoutFailure.None;
}

// Thrown by the seam to simulate a failure in the middle of the transaction.
public class SimulatedFailureException : Exception
{
    public SimulatedFailureException(string message) : base(message)
    {
    }
}

// Thrown by the seam to simulate a lost connection while waiting for the commit answer.
public class SimulatedConnectionLostException : Exception
{
    public SimulatedConnectionLostException(string message) : base(message)
    {
    }
}
