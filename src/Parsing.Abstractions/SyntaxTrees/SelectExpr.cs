namespace SqlFace.Parsing.SyntaxTrees;

public interface ISyntaxTree
{
    IPayload? Payload { get; }
    ISecrets? Secrets { get; }
    IEnumerable<IStatement> Statements { get; }
}

public interface ISyntaxElement { }
public interface IPayload : ISyntaxElement { }
public interface ISecrets : ISyntaxElement { }
public interface IStatement : ISyntaxElement { }
public interface IExpression : ISyntaxElement { }

public interface IVariable : IStatement { }
public interface IConstant : IStatement { }
public interface IQuery : IStatement { }

public interface ILiteral : IExpression { }
public interface ILiteral<T> : ILiteral
{
    T Value { get;  }
}
public interface IStringLiteral : ILiteral<string> { }
public interface IBooleanLiteral : ILiteral<bool> { }
public interface INumberLiteral<T> : ILiteral<T> { }
public interface IIntegerLiteral<T> : INumberLiteral<int> { }
public interface IFloatingPointLiteral<T> : INumberLiteral<double> { }

public interface IVariableReference : IExpression
{
    INamePath NamePath { get; }
}
public interface ISourceReference : IExpression
    , ISelectable
    , IInsertable
    , IUpdatable
{
    INamePath Path { get; }
}

public interface IWildcard : IExpression
{
    int Id { get; }
}

public interface INamePath : IExpression
{
    IEnumerable<INameItem> Sequence { get; }
}
public interface INameItem
{
    string Identifier { get; }
}

public interface IExpansionPath : IExpression
{
    IEnumerable<INameItem> Sequence { get; }
}
public interface IExpansionItem { }
public interface IExpansionName : IExpansionItem
{
    INameItem Name { get; set; }
}
public interface IExpansionWildcard : IExpansionItem
{
    IWildcard Wildcard { get; set; }
}

public interface IInsertQuery
{
    IInsertable Insertable { get; }

    IInsertion Insertion { get; }
}

public interface IInsertable
{
}

public interface IInsertion
{
}

public interface IOneValueInsertion : IInsertion
{
}

public interface IManyValuesInsertion : IInsertion
{
}

public interface ISelectedInsertion : IInsertion
{
}

public interface IUpdateQuery : IQuery
    , IMutable
    , IUpdatable
{
    IUpdatable Updatable { get; }
}

public interface IMutable
{
    IMutation Mutation { get; }
}

public interface IUpdatable
{
}

public interface IMutation
{
}

public interface IDeleteQuery : IQuery
{
}

public interface IPredicatedDeleteQuery : IDeleteQuery
    , IBindingInAble
    , IIncludingInAble
    , IJoiningInAble
    , IPredicable
{
}

public interface IParametizedDeleteQuery : IDeleteQuery
{
}

public interface ISelectQuery : IQuery
    , ISelectable
    , ITopable
    , IBindingInAble
    , IIncludingInAble
    , IJoiningInAble
    , IPredicable
    , IAggrupable
    , ILimitable
    , IOffsettable
    , IPageable
    , IOrderable
{
    ISelectable Selectable { get; }

    ISelection Selection { get; }
}

public interface IIncludingInAble
{
    IEnumerable<IInclude> Includes { get; }
}

public interface IJoiningInAble
{
    IEnumerable<IJoin> Joins { get; }
}

public interface IBindingInAble
{
    IEnumerable<IBind> Binds { get; }
}

public interface IPredicable
{
    IPredicate? Predicate { get; }
}

public interface IOrderable
{
    IEnumerable<IOrdering> Orderings { get; }
}

public interface IAggrupable
{
    IAggrupation? Aggrupation { get; }
}

public interface ITopable
{
    ITop Top { get; }
}

public interface ITop { }
public interface IAll : ITop { }
public interface IOne : ITop { }
public interface IFirst : ITop { }
public interface ILast : ITop { }
public interface IAtPosition : ITop
{
    int Position { get; }
}
public interface ITopByQuantity : ITop
{
    int Quantity { get; }
}
public interface ITopByPercentage : ITop
{
    double Percentage { get; }
}

public interface ILimitable
{
    ILimit? Limit { get; }
}

public interface ILimit
{
    int Quantity { get; }
}

public interface IOffsettable
{
    IOffset? Offset { get; }
}

public interface IOffset
{
    int Quantity { get; }
}

public interface IPageable
{
    IPagination? Pagination { get; }
}

public interface IPagination
{
    int Page { get; }
    int Size { get; }
}

public interface ISelectable
{
}

public interface ISelection
{
}

public interface ITupleSelection : ISelection
{
    IEnumerable<ITupleSelectionAtom> Atoms { get;  }
}

public interface ITupleSelectionAtom
{
}

public interface ITupleSelectionExpression : ITupleSelectionAtom
{
    IExpression Expression { get; }
    INamePath? OutputPath { get; }
}

public interface ITupleSelectionAssignment : ITupleSelectionAtom
{
    INamePath OutputPath { get; }
    IExpression Expression { get; }
}

public interface IMapSelection : ISelection
{
}

public interface IBind
{
}

public interface IInclude
{
}

public interface IJoin
{
}

public interface IPredicate
{
}

public interface IAggrupation
{
}

public interface IOrdering
{
    IExpression Expression { get; }
    
    OrderDirection Direction { get; }
}

public enum OrderDirection
{
    Ascending,
    Descending,
}
