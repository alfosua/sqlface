using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SqlFace.Core;
using SqlFace.Parsing;

namespace SqlFace.AspNetCore;

public static class SqlFaceAspNetCoreExtensions
{
    public static IServiceCollection AddSqlFaceContext<TTopic>(this IServiceCollection services, Action<ISqlFaceContextBuilder> decorator)
    {
        Func<ISqlFaceContext> contextFactory = () =>
        {
            var builder = new SqlFaceContextBuilder();
            decorator.Invoke(builder);
            return builder.Build<TTopic>();
        };

        Func<IServiceProvider, ISqlFaceContainer<TTopic>> containerFactory = (serviceProvider) =>
        {
            var services = new ServiceCollection();
            services.AddMemoryCache();
            services.AddTransient((_) => contextFactory());
            services.AddTransient((_) => serviceProvider.GetRequiredService<IMemoryCache>());
            services.AddTransient<Func<IServiceScopeFactory>>((_) => () => serviceProvider.GetRequiredService<IServiceScopeFactory>());
            services.AddTransient<Func<IServiceProvider>>((_) => () => serviceProvider);
            services.AddTransient<ISqlFaceLinqFactory, SqlFaceLinqFactory>();
            services.AddTransient<ISelectQueryPipeFactory, SelectQueryPipeFactory>();
            services.AddTransient<ISourceExecutor, SourceExecutor>();
            services.AddTransient<ISqlFaceParser, SqlFaceParser>();
            services.AddTransient<ISqlFaceRunner, SqlFaceRunner>();
            return new SqlFaceContainer<TTopic>(services);
        };
        
        services.AddSingleton((sp) => containerFactory(sp));
        services.AddSingleton((sp) => sp.GetRequiredService<ISqlFaceContainer<TTopic>>() as ISqlFaceContainer);

        var resolverDeclaringTypes = contextFactory()
            .Schemas.SelectMany(x => x.Sources)
            .Select(x => x.Resolver?.DeclaringType)
            .Where(x => x is not null)
            .GroupBy(x => x)
            .Select(x => x.First() ?? throw new NullReferenceException());

        foreach (var transient in resolverDeclaringTypes)
        {
            services.AddTransient(transient);
        }

        return services;
    }
    
    public static IEndpointRouteBuilder MapSqlFace(this IEndpointRouteBuilder app)
    {
        var containers = app.ServiceProvider.GetRequiredService<IEnumerable<ISqlFaceContainer>>();
        
        foreach (var container in containers)
        {
            app.MapPost("api", async ([FromBody] SqlFacePayload payload) =>
            {
                if (string.IsNullOrEmpty(payload.Script))
                {
                    return Results.NoContent();
                }
                
                var parser = container.ServiceProvider.GetRequiredService<ISqlFaceParser>();
                var runner = container.ServiceProvider.GetRequiredService<ISqlFaceRunner>();

                var syntaxTree = parser.Parse(payload.Script);

                var data = await runner.RunAsync(syntaxTree);

                return Results.Json(new SqlFaceResponse()
                {
                    Data = data,
                });
            });
        }

        return app;
    }
}
