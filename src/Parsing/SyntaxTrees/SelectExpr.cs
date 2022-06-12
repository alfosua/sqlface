namespace SqlFace.Parsing.SyntaxTrees;

public record SyntaxTree : ISyntaxTree
{
    public SyntaxTree(IEnumerable<IStatement> statements)
    {
        Statements = statements;
    }

    public IPayload? Payload { get; set; }
    public ISecrets? Secrets { get; set; }
    public IEnumerable<IStatement> Statements { get; set; }
}

public record StringLiteral(string Value) : IStringLiteral;
public record BooleanLiteral(bool Value) : IBooleanLiteral;
public record IntegerLiteral(int Value) : IIntegerLiteral;
public record FloatingPointLiteral(double Value) : IFloatingPointLiteral;

public record AdditionOperator(IExpression Left, IExpression Right) : IAdditionOperator { }
public record SubtractionOperator(IExpression Left, IExpression Right) : ISubtractionOperator { }
public record MultiplicationOperator(IExpression Left, IExpression Right) : IMultiplicationOperator { }
public record DivisionOperator(IExpression Left, IExpression Right) : IDivisionOperator { }
public record ModuloOperator(IExpression Left, IExpression Right) : IModuloOperator { }
public record NegationOperator(IExpression Target) : INegationOperator { }

public record NotOperator(IExpression Target) : INotOperator { }
public record AndOperator(IExpression Left, IExpression Right) : IAndOperator { }
public record OrOperator(IExpression Left, IExpression Right) : IOrOperator { }

public record Invocation(IExpression Target, ICollection<IInvocationParameter> Parameters) : IInvocation;
public record InvocationParameter(IExpression Value, INameItem? Name = null) : IInvocationParameter;
public record Pipeline(IExpression Value, IExpression Curry) : IPipeline;

public class SelectQuery : ISelectQuery
{
    public SelectQuery(ISelectable selectable, ISelection selection)
    {
        Selectable = selectable;
        Selection = selection;
        Binds = Enumerable.Empty<IBind>();
        Includes = Enumerable.Empty<IInclude>();
        Joins = Enumerable.Empty<IJoin>();
        Orderings = Enumerable.Empty<IOrdering>();
        Top = new All();
    }

    public ISelectable Selectable { get; set; }
    public ISelection Selection { get; set; }
    public IEnumerable<IBind> Binds { get; set; }
    public IEnumerable<IInclude> Includes { get; set; }
    public IEnumerable<IJoin> Joins { get; set; }
    public IPredicate? Predicate { get; set; }
    public IAggrupation? Aggrupation { get; set; }
    public IEnumerable<IOrdering> Orderings { get; set; }
    public ITop Top { get; set; }
    public ILimit? Limit { get; set; }
    public IOffset? Offset { get; set; }
    public IPagination? Pagination { get; set; }
}

public record TupleSelection(IEnumerable<ITupleSelectionAtom> Atoms) : ITupleSelection;
public record TupleSelectionExpression(IExpression Expression, INamePath? OutputPath = null) : ITupleSelectionExpression;
public record TupleSelectionAssignment(INamePath OutputPath, IExpression Expression) : ITupleSelectionAssignment;

public record SourceReference(INamePath Path) : ISourceReference;

public record NamePath(IEnumerable<INameItem> Sequence) : INamePath;
public record NameItem(string Identifier) : INameItem;

public record Wildcard(int Id) : IWildcard;

public record Limit(int Quantity) : ILimit;
public record Offset(int Quantity) : IOffset;
public record Pagination(int Page, int Size) : IPagination;

public record All : IAll;
public record First : IFirst;
public record Last : ILast;
public record One : IOne;
public record AtPosition(int Position) : IAtPosition;
public record TopByQuantity(int Quantity) : ITopByQuantity;
public record TopByPercentage(double Percentage) : ITopByPercentage;

public record Ordering(IExpression Expression, OrderDirection Direction) : IOrdering;
