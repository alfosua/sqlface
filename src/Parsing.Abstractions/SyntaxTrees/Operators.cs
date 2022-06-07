namespace SqlFace.Parsing.SyntaxTrees;

public interface IOperator : IExpression { }
public interface IUnaryOperator : IOperator
{
    IExpression Target { get; }
}
public interface IBinaryOperator : IOperator
{
    IExpression Left { get; }
    IExpression Right { get; }
}

public interface IAdditionOperator : IBinaryOperator { }
public interface ISubtractionOperator : IBinaryOperator { }
public interface IMultiplicationOperator : IBinaryOperator { }
public interface IDivisionOperator : IBinaryOperator { }
public interface IModuloOperator : IBinaryOperator { }
public interface INegationOperator : IUnaryOperator { }

public interface INotOperator : IUnaryOperator { }
public interface IAndOperator : IBinaryOperator { }
public interface IOrOperator : IBinaryOperator { }
