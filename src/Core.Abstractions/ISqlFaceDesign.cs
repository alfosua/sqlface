namespace SqlFace.Core;

public interface ISqlFaceSchema
{
    void Describe(ISqlFaceSchemaDesigner builder);
}
