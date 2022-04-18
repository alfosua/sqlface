using System.Reflection;

namespace SqlFace.Core.Schemas;

public interface ISqlFaceSchemaDom
{
    List<ISqlFaceSource> Sources { get; }
}

public interface ISqlFaceSource : ISqlFaceBulkIdentifiable
{
    ISqlFaceType Type { get; }

    MethodInfo? Resolver { get; }
}

public interface ISqlFaceType
{
}

public interface ISqlFaceDataTokenizable
{
    SqlFaceDataToken Token { get; }
}

public enum SqlFaceDataToken
{
    Undefined,
    Null,
    Atom,
    Boolean,
    Number,
    String,
}

public interface ISqlFaceObjectifiable
{
    IReadOnlyCollection<ISqlFaceObjectProperty> Properties { get; }
}

public interface ISqlFaceArrayable
{
    ISqlFaceType ElementType { get; }
}

public interface ISqlFaceGenericable
{
    IReadOnlyCollection<ISqlFaceGenericArgument> Arguments { get; }
}

public interface ISqlFaceNullable
{
    ISqlFaceType UnderlyingType { get; }
}

public interface ISqlFaceClrSourcifiable
{
    Type SourcingClrType { get; }
}

public interface ISqlFaceCallable
{
    IReadOnlyCollection<ISqlFaceCallableArgument> Arguments { get; }

    ISqlFaceType ReturnType { get; }
}

public interface ISqlFaceIdentifiable
{
    ISqlFaceAtom Identifier { get; }
}

public interface ISqlFaceBulkIdentifiable
{
    IReadOnlyCollection<ISqlFaceAtom> Identifiers { get; }
}

public interface ISqlFaceObjectProperty : ISqlFaceIdentifiable
{
    PropertyInfo Property { get; }

    ISqlFaceType Type { get; }
}

public interface ISqlFaceAtom
{
    string Name { get; }

    ISqlFaceAtom? Parent { get; }
}

public interface ISqlFaceGenericArgument
{
    ISqlFaceType Type { get; }
}

public interface ISqlFaceCallableArgument
{
    ISqlFaceType Type { get; }

    string Name { get; }
}
