namespace SqlFace.Core;

public interface ISqlFaceSchemaDesigner
{
    ISqlFaceSchemaDesigner Source<T>();
    ISqlFaceSchemaDesigner Resolver<T>();
}
