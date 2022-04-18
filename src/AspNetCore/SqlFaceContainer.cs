using Microsoft.Extensions.DependencyInjection;

namespace SqlFace.AspNetCore;

public interface ISqlFaceContainer<TTopic> : ISqlFaceContainer { }

public interface ISqlFaceContainer
{
    IServiceProvider ServiceProvider { get; }
}

public class SqlFaceContainer<TTopic> : ISqlFaceContainer<TTopic>
{
    private readonly IServiceProvider serviceProvider;

    public SqlFaceContainer(IServiceCollection services)
    {
        serviceProvider = services.BuildServiceProvider();
    }

    public IServiceProvider ServiceProvider => serviceProvider;
}
