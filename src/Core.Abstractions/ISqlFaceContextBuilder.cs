namespace SqlFace.Core;

public interface ISqlFaceContextBuilder
{
    ISqlFaceContext<TTopic> Build<TTopic>();

    ISqlFaceContextBuilder WithSchema(Action<ISqlFaceSchemaDesigner> decorator);

    ISqlFaceContextBuilder UseSchema<TDesign>(Action<ISqlFaceSchemaDesigner>? decorator = null)
        where TDesign : class, ISqlFaceSchema;
}
