using Nom;
using Nom.Branches;
using Nom.Characters;
using Nom.Collections;
using Nom.Combinators;
using Nom.Sequences;
using Nom.Strings;
using SqlFace.Parsing.SyntaxTrees;

namespace SqlFace.Parsing;

public class SqlFaceParser : ISqlFaceParser
{
    public ISyntaxTree Parse(string code)
    {
        var parser = SyntaxTree();
        var result = parser.Parse(code);
        return result.Output;
    }

    public IParser<StringParsable, ISyntaxTree> SyntaxTree()
    {
        var selectQuery = Map.Create(new SelectQueryParser(), x => x as IStatement);
        var statementAlternation = Alternation.Create(new[] { selectQuery });
        var statements = ManyOrNone.Create(statementAlternation);
        var syntaxTree = Map.Create(statements,
            (ICollection<IStatement> statements) => new SyntaxTree(statements) as ISyntaxTree);
        return syntaxTree;
    }
}

public class ExpressionParser : IParser<StringParsable, IExpression>
{
    public IResult<StringParsable, IExpression> Parse(StringParsable input)
    {
        var arithmeticMap = AsExpression(new ArithmeticExpressionParser());
        var literalMap = AsExpression(new LiteralParser());
        var namePathMap = AsExpression(new NamePathParser());
        var wildcardMap = AsExpression(new WildcardParser());

        var alternation = Alternation.Create(new[] { arithmeticMap, literalMap, wildcardMap, namePathMap });

        return alternation.Parse(input);
    }

    private IParser<StringParsable, IExpression> AsExpression<T>(IParser<StringParsable, T> parser)
        where T : IExpression
    {
        return Map.Create(parser, x => x as IExpression);
    }
}

public class LiteralParser : IParser<StringParsable, ILiteral>
{
    public IResult<StringParsable, ILiteral> Parse(StringParsable input)
    {
        var booeleanLiteral = ValueToLiteral(PrimitiveValues.BooleanValueByKeyword(), x => new BooleanLiteral(x));
        var stringLiteral = ValueToLiteral(PrimitiveValues.StringValue(), x => new StringLiteral(x));
        var floatingPointLiteral = ValueToLiteral(PrimitiveValues.NonNegativeDoubleValue(), x => new FloatingPointLiteral(x));
        var integerLiteral = ValueToLiteral(PrimitiveValues.NonNegativeIntegerValue(), x => new IntegerLiteral(x));

        var alternation = Alternation.Create(new[] { booeleanLiteral, stringLiteral, floatingPointLiteral, integerLiteral });

        return alternation.Parse(input);
    }

    private IParser<StringParsable, ILiteral> ValueToLiteral<T>(IParser<StringParsable, T> parser, Func<T, ILiteral> factory)
    {
        return Map.Create(parser, x => factory(x));
    }
}

public class ArithmeticFactorParser : IParser<StringParsable, IExpression>
{
    public IResult<StringParsable, IExpression> Parse(StringParsable input)
    {
        var literalMap = AsExpression(new LiteralParser());
        var namePathMap = AsExpression(new NamePathParser());
        var wildcardMap = AsExpression(new WildcardParser());

        var alternation = Alternation.Create(new[] { literalMap, wildcardMap, namePathMap });

        return alternation.Parse(input);
    }

    private IParser<StringParsable, IExpression> AsExpression<T>(IParser<StringParsable, T> parser)
        where T : IExpression
    {
        return Map.Create(parser, x => x as IExpression);
    }
}

public class ArithmeticTermParser : IParser<StringParsable, IExpression>
{
    public IResult<StringParsable, IExpression> Parse(StringParsable input)
    {
        var times = Utilities.Symbol('*');
        var by = Utilities.Symbol('/');
        var rem = Utilities.Symbol('%');
        var symbol = Alternation.Create(new[] { times, by, rem });

        var initial = new ArithmeticFactorParser().Parse(input);
        var rightHandSides = Pair.Create(symbol, new ArithmeticFactorParser());

        var fold = FoldManyOrNone.Create(rightHandSides, initial.Output, (accum, next) => (string)next.Item1 switch
        {
            "*" => new MultiplicationOperator(accum, next.Item2),
            "/" => new DivisionOperator(accum, next.Item2),
            "%" => new ModuloOperator(accum, next.Item2),
            _ => throw new Exception("Invalid passed operator"),
        });

        return fold.Parse(initial.Remainder);
    }
}

