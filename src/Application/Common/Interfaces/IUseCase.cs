using ErrorOr;

namespace NovaFE.Application.Common;

public interface IUseCase<in TRequest, TResponse>
{
    Task<ErrorOr<TResponse>> Execute(TRequest request, CancellationToken ct = default);
}