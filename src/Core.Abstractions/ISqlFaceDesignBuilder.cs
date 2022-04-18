using SqlFace.Core.Schemas;

namespace SqlFace.Core;

public interface ISqlFaceSchemaBuilder : ISqlFaceSchemaDesigner
{
    ISqlFaceSchemaDom Build();
}
