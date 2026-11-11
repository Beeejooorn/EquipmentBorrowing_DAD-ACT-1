namespace EquipmentBorrowing.Application.Interfaces;

// Services ask this factory for a NEW unit of work for every operation.
// This keeps each database context short-lived (see README, "Context lifetime").
public interface IUnitOfWorkFactory
{
    IUnitOfWork Create();
}
