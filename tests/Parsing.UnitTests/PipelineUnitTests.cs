using Nom;
using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Parsing.UnitTests;

public class PipelineUnitTests
{
    [Fact]
    public void Test1()
    {
        var expected = new Pipeline(new IntegerLiteral(2), new NamePath(new[] { new NameItem("square") }));
        var parser = new PipelineParser();

        var (_, actual) = parser.Parse("2 |> square");

        expected.AssertWith(actual);
    }
}

public static class AssertExtensions
{
    public static IExpression AssertWith(this IExpression expected, IExpression actual) => (expected, actual) switch
    {
        (IPipeline exp, IPipeline act) => exp.AssertWith(act),
        (INamePath exp, INamePath act) => exp.AssertWith(act),
        _ => throw new NotImplementedException(),
    };
    
    public static IPipeline AssertWith(this IPipeline expected, IPipeline actual)
    {
        Assert.Equal(expected.Value, actual.Value);

        expected.Curry.AssertWith(actual.Curry);

        return expected;
    }

    public static INamePath AssertWith(this INamePath expected, INamePath actual)
    {
        Assert.Equal(expected.Sequence.ToList(), actual.Sequence.ToList());
        
        return expected;
    }
}