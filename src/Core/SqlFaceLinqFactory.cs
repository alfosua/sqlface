using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SqlFace.Core.Schemas;
using SqlFace.Parsing.SyntaxTrees;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SqlFace.Core;

public class SqlFaceLinqFactory : ISqlFaceLinqFactory
{
    private readonly ISqlFaceContext _context;
    private readonly ISelectQueryPipeFactory _pipeFactory;
    private readonly ISourceExecutor _sourceExecutor;

    public SqlFaceLinqFactory(ISqlFaceContext context, ISelectQueryPipeFactory pipeFactory, ISourceExecutor sourceExecutor)
    {
        _context = context;
        _pipeFactory = pipeFactory;
        _sourceExecutor = sourceExecutor;
    }

    public async Task<object> CreateFromAsync(ISelectQuery selectQuery)
    {
        var selectQueryVisitor = new SelectQueryVisitor(selectQuery);

        var sourceDom = _context.Schemas.SelectMany(x => x.Sources)
            .FirstOrDefault(x => x.Identifiers.Select(x => x.Name).Contains(selectQueryVisitor.SourceName))
            ?? throw new NullReferenceException("No source with given name is defined");
        
        var sourceVisitor = new SourceVisitor(sourceDom);

        using var source = await _sourceExecutor.ExecuteIntoScopeAsync(sourceVisitor);

        var pipe = await _pipeFactory.CreatePipeAsync(sourceVisitor, selectQueryVisitor);

        var query = pipe.Invoke(source.Value);

        return query;
    }
}

internal static class TypeExtensions
{
    public static string GetCSharpFullName(this Type type) => type switch
    {
        var t when t == typeof(bool) => "bool",
        var t when t == typeof(byte) => "byte",
        var t when t == typeof(char) => "char",
        var t when t == typeof(decimal) => "decimal",
        var t when t == typeof(double) => "double",
        var t when t == typeof(float) => "float",
        var t when t == typeof(int) => "int",
        var t when t == typeof(long) => "long",
        var t when t == typeof(short) => "short",
        var t when t == typeof(string) => "string",
        var t when t == typeof(sbyte) => "sbyte",
        var t when t == typeof(uint) => "uint",
        var t when t == typeof(ulong) => "ulong",
        var t when t == typeof(ushort) => "ushort",
        _ => HandleNotStandardTypeCSharpName(type),
    };

    private static string HandleNotStandardTypeCSharpName(Type type)
    {
        StringBuilder nameBuilder = new StringBuilder(type.FullName ?? type.Name);
        
        if (type.IsNested)
        {
            nameBuilder.Replace('+', '.');
        }

        if (type.IsGenericType)
        {
            nameBuilder
                .Append("<")
                .AppendJoin(", ", type.GetGenericArguments().Select(x => x.GetCSharpFullName()))
                .Append(">");
        }
        
        var raw = nameBuilder.ToString();

        var name = Regex.Replace(raw, @"`\d+\[.*\]", "");

        return name;
    }
}

public interface ISourceExecutor
{
    Task<IScope> ExecuteIntoScopeAsync(ISourceVisitor sourceVisitor);

    public interface IScope : IDisposable
    {
        object Value { get; }
    }
}

public class SourceExecutor : ISourceExecutor
{
    private readonly Func<IServiceScopeFactory> _factoryOfServiceScopeFactory;

    public SourceExecutor(Func<IServiceScopeFactory> factoryOfServiceScopeFactory)
    {
        _factoryOfServiceScopeFactory = factoryOfServiceScopeFactory;
    }

    public async Task<ISourceExecutor.IScope> ExecuteIntoScopeAsync(ISourceVisitor sourceVisitor)
    {
        var scope = _factoryOfServiceScopeFactory().CreateScope();
        var instance = scope.ServiceProvider.GetRequiredService(sourceVisitor.ResolverServiceType);

        var resolution = sourceVisitor.Resolver.Invoke(instance, null);
        var source = resolution;

        if (resolution is Task task)
        {
            await task;

            source = ((dynamic)task).Result;
        }

        if (source is not IEnumerable)
        {
            throw new NotImplementedException("Source type is not supported. It must be enumerable.");
        }

        return new Scope(scope, source);
    }

    public class Scope : ISourceExecutor.IScope
    {
        private readonly IServiceScope _scope;
        private readonly object _value;

        public Scope(IServiceScope scope, object value)
        {
            _scope = scope;
            _value = value;
        }

        public object Value => _value;

        public void Dispose()
        {
            _scope.Dispose();
        }
    }
}

