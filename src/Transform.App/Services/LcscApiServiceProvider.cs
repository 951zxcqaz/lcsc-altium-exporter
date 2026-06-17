#nullable enable

using Npnp.Core.Services;

namespace Transform.App.Services;

public interface ILcscApiServiceProvider
{
    ILcscApiService? Current { get; }
}

public class LcscApiServiceProvider : ILcscApiServiceProvider
{
    public ILcscApiService? Current { get; }

    public LcscApiServiceProvider(ILcscApiService apiService)
    {
        Current = apiService;
    }
}
