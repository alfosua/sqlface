using SqlFace.Core.Schemas;
using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlFace.Core;

public class SqlFaceSchemaBuilder : ISqlFaceSchemaBuilder
{
    private readonly List<Type> _sources = new();
    private readonly List<Type> _resolvers = new();

    public ISqlFaceSchemaDom Build()
    {
        var resolverMapQuery = _resolvers
            .SelectMany(x => x.GetMethods())
            .Where(x => x.IsPublic
                && (typeof(IEnumerable).IsAssignableFrom(x.ReturnType)
                    || (typeof(Task).IsAssignableFrom(x.ReturnType)
                        && typeof(IEnumerable).IsAssignableFrom(x.ReturnType.GetGenericArguments().First()))))
            .Select(x => new
            {
                NameMatch = Regex.Match(x.Name, @"(?<=(?i)get)((?!(?i)async$).)*"),
                MethodInfo = x,
            })
            .Where(x => x.NameMatch.Success);

        var resolverMapByName = resolverMapQuery
            .ToDictionary(x => x.NameMatch.Value, x => x.MethodInfo);

        var domSources = _sources
            .Select(x => CreateSqlFaceSourceFromType(x, resolverMapByName));

        return new SqlFaceSchemaDom
        {
            Sources = domSources.ToList(),
        };
    }

    private ISqlFaceSource CreateSqlFaceSourceFromType(Type type, IReadOnlyDictionary<string, MethodInfo> resolversMap)
    {
        var names = new[] { type.Name, type.Name + "s" };
        var identifiers = names
            .Select(x => new SqlFaceAtom(JsonNamingPolicy.CamelCase.ConvertName(x)))
            .ToList();
        var resolver = names
            .Select(x => resolversMap.GetValueOrDefault(x))
            .FirstOrDefault(x => x is not null);
        var sqlFaceType = MapClrTypeToSqlFaceType(type);

        return new SqlFaceSource(identifiers, sqlFaceType, resolver);
    }

    private ISqlFaceType MapClrTypeToSqlFaceType(Type type) => type switch
    {
        var t when t == typeof(string) => SqlFaceBasicType.String,
        
        var t when t == typeof(bool) => SqlFaceBasicType.Boolean,

        var t when IsNumberType(t) => SqlFaceBasicType.Number,

        var t when IsArrayType(t) => CreateSqlFaceArrayTypeFromClrType(t),
        
        _ => CreateSqlFaceObjectTypeFromClrType(type),
    };

    private bool IsNumberType(Type type) => type == typeof(byte)
        && type == typeof(ushort)
        && type == typeof(uint)
        && type == typeof(ulong)
        && type == typeof(sbyte)
        && type == typeof(short)
        && type == typeof(int)
        && type == typeof(long)
        && type == typeof(float)
        && type == typeof(double)
        && type == typeof(decimal);

    private bool IsArrayType(Type type) => type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type);

    private ISqlFaceType CreateSqlFaceArrayTypeFromClrType(Type clrType)
    {
        var elementClrType = clrType.GetGenericArguments().Last();
        var sqlFaceType = new SqlFaceArrayType(clrType, MapClrTypeToSqlFaceType(elementClrType));
        return sqlFaceType;
    }

    private ISqlFaceType CreateSqlFaceObjectTypeFromClrType(Type clrType)
    {
        var sqlFaceProps = clrType
            .GetProperties()
            .Select(x => CreateSqlFaceObjectPropertyFrom(x))
            .ToList();
        var sqlFaceType = new SqlFaceObjectType(clrType, sqlFaceProps);
        return sqlFaceType;
    }
    
    private ISqlFaceObjectProperty CreateSqlFaceObjectPropertyFrom(PropertyInfo prop)
    {
        var name = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
        var identifier = new SqlFaceAtom(name);
        var sqlFaceType = MapClrTypeToSqlFaceType(prop.PropertyType);

        return new SqlFaceObjectProperty(prop, identifier, sqlFaceType);
    }
    
    public ISqlFaceSchemaDesigner Resolver<T>()
    {
        _resolvers.Add(typeof(T));
        return this;
    }

    public ISqlFaceSchemaDesigner Source<T>()
    {
        _sources.Add(typeof(T));
        return this;
    }
}