public interface ISourceVisitor
{
    Type SourceClrType { get; }
    MethodInfo Resolver { get; }
    Type ResolverServiceType { get; }
    IReadOnlyCollection<ISqlFaceObjectProperty> TypeProperties { get; }
    ISqlFaceSource Value { get; }
}

public class SourceVisitor : ISourceVisitor
{
    private readonly ISqlFaceSource _source;

    public SourceVisitor(ISqlFaceSource source)
    {
        _source = source;
    }

    public ISqlFaceSource Value => _source;

    public Type SourceClrType => (_source.Type as ISqlFaceClrSourcifiable)?.SourcingClrType
        ?? throw new NotImplementedException("Source type is not supported");

    public MethodInfo Resolver => _source.Resolver
        ?? throw new NullReferenceException("Not resolver available to select from this source");

    public Type ResolverServiceType => Resolver.DeclaringType
        ?? throw new NotImplementedException("Resolver is not supported");
    
    public IReadOnlyCollection<ISqlFaceObjectProperty> TypeProperties
        => (_source.Type as ISqlFaceObjectifiable)?.Properties
        ?? throw new NotImplementedException("Source type is not supported");
}

public interface ISelectQueryVisitor
{
    ISelectQuery Value { get; }
    ISourceReference SourceReference { get; }
    string SourceName { get; }
    ITupleSelection TupleSelection { get; }
}

public class SelectQueryVisitor : ISelectQueryVisitor
{
    private readonly ISelectQuery _selectQuery;

    public SelectQueryVisitor(ISelectQuery selectQuery)
    {
        _selectQuery = selectQuery;
    }

    public ISelectQuery Value => _selectQuery;

    public ISourceReference SourceReference => _selectQuery.Selectable as ISourceReference
        ?? throw new NotImplementedException("Source reference is not supported");

    public string SourceName => SourceReference.Path.GetPathString();

    public ITupleSelection TupleSelection => _selectQuery.Selection as ITupleSelection
        ?? throw new NotImplementedException("Selection is not supported");
}

public interface ISelectQueryPipeFactory
{
    Task<Func<object, object>> CreatePipeAsync(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor);
}

public class SelectQueryPipeFactory : ISelectQueryPipeFactory
{
    private readonly IMemoryCache _cache;

