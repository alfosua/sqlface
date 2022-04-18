using SqlFace.Core.Schemas;

namespace SqlFace.Core;

public class SqlFaceContext<TTopic> : SqlFaceContext, ISqlFaceContext<TTopic> { }

public class SqlFaceContext : ISqlFaceContext
{
    public SqlFaceContext()
    {
        Schemas = new();
    }

    public List<ISqlFaceSchemaDom> Schemas { get; set; }
}