public class ArithmeticExpressionParser : IParser<StringParsable, IExpression>
{
    public IResult<StringParsable, IExpression> Parse(StringParsable input)
    {
        var plus = Utilities.Symbol('+');
        var minus = Utilities.Symbol('-');
        var symbol = Alternation.Create(new[] { plus, minus });
        
        var initial = new ArithmeticTermParser().Parse(input);
        var rightHandSides = Pair.Create(symbol, new ArithmeticTermParser());

        var fold = FoldManyOrNone.Create(rightHandSides, initial.Output, Aggregation);

        return fold.Parse(initial.Remainder);
    }

    private IExpression Aggregation(IExpression accum, (StringParsable op, IExpression expr) next) => (string)next.op switch
    {
        "+" => new AdditionOperator(accum, next.expr),
        "-" => new SubtractionOperator(accum, next.expr),
        _ => throw new Exception("Invalid passed operator"),
    };
}

public class NamePathParser : IParser<StringParsable, INamePath>
{
    public IResult<StringParsable, INamePath> Parse(StringParsable input)
    {
        var nameItemRawData = Pair.Create(SatisfiedBy.Create(c => char.IsLetter(c) || c == '_'), AlphanumericsOrNone.Create());
        var nameItemMap = Map.Create(
            parser: nameItemRawData,
            mapper: x => new NameItem(x.Item1 + x.Item2) as INameItem);

        var namePathList = SeparatedList.Create(Utilities.Symbol('.'), nameItemMap);
        var namePathMap = Map.Create(namePathList, x => new NamePath(x) as INamePath);

        return namePathMap.Parse(input);
    }
}

public class WildcardParser : IParser<StringParsable, IWildcard>
{
    public IResult<StringParsable, IWildcard> Parse(StringParsable input)
    {
        var map = Map.Create(
            parser: Many.Create(Character.Create('*')),
            mapper: levels => new Wildcard(levels.Count) as IWildcard);

        return map.Parse(input);
    }
}

public class SelectQueryParser : IParser<StringParsable, ISelectQuery>
{
    public IResult<StringParsable, ISelectQuery> Parse(StringParsable input)
    {
        var selectPrefix = Utilities.KeywordPrefix("select");

        var top = new SelectQueryTopParser();
        var selection = Utilities.MultispaceAfter(new SelectionParser());
        var source = SelectSource();

        var headerDataTuple = Preceded.Create(selectPrefix, Tup.Create((top, selection, source)));

        var orderings = Optional.Create(Utilities.MultispaceBefore(new OrderingsParser()));
        var offset = Optional.Create(Utilities.MultispaceBefore(new OffsetParser()));
        var limit = Optional.Create(Utilities.MultispaceBefore(new LimitParser()));
        var pagination = Optional.Create(Utilities.MultispaceBefore(new PaginationParser()));

        var bodyDataTuple = Tup.Create((orderings, offset, limit, pagination));

        var allDataTuple = Terminated.Create(Tup.Create((headerDataTuple, bodyDataTuple)), Utilities.StatementTermination());

        var selectQueryMap = Map.Create(allDataTuple, SelectQueryMap);

        return selectQueryMap.Parse(input);
    }

    private IParser<StringParsable, ISourceReference> SelectSource()
    {
        var fromPrefix = Utilities.KeywordPrefix("from");
        var precededSource = Preceded.Create(fromPrefix, new NamePathParser());
        var sourceMap = Map.Create(precededSource, x => new SourceReference(x) as ISourceReference);
        return sourceMap;
    }

