using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Core;

public class SqlFaceRunner : ISqlFaceRunner
{
    private readonly ISqlFaceLinqFactory linqFactory;

    public SqlFaceRunner(ISqlFaceLinqFactory linqFactory)
    {
        this.linqFactory = linqFactory;
    }

    public async Task<Dictionary<string, object>> RunAsync(ISyntaxTree syntaxTree)
    {
        var data = new Dictionary<string, object>();
        
        foreach (var statement in syntaxTree.Statements)
        {
            if (statement is ISelectQuery selectQuery)
            {
                var sourceName = ((selectQuery.Selectable as ISourceReference)?
                    .ObjectPath.Value as IObjectIdentifier)?.Name
                    ?? throw new NotImplementedException("Selectable is not supported");

                var query = await linqFactory.CreateFromAsync(selectQuery);

                data.Add(sourceName, query);
            }
        }

        return data;
    }
}
