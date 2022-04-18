using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Core;

public interface ISqlFaceRunner
{
    Task<Dictionary<string, object>> RunAsync(ISyntaxTree syntaxTree);
}