    private ISelectQuery SelectQueryMap(
        ((ITop Top, ISelection Selection, ISourceReference Source) Header, (ICollection<IOrdering>? Orderings, IOffset? Offset, ILimit? Limit, IPagination? Pagination) Body) x) =>
        new SelectQuery(selection: x.Header.Selection, selectable: x.Header.Source)
        {
            Top = x.Header.Top,
            Limit = x.Body.Limit,
            Offset = x.Body.Offset,
            Pagination = x.Body.Pagination,
            Orderings = x.Body.Orderings
                ?? Enumerable.Empty<IOrdering>(),
        };
}

public class SelectionParser : IParser<StringParsable, ISelection>
{
    public IResult<StringParsable, ISelection> Parse(StringParsable input)
    {
        var tupleSelection = Map.Create(TupleSelection(), x => x as ISelection);

        return tupleSelection.Parse(input);
    }

    private IParser<StringParsable, ITupleSelection> TupleSelection()
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var asTag = TagIgnoringCase.Create("as");

        var selectionsSeparator = Utilities.Symbol(',');
        var assigmentSeparation = Utilities.Symbol('=');

        var selectionAssignmentRaw = SeparatedPair.Create(new NamePathParser(), assigmentSeparation, new ExpressionParser());
        var selectionAssignmentMap = Map.Create(selectionAssignmentRaw, TupleSelectionAssignmentMap);

        var asSeparation = Sequence.Create(multispaceOrNone, asTag, multispaceOrNone);
        var selectionOutputPath = Optional.Create(Preceded.Create(asSeparation, new NamePathParser()));
        var selectionExpressionRaw = Pair.Create(new ExpressionParser(), selectionOutputPath);
        var selectionExpressionMap = Map.Create(selectionExpressionRaw, TupleSelectionExpressionMap);

        var selectionAtom = Alternation.Create(new[] { selectionAssignmentMap, selectionExpressionMap });
        var selectionAtomList = SeparatedList.Create(selectionsSeparator, selectionAtom);

        var tupleSelectionMap = Map.Create(selectionAtomList, TupleSelectionMap);

        return tupleSelectionMap;
    }

    private ITupleSelection TupleSelectionMap(ICollection<ITupleSelectionAtom> atoms) => new TupleSelection(atoms);

    private ITupleSelectionAtom TupleSelectionAssignmentMap((INamePath OutputPath, IExpression Expression) x) =>
        new TupleSelectionAssignment(OutputPath: x.OutputPath, Expression: x.Expression);

    private ITupleSelectionAtom TupleSelectionExpressionMap((IExpression Expression, INamePath? OutputPath) x) =>
        new TupleSelectionExpression(x.Expression, x.OutputPath);
}

public class SelectQueryTopParser : IParser<StringParsable, ITop>
{
    public IResult<StringParsable, ITop> Parse(StringParsable input)
    {
        var firstTag = TagIgnoringCase.Create("first");
        var firstMap = Map.Create(firstTag, x => new First() as ITop);

        var lastTag = TagIgnoringCase.Create("last");
        var lastMap = Map.Create(lastTag, x => new Last() as ITop);

        var oneTag = TagIgnoringCase.Create("one");
        var oneMap = Map.Create(oneTag, x => new One() as ITop);

        var atTag = TagIgnoringCase.Create("at");
        var atPrefix = Pair.Create(atTag, Multispace.Create());
        var atWithPosition = Preceded.Create(atPrefix, Digits.Create());
        var atMap = Map.Create(atWithPosition, (i) => new AtPosition(int.Parse(i)) as ITop);

        var topTag = TagIgnoringCase.Create("top");
        var percentTag = TagIgnoringCase.Create("%");

        var topPrefix = Pair.Create(topTag, Multispace.Create());

        var topWithPercentage = Delimited.Create(topPrefix, Digits.Create(), percentTag);
        var topWithPercentageMap = Map.Create(topWithPercentage, x => new TopByPercentage(double.Parse(x)) as ITop);

        var topWithQuantity = Preceded.Create(topPrefix, Digits.Create());
        var topWithQuantityMap = Map.Create(topWithQuantity, x => new TopByQuantity(int.Parse(x)) as ITop);

        var all = Value.Create(new All() as ITop, Success.Create<StringParsable>());

        var selectTopAlt = Alternation.Create(new[]
        {
                Utilities.MultispaceAfter(firstMap),
                Utilities.MultispaceAfter(lastMap),
                Utilities.MultispaceAfter(oneMap),
                Utilities.MultispaceAfter(topWithPercentageMap),
                Utilities.MultispaceAfter(topWithQuantityMap),
                all,
            });

        return selectTopAlt.Parse(input);
    }
}

