namespace SqlFace.Parsing.SyntaxTrees;

public interface ISyntaxTree
{
    IPayload? Payload { get; }

    ISecrets? Secrets { get; }

    IEnumerable<IStatement> Statements { get; set; }
}

public interface IStatement : ISyntaxElement
{
}

public interface IQuery : IStatement
{
}

public interface IReference : IExpression
{
}

public interface IPayload : ISyntaxElement
{
}

public interface ISecrets : ISyntaxElement
{
}

public interface IVariable : IStatement
{
}

public interface IConstant : IStatement
{
}

public interface ISyntaxElement
{
}

public interface ILiteral : IExpression
{
}

public interface ILiteral<T> : ILiteral
{
}

public interface IStringLiteral : ILiteral<string>
{
}

public interface INumberLiteral<T> : ILiteral<T>
{
}

public interface ISourceReference : IReference
    , ISelectable
    , IInsertable
    , IUpdatable
{
    IObjectPath ObjectPath { get; }
}

public interface ISourceValueReference : IReference
{
}

public interface IVariableReference : IReference
{
}

public interface IInsertQuery
{
    public IInsertable Insertable { get; set; }

    public IInsertion Insertion { get; set; }
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
    public IUpdatable Updatable { get; set; }
}

public interface IMutable
{
    public IMutation Mutation { get; set; }
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
    , IBindingInAble
    , IIncludingInAble
    , IJoiningInAble
    , IPredicable
    , IAggrupable
    , IOrderable
{
    public ISelectable Selectable { get; set; }

    public ISelection Selection { get; set; }
}

public interface IIncludingInAble
{
    public IEnumerable<IInclude> Includes { get; set; }
}

public interface IJoiningInAble
{
    public IEnumerable<IJoin> Joins { get; set; }
}

public interface IBindingInAble
{
    public IEnumerable<IBind> Binds { get; set; }
}

public interface IPredicable
{
    public IPredicate Predicate { get; set; }
}

public interface IOrderable
{
    public IOrdering Ordering { get; set; }
}

public interface IAggrupable
{
    public IAggrupation Aggrupation { get; set; }
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

    IObjectPath? OutputPath { get; }
}

public interface ITupleSelectionAssignment : ITupleSelectionAtom
{
    IObjectPath OutputPath { get; }

    IExpression Expression { get; }
}

public interface IExpression : ISyntaxElement
{
}

public interface IMapSelection : ISelection
{
}

public interface IObjectPath : IReference
{
    public IObjectPathValue Value { get; }

    public IObjectPath? Child { get; }
}

public interface IObjectPathValue
{
}

public interface IObjectIdentifier : IObjectPathValue
{
    public string Name { get; }
}

public interface IObjectWildcard : IObjectPathValue
{
    public int Level { get; }
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
}
