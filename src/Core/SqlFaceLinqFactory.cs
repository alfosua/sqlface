using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SqlFace.Core.Schemas;
using SqlFace.Parsing.SyntaxTrees;
using System.Collections;
using System.Reflection;
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

    public string SourceName => (SourceReference.ObjectPath.Value as IObjectIdentifier)?.Name
        ?? throw new NotImplementedException("Object path value is not supported");

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

    private async Task<Func<object, object>> CreatePipeInternalAsync(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor)
    {
        var sourceTypeName = sourceVisitor.SourceClrType.GetCSharpFullName();

        var columns = new PropertyProjectionAnalysis(sourceVisitor, selectQueryVisitor);
        
        var propsDefCode = new StringBuilder()
            .Append("\t")
            .AppendJoin("\n\t", columns.Select(x => $"public {x.TypeName} {x.OutputName} {{ get; set; }}"));

        var targetTypeName = new StringBuilder().AppendJoin("", sourceTypeName, Guid.NewGuid().ToString("n"));

        var classDefCode = new StringBuilder()
            .AppendFormat("public class {0}\n{{\n{1}\n}}\n", targetTypeName, propsDefCode);

        var funcTypeDefCode = new StringBuilder()
            .AppendFormat("Func<IEnumerable<{0}>, IEnumerable<{1}>>", sourceTypeName, targetTypeName);

        var propsAssignmentsCode = new StringBuilder()
            .Append("\t")
            .AppendJoin("\n\t", columns.Select(x => $"{x.OutputName} = x.{x.TargetName},"));

        var functionDefCode = new StringBuilder()
            .AppendFormat(
                "{0} pipe = (source) => source.Select(x => new {1}\n{{\n{2}\n}});\n",
                funcTypeDefCode,
                targetTypeName,
                propsAssignmentsCode);

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

    public PropertyProjectionAnalysis(ISourceVisitor sourceVisitor, ISelectQueryVisitor selectQueryVisitor)
    {
        _sourceProps = sourceVisitor.TypeProperties;
        _selectQueryVisitor = selectQueryVisitor;
    }

    public IEnumerator<PropertyProjection> GetEnumerator()
    {
        var columns = _selectQueryVisitor.TupleSelection.Atoms
            .SelectMany(x => AnalyzeAtom(x, _sourceProps));

        return columns.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private IEnumerable<PropertyProjection> AnalyzeAtom(ITupleSelectionAtom atom, IReadOnlyCollection<ISqlFaceObjectProperty> sourceProps)
    {
        var (targetPath, outputPath) = atom switch
        {
            var a when a is ITupleSelectionExpression expr => (MapExpressionToObjectPath(expr.Expression), expr.OutputPath),
            var a when a is ITupleSelectionAssignment assig => (MapExpressionToObjectPath(assig.Expression), assig.OutputPath),
            _ => throw new NotImplementedException("Tuple selection atom is not supported"),
        };

        var targets = ComputeTargets(targetPath);

        var outputNames = ComputeOutputNames(targetPath, outputPath);

        var zip = targets.Zip(outputNames).Select(x => new { Target = x.First, OutputName = x.Second })
            .Select(x => new PropertyProjection(x.Target.CSharpName, x.OutputName ?? x.Target.CSharpName, x.Target.CSharpTypeName));

        return zip;
    }

    private IEnumerable<PropertyTarget> ComputeTargets(IObjectPath targetPath) => targetPath.Value switch
    {
        var p when p is IObjectWildcard ow => _sourceProps
            .Select(x => CreatePropertyTargetFrom(x)),

        var p when p is IObjectIdentifier oi => _sourceProps
            .Where(x => x.Identifier.Name == oi.Name)
            .Select(x => CreatePropertyTargetFrom(x))
            .Take(1),

        _ => throw new NotImplementedException("Object path value is not supported"),
    };

    private IEnumerable<string?> ComputeOutputNames(IObjectPath targetPath, IObjectPath? outputPath) => (targetPath.Value, outputPath?.Value) switch
    {
        var (tp, op) when tp is IObjectIdentifier => ComputeOutputNamesWhenTargetIsIdentifier(op),
        
        var (tp, op) when tp is IObjectWildcard => ComputeOutputNamesWhenTargetIsWildcard(op),
        
        _ => throw new NotImplementedException("Object path value is not supported"),
    };

    private IEnumerable<string?> ComputeOutputNamesWhenTargetIsIdentifier(IObjectPathValue? objectPathValue) => objectPathValue switch
    {
        var p when p is IObjectWildcard or null => new string?[] { null },

        var p when p is IObjectIdentifier oi => new[] { oi.Name },

        _ => throw new NotImplementedException("Object path value for output is not supported"),
    };

    private IEnumerable<string?> ComputeOutputNamesWhenTargetIsWildcard(IObjectPathValue? objectPathValue) => objectPathValue switch
    {
        var p when p is IObjectWildcard or null => _sourceProps.Select(x => (string?)null),

        var p when p is IObjectIdentifier =>
            throw new NotImplementedException("Object identifier for grouping wildcard in outputs is not supported yet"),

        _ => throw new NotImplementedException("Object path value for output is not supported"),
    };

    private PropertyTarget CreatePropertyTargetFrom(ISqlFaceObjectProperty prop)
    {
        var identifiableName = prop.Identifier.Name;
        var csharpName = prop.Property.Name;
        var csharpTypeName = prop.Property.PropertyType.GetCSharpFullName();
        return new(identifiableName, csharpName, csharpTypeName);
    }

    private IObjectPath MapExpressionToObjectPath(IExpression expression) =>
        expression as IObjectPath ?? throw new NotImplementedException("Expression is not supported");

    private record PropertyTarget(string IdentifiableName, string CSharpName, string CSharpTypeName);
}

public record PropertyProjection(string TargetName, string OutputName, string TypeName);