public class OrderingsParser : IParser<StringParsable, ICollection<IOrdering>>
{
    public IResult<StringParsable, ICollection<IOrdering>> Parse(StringParsable input)
    {
        var orderPrefix = Utilities.KeywordChainPrefix("order", "by");
        var orderingsSeparator = Utilities.Symbol(',');

        var orderingRawData = Pair.Create(new ExpressionParser(), OrderDirectionByKeywordSuffix());
        var orderingMap = Map.Create(orderingRawData, OrderingMap);
        var orderingList = SeparatedList.Create(orderingsSeparator, orderingMap);

        return Preceded.Create(orderPrefix, orderingList).Parse(input);
    }

    private IParser<StringParsable, OrderDirection> OrderDirectionByKeywordSuffix()
    {
        var ascendingTag = Alternation.Create(new[]
        {
                Utilities.MultispaceBefore(TagIgnoringCase.Create("ascending")),
                Utilities.MultispaceBefore(TagIgnoringCase.Create("asc")),
                Value.Create(StringParsable.Empty, Success.Create<StringParsable>()),
            });
        var ascendingValue = Value.Create(OrderDirection.Ascending, ascendingTag);

        var descendingTag = Alternation.Create(new[]
        {
                Utilities.MultispaceBefore(TagIgnoringCase.Create("descending")),
                Utilities.MultispaceBefore(TagIgnoringCase.Create("desc")),
            });
        var descendingValue = Value.Create(OrderDirection.Descending, descendingTag);

        var orderDirectionData = Alternation.Create(new[] { descendingValue, ascendingValue });

        return orderDirectionData;
    }

    private IOrdering OrderingMap((IExpression Expression, OrderDirection Direction) x)
    {
        return new Ordering(x.Expression, x.Direction);
    }
}

public class LimitParser : IParser<StringParsable, ILimit>
{
    public IResult<StringParsable, ILimit> Parse(StringParsable input)
    {
        var limitPrefix = Utilities.KeywordPrefix("limit");
        var limitQuantity = Preceded.Create(limitPrefix, PrimitiveValues.NonNegativeIntegerValue());
        var limitMap = Map.Create(limitQuantity, x => new Limit(x) as ILimit);
        return limitMap.Parse(input);
    }
}

public class OffsetParser : IParser<StringParsable, IOffset>
{
    public IResult<StringParsable, IOffset> Parse(StringParsable input)
    {
        var offsetPrefix = Utilities.KeywordPrefix("offset");
        var offsetQuantity = Preceded.Create(offsetPrefix, PrimitiveValues.NonNegativeIntegerValue());
        var offsetMap = Map.Create(offsetQuantity, x => new Offset(x) as IOffset);
        return offsetMap.Parse(input);
    }
}

public class PaginationParser : IParser<StringParsable, IPagination>
{
    public IResult<StringParsable, IPagination> Parse(StringParsable input)
    {
        var paginatedPrefix = Utilities.KeywordChainPrefix("paginated", "on");
        var pageData = Delimited.Create(paginatedPrefix, PrimitiveValues.NonNegativeIntegerValue(), Multispace.Create());

        var byPrefix = Utilities.KeywordPrefix("by");
        var sizeData = Preceded.Create(byPrefix, PrimitiveValues.NonNegativeIntegerValue());

        var rawData = Pair.Create(pageData, sizeData);
        var paginationMap = Map.Create(rawData, PaginationMap);

        return paginationMap.Parse(input);
    }

    private IPagination PaginationMap((int Page, int Size) x) => new Pagination(x.Page, x.Size);
}
