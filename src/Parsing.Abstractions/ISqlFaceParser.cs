using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Parsing;

public interface ISqlFaceParser
{
    ISyntaxTree Parse(string code);
}
