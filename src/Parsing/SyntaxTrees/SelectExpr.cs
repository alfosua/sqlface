namespace SqlFace.Parsing.SyntaxTrees;

public class SyntaxTree : ISyntaxTree
{
    public SyntaxTree(IEnumerable<IStatement> statements)
    {
        Statements = statements;
    }

    public IPayload? Payload { get; set; }

    public ISecrets? Secrets { get; set; }

    public IEnumerable<IStatement> Statements { get; set; }
}

public class SelectQuery : ISelectQuery
{
    public SelectQuery(ISelectable selectable, ISelection selection)
    {
        Selectable = selectable;
        Selection = selection;
        Binds = new IBind[] { };
        Includes = new IInclude[] { };
        Joins = new IJoin[] { };
    }

    public ISelectable Selectable { get; set; }
    public ISelection Selection { get; set; }
    public IEnumerable<IBind> Binds { get; set; }
    public IEnumerable<IInclude> Includes { get; set; }
    public IEnumerable<IJoin> Joins { get; set; }
    public IPredicate? Predicate { get; set; }
    public IAggrupation? Aggrupation { get; set; }
    public IOrdering? Ordering { get; set; }
}

public record TupleSelection : ITupleSelection
{
    public TupleSelection(IEnumerable<ITupleSelectionAtom> atoms)
    {
        Atoms = atoms;
    }

    public IEnumerable<ITupleSelectionAtom> Atoms { get; set; }
}

public record TupleSelectionExpression : ITupleSelectionExpression
{
    public TupleSelectionExpression(IExpression expression)
    {
        Expression = expression;
    }

    public IExpression Expression { get; set; }

    public IObjectPath? OutputPath { get; set; }
}

public record TupleSelectionAssignment : ITupleSelectionAssignment
{
    public TupleSelectionAssignment(IObjectPath outputPath, IExpression expression)
    {
        OutputPath = outputPath;
        Expression = expression;
    }

    public IObjectPath OutputPath { get; set; }

    public IExpression Expression { get; set; }
}

public record SourceReference : ISourceReference
{
    public SourceReference(IObjectPath objectPath)
    {
        ObjectPath = objectPath;
    }

    public IObjectPath ObjectPath { get; set; }
}

public record ObjectPath : IObjectPath
{
    public ObjectPath(IObjectPathValue value)
    {
        Value = value;
    }

    public IObjectPathValue Value { get; set; }

    public IObjectPath? Child { get; set; }
}

public record ObjectIdentifier(string Name) : IObjectIdentifier;

public record ObjectWildcard(int Level) : IObjectWildcard;