    public SelectQueryPipeFactory(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<Func<object, object>> CreatePipeAsync(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor)
    {
        const string cacheTabulation = $"{nameof(SelectQueryPipeFactory)}.{nameof(CreatePipeAsync)}";
        var cacheKey = new { Tab = cacheTabulation, Source = sourceVisitor.Value, SelectQuery = selectQueryVisitor.Value };

        var pipe = await _cache.GetOrCreateAsync(
            cacheKey, async (cacheEntry) => await CreatePipeInternalAsync(sourceVisitor, selectQueryVisitor));

        return pipe;
    }

    private string Pascalize(string x) => x.Substring(0, 1).ToUpper() + x.Substring(1);

    private async Task<Func<object, object>> CreatePipeInternalAsync(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor)
    {
        var sourceTypeName = sourceVisitor.SourceClrType.GetCSharpFullName();

        var columns = new PropertyProjectionAnalysis(sourceVisitor, selectQueryVisitor);

        var topDelimitationCode = selectQueryVisitor.Value.Top switch
        {
            IAll => string.Empty,
            IFirst => ".FirstOrDefault()",
            ILast => ".LastOrDefault()",
            IOne => ".SingleOrDefault()",
            ITopByQuantity top => $".Take({top.Quantity})",
            ITopByPercentage => throw new NotImplementedException("Top by percentage is not supported yet"),
            _ => throw new NotImplementedException("Top is not supported")
        };

        Func<string, string> translate = x =>
        {
            var targetOutputName = Pascalize(x);
            return columns.FirstOrDefault(x => x.OutputName == targetOutputName)?.OutputName ?? x;
        };

        var orderingsCode = selectQueryVisitor.Value.Orderings
            .Select(x => x.Expression switch
            {
                INamePath path => path.Sequence switch
                {
                    INameItem oi => new { Path = oi.Identifier, x.Direction },
                    _ => throw new NotImplementedException("Object path not supported"),
                },
                _ => throw new NotImplementedException("Expression not supported"),
            })
            .Select(x => x.Direction switch
            {
                OrderDirection.Ascending => new StringBuilder().AppendFormat(".OrderBy(x => x.{0})", translate(x.Path)),
                OrderDirection.Descending => new StringBuilder().AppendFormat(".OrderByDescending(x => x.{0})", translate(x.Path)),
                _ => throw new NotImplementedException("Order direction not supported"),
            })
            .Aggregate(new StringBuilder(), (accum, next) => accum.Append(next));

        var offsetCode = selectQueryVisitor.Value.Offset switch
        {
            null => string.Empty,
            var offset => $".Skip({offset.Quantity})",
        };

        var limitationCode = selectQueryVisitor.Value.Limit switch
        {
            null => string.Empty,
            var limit => $".Take({limit.Quantity})",
        };

        var paginationCode = selectQueryVisitor.Value.Pagination switch
        {
            null => string.Empty,
            { Page: < 1 } => throw new NotImplementedException("Page index must be a number greather than 1"),
            { Size: < 1 } => throw new NotImplementedException("Page size must be a number greather than 1"),
            var pagination => $".Skip({(pagination.Page - 1) * pagination.Size}).Take({pagination.Size})",
        };

        var propsDefCode = new StringBuilder()
            .Append("\t")
            .AppendJoin("\n\t", columns.Select(x => $"public {x.CSharpTypeName} {x.OutputName} {{ get; set; }}"));

        var targetTypeName = new StringBuilder().AppendJoin("", sourceTypeName, Guid.NewGuid().ToString("n"));

        var resultTypeName = selectQueryVisitor.Value.Top switch
        {
            IFirst or ILast or IOne => targetTypeName,
            _ => new StringBuilder().AppendFormat("IEnumerable<{0}>", targetTypeName),
        };

        var classDefCode = new StringBuilder()
            .AppendFormat("public class {0}\n{{\n{1}\n}}\n", targetTypeName, propsDefCode);

        var funcTypeDefCode = new StringBuilder()
            .AppendFormat("Func<IEnumerable<{0}>, {1}>", sourceTypeName, resultTypeName);

        var propsAssignmentsCode = new StringBuilder()
            .Append("\t")
            .AppendJoin("\n\t", columns.Select(x => $"{x.OutputName} = {x.ExpressionCode},"));

        var selectionCode = new StringBuilder().AppendFormat(".Select(x => new {0}\n{{\n{1}\n}})",
            targetTypeName,
            propsAssignmentsCode);

        var functionDefCode = new StringBuilder()
            .AppendFormat("{0} pipe = (source) => source", funcTypeDefCode)
            .Append(selectionCode)
            .Append(orderingsCode)
            .Append(topDelimitationCode)
            .Append(offsetCode)
            .Append(limitationCode)
            .Append(paginationCode)
            .Append(";\n");

        var functionReturnCode = new StringBuilder()
            .AppendFormat("return (source) => pipe(source as IEnumerable<{0}>);", sourceTypeName);

        var dynamicSelectPipeCode = new StringBuilder()
            .AppendJoin("\n", classDefCode, functionDefCode, functionReturnCode)
            .ToString();

        ScriptOptions options = ScriptOptions.Default
            .WithReferences(new[]
            {
                Assembly.Load("System.Reflection"),
                Assembly.Load("System.Collections"),
                Assembly.Load("System.Linq"),
                sourceVisitor.SourceClrType.Assembly,
            })
            .WithImports(new[]
            {
                "System",
                "System.Reflection",
                "System.Collections.Generic",
                "System.Linq",
                "System.Linq.Expressions",
            });

        var pipe = await CSharpScript.EvaluateAsync<Func<object, object>>(dynamicSelectPipeCode, options);

        return pipe;
    }
}

public class PropertyProjectionAnalysis : IEnumerable<PropertyProjection>
{
    private readonly IReadOnlyCollection<ISqlFaceObjectProperty> _sourceProps;
    private readonly ISelectQueryVisitor _selectQueryVisitor;
    private List<PropertyProjection>? _projections;

    public PropertyProjectionAnalysis(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor)
    {
        _sourceProps = sourceVisitor.TypeProperties;
        _selectQueryVisitor = selectQueryVisitor;
    }

