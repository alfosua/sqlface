using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Core
{
    public interface ISqlFaceLinqFactory
    {
        Task<object> CreateFromAsync(ISelectQuery selectQuery);
    }
}