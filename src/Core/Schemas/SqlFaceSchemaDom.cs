using System.Reflection;

namespace SqlFace.Core.Schemas;

public class SqlFaceSchemaDom : ISqlFaceSchemaDom
{
    public SqlFaceSchemaDom()
    {
        Sources = new();
    }

    public List<ISqlFaceSource> Sources { get; set; }
}

public class SqlFaceSource : ISqlFaceSource
{
    public SqlFaceSource(IReadOnlyCollection<ISqlFaceAtom> identifiers, ISqlFaceType type, MethodInfo? resolver = null)
    {
        Identifiers = identifiers;
        Type = type;
        Resolver = resolver;
    }

    public IReadOnlyCollection<ISqlFaceAtom> Identifiers { get; }

    public ISqlFaceType Type { get; }

    public MethodInfo? Resolver { get; }
}

public record SqlFaceAtom(string Name, ISqlFaceAtom? Parent = null) : ISqlFaceAtom;

public record SqlFaceObjectType : ISqlFaceType, ISqlFaceObjectifiable, ISqlFaceClrSourcifiable
{
    public SqlFaceObjectType(Type sourceClrType, IReadOnlyCollection<ISqlFaceObjectProperty> properties)
    {
        SourcingClrType = sourceClrType;
        Properties = properties;
    }

    public Type SourcingClrType { get; }

    public IReadOnlyCollection<ISqlFaceObjectProperty> Properties { get; }
}

public record SqlFaceArrayType : ISqlFaceType, ISqlFaceClrSourcifiable, ISqlFaceArrayable
{
    public SqlFaceArrayType(Type sourceClrType, ISqlFaceType elementType)
    {
        SourcingClrType = sourceClrType;
        ElementType = elementType;
    }

    public Type SourcingClrType { get; }

    public ISqlFaceType ElementType { get; }
}

public record SqlFaceObjectProperty(PropertyInfo Property, ISqlFaceAtom Identifier, ISqlFaceType Type) : ISqlFaceObjectProperty;

public record SqlFaceBasicType
    : ISqlFaceType
    , ISqlFaceIdentifiable
    , ISqlFaceDataTokenizable
{
    public static readonly SqlFaceBasicType Undefined = new SqlFaceBasicType(new SqlFaceAtom("undefined"), SqlFaceDataToken.Undefined);
    public static readonly SqlFaceBasicType Null = new SqlFaceBasicType(new SqlFaceAtom("null"), SqlFaceDataToken.Null);
    public static readonly SqlFaceBasicType Boolean = new SqlFaceBasicType(new SqlFaceAtom("boolean"), SqlFaceDataToken.Boolean);
    public static readonly SqlFaceBasicType Number = new SqlFaceBasicType(new SqlFaceAtom("number"), SqlFaceDataToken.Number);
    public static readonly SqlFaceBasicType String = new SqlFaceBasicType(new SqlFaceAtom("string"), SqlFaceDataToken.String);

    public SqlFaceBasicType(ISqlFaceAtom identifier, SqlFaceDataToken token)
    {
        Identifier = identifier;
        Token = token;
    }

    public ISqlFaceAtom Identifier { get; init; }

    public SqlFaceDataToken Token { get; init; }

}