    public IEnumerator<PropertyProjection> GetEnumerator()
    {
        _projections ??= ExtractCommonData(_selectQueryVisitor.Value.Selection)
            .SelectMany(x => CreateProjectionsFrom(x.OutputPath, x.Expression))
            .ToList();

        return _projections.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IEnumerable<(INamePath? OutputPath, IExpression Expression)> ExtractCommonData(ISelection selection) => selection switch
    {
        ITupleSelection ts => ts.Atoms.Select(x => x switch
        {
            ITupleSelectionAssignment tsa => (tsa.OutputPath, tsa.Expression),
            ITupleSelectionExpression tse => (tse.OutputPath, tse.Expression),
            var ts => throw new NotImplementedException($"Tuple selection atom `{ts.GetType().FullName}` is not supported"),
        }),
        
        IMapSelection ms => throw new NotImplementedException("Map selection is not supported"),

        var s => throw new NotImplementedException($"Selection `{s.GetType().FullName}` is not supported"),
    };

    private IEnumerable<PropertyProjection> CreateProjectionsFrom(INamePath? outputPath, IExpression expression) => expression switch
    {
        IWildcard w => CreateProjectionsByWildcard(outputPath, w),
        var expr => new[] { CreateProjectionByExpresion(outputPath, expr) },
    };

    private IEnumerable<PropertyProjection> CreateProjectionsByWildcard(INamePath? outputPath, IWildcard wildcard) => outputPath switch
    {
        null => _sourceProps
            .Select(x => new PropertyProjection(x.Property.PropertyType.GetCSharpFullName(), x.Property.Name, new StringBuilder().AppendFormat("x.{0}", x.Property.Name))),

        _ => throw new NotImplementedException("Output path is not supported yet for wildcarded expansion"),
    };
    
    private PropertyProjection CreateProjectionByExpresion(INamePath? outputPath, IExpression expression)
    {
        var outputName = TranslateOutputPathToCSharpCode(outputPath, expression);
        var expressionCode = TranslateExpressionToCSharpCode(expression);
        return new PropertyProjection("dynamic", outputName, expressionCode);
    }

    private string TranslateOutputPathToCSharpCode(INamePath? outputPath, IExpression expression)
    {
        return outputPath switch
        {
            null => expression switch
            {
                INamePath np => np.GetPathString(),
                _ => $"column{UtilityExtensions.GetRandomHexString(6)}",
            },
            var path => path.GetPathString(),
        };
    }

    private StringBuilder TranslateExpressionToCSharpCode(IExpression expression) => expression switch
    {
        IOperator o => o  switch
        {
            IAdditionOperator add => new StringBuilder()
                .AppendFormat("{0} + {1}", TranslateExpressionToCSharpCode(add.Left), TranslateExpressionToCSharpCode(add.Right)),

            ISubtractionOperator add => new StringBuilder()
                .AppendFormat("{0} - {1}", TranslateExpressionToCSharpCode(add.Left), TranslateExpressionToCSharpCode(add.Right)),
            
            IMultiplicationOperator add => new StringBuilder()
                .AppendFormat("{0} * {1}", TranslateExpressionToCSharpCode(add.Left), TranslateExpressionToCSharpCode(add.Right)),

            IDivisionOperator add => new StringBuilder()
                .AppendFormat("{0} / {1}", TranslateExpressionToCSharpCode(add.Left), TranslateExpressionToCSharpCode(add.Right)),

            IModuloOperator add => new StringBuilder()
                .AppendFormat("{0} % {1}", TranslateExpressionToCSharpCode(add.Left), TranslateExpressionToCSharpCode(add.Right)),

            _ => throw new NotImplementedException($"Operator `{o.GetType().FullName}` is not supported"),
        },

        ILiteral l => l switch
        {
            IStringLiteral sl => new StringBuilder().AppendFormat("\"{0}\"", sl.Value),
            IBooleanLiteral bl => new StringBuilder().AppendFormat("{0}", bl.Value ? "true" : "false"),
            IIntegerLiteral il => new StringBuilder().AppendFormat("{0}", il.Value),
            IFloatingPointLiteral fpl => new StringBuilder().AppendFormat("{0}", fpl.Value),
            _ => throw new NotImplementedException($"Literal `{l.GetType().FullName}` is not supported"),
        },
        
        INamePath np => _sourceProps
            .Where(x => x.Identifier.Name == np.GetPathString())
            .Take(1)
            .Select(x => new StringBuilder().AppendFormat("x.{0}", x.Property.Name))
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Reference is out of context"),

        _ => throw new NotImplementedException("Expression is not supported"),
    };
}

public record PropertyProjection(string CSharpTypeName, string OutputName, StringBuilder ExpressionCode);

public static class UtilityExtensions
{
    public static string GetPathString(this INamePath path)
    {
        return string.Join('.', path.Sequence.Select(x => x.Identifier));
    }

    public static string GetRandomHexString(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];

        rng.GetBytes(bytes);

        return string.Join("", bytes.Select(b => b.ToString("x2")));
    }
}
