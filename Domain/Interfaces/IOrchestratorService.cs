using ErrorOr;

namespace Domain.Interfaces;

public interface IOrchestratorService {
    Task<ErrorOr<string>> ExecuteAsync(object data);

    Task CompensateAsync(object data);
}
