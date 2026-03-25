using Domain.Enums;

namespace Domain.Interfaces;

public interface IOrchestratorService
{
    Task<State> ExecuteAsync(object data);
    Task CompensateAsync(object data);
}