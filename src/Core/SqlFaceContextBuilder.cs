using SqlFace.Core.Schemas;

namespace SqlFace.Core;

public class SqlFaceContextBuilder : ISqlFaceContextBuilder
{
    private readonly List<ISqlFaceSchemaDom> schemas = new();

    public ISqlFaceContextBuilder WithSchema(Action<ISqlFaceSchemaDesigner> decorator)
    {
        var builder = new SqlFaceSchemaBuilder();

        decorator.Invoke(builder);

        var schemaDom = builder.Build();
        schemas.Add(schemaDom);

        return this;
    }

    public ISqlFaceContextBuilder UseSchema<TSchema>(Action<ISqlFaceSchemaDesigner>? decorator = null)
        where TSchema : class, ISqlFaceSchema
    {
        var builder = new SqlFaceSchemaBuilder();
        var design = Activator.CreateInstance<TSchema>();

        design.Describe(builder);
        decorator?.Invoke(builder);

        var schemaDom = builder.Build();
        schemas.Add(schemaDom);

        return this;
    }

    public ISqlFaceContext<TTopic> Build<TTopic>()
    {
        return new SqlFaceContext<TTopic>
        {
            Schemas = schemas,
        };
    }
}
