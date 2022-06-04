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
        var selectQuery = Map.Create(SelectQuery(), x => x as IStatement);
        var statementAlternation = Alternation.Create(new[] { selectQuery });
        var statements = ManyOrNone.Create(statementAlternation);
        var syntaxTree = Map.Create(statements,
            (ICollection<IStatement> statements) => new SyntaxTree(statements) as ISyntaxTree);
        return syntaxTree;
    }

    private IParser<StringParsable, StringParsable> Symbol(char symbolChar)
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var symbol = Character.Create(symbolChar);
        return Delimited.Create(multispaceOrNone, symbol, multispaceOrNone);
    }

    private IParser<StringParsable, T> MultispaceDelimited<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Delimited.Create(Multispace.Create(), parser, Multispace.Create());
        return spaceAfter;
    }

    private IParser<StringParsable, T> MultispaceBefore<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Preceded.Create(Multispace.Create(), parser);
        return spaceAfter;
    }

    private IParser<StringParsable, T> MultispaceAfter<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Terminated.Create(parser, Multispace.Create());
        return spaceAfter;
    }

    private IParser<StringParsable, StringParsable> KeywordPrefix(string keyword)
    {
        var keywordTag = TagIgnoringCase.Create(keyword);
        var keywordPrefix = Preceded.Create(keywordTag, Multispace.Create());
        return keywordPrefix;
    }

    private IParser<StringParsable, ICollection<StringParsable>> KeywordChainPrefix(params string[] keywords)
    {
        var parserChain = keywords
            .Select(k => new IParser<StringParsable, StringParsable>[] { TagIgnoringCase.Create(k), Multispace.Create() })
            .SelectMany(x => x);

        return Sequence.Create(parserChain);
    }

    private IParser<StringParsable, StringParsable?> StatementTermination()
    {
        var semicolonSymbol = Character.Create(';');
        var multispaceOrNone = MultispaceOrNone.Create();
        return Delimited.Create(multispaceOrNone, Optional.Create(semicolonSymbol), multispaceOrNone);
    }

    private IParser<StringParsable, IExpression> Expression()
    {
        var namePathMap = Map.Create(NamePath(), x => x as IExpression);
        var wildcardMap = Map.Create(Wildcard(), x => x as IExpression);
        var alternation = Alternation.Create(new[] { wildcardMap, namePathMap });
        return alternation;
    }

    private IParser<StringParsable, INamePath> NamePath()
    {
        var nameItemRawData = Pair.Create(SatisfiedBy.Create(c => char.IsLetter(c) || c == '_'), AlphanumericsOrNone.Create());
        var nameItemMap = Map.Create(
            parser: nameItemRawData,
            mapper: x => new NameItem(x.Item1 + x.Item2) as INameItem);

        var namePathList = SeparatedList.Create(Symbol('.'), nameItemMap);
        var namePathMap = Map.Create(namePathList, x => new NamePath(x) as INamePath);

        return namePathMap;
    }

    private IParser<StringParsable, IWildcard> Wildcard()
    {
        return Map.Create(
            parser: Many.Create(Character.Create('*')),
            mapper: levels => new Wildcard(levels.Count) as IWildcard);
    }

    public IParser<StringParsable, ISelectQuery> SelectQuery()
    {
        var selectPrefix = KeywordPrefix("select");

        var top = SelectQueryTop();
        var selection = MultispaceAfter(Selection());
        var source = SelectSource();
        
        var headerDataTuple = Preceded.Create(selectPrefix, Tup.Create((top, selection, source)));

        var orderings = Optional.Create(MultispaceBefore(Orderings()));
        var offset = Optional.Create(MultispaceBefore(Offset()));
        var limit = Optional.Create(MultispaceBefore(Limit()));
        var pagination = Optional.Create(MultispaceBefore(Pagination()));

        var bodyDataTuple = Tup.Create((orderings, offset, limit, pagination));

        var allDataTuple = Terminated.Create(Tup.Create((headerDataTuple, bodyDataTuple)), StatementTermination());

        var selectQueryMap = Map.Create(allDataTuple, SelectQueryMap);
        
        return selectQueryMap;
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

    private IParser<StringParsable, ISelection> Selection()
    {
        var tupleSelection = Map.Create(TupleSelection(), x => x as ISelection);
        
        return tupleSelection;
    }

    private IParser<StringParsable, ITupleSelection> TupleSelection()
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var asTag = TagIgnoringCase.Create("as");

        var selectionsSeparator = Symbol(',');
        var assigmentSeparation = Symbol('=');

        var selectionAssignmentRaw = SeparatedPair.Create(NamePath(), assigmentSeparation, Expression());
        var selectionAssignmentMap = Map.Create(selectionAssignmentRaw, TupleSelectionAssignmentMap);

        var asSeparation = Sequence.Create(multispaceOrNone, asTag, multispaceOrNone);
        var selectionOutputPath = Optional.Create(Preceded.Create(asSeparation, NamePath()));
        var selectionExpressionRaw = Pair.Create(Expression(), selectionOutputPath);
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

    private IParser<StringParsable, ISourceReference> SelectSource()
    {
        var fromPrefix = KeywordPrefix("from");
        var precededSource = Preceded.Create(fromPrefix, NamePath());
        var sourceMap = Map.Create(precededSource, x => new SourceReference(x) as ISourceReference);
        return sourceMap;
    }

    private IParser<StringParsable, ITop> SelectQueryTop()
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
            MultispaceAfter(firstMap),
            MultispaceAfter(lastMap),
            MultispaceAfter(oneMap),
            MultispaceAfter(topWithPercentageMap),
            MultispaceAfter(topWithQuantityMap),
            all,
        });

        return selectTopAlt;
    }

    private IParser<StringParsable, ICollection<IOrdering>> Orderings()
    {
        var orderPrefix = KeywordChainPrefix("order", "by");
        var orderingsSeparator = Symbol(',');

        var orderingRawData = Pair.Create(Expression(), OrderDirectionByKeywordSuffix());
        var orderingMap = Map.Create(orderingRawData, OrderingMap);
        var orderingList = SeparatedList.Create(orderingsSeparator, orderingMap);

        return Preceded.Create(orderPrefix, orderingList);
    }

    private IParser<StringParsable, OrderDirection> OrderDirectionByKeywordSuffix()
    {
        var ascendingTag = Alternation.Create(new[]
        {
            MultispaceBefore(TagIgnoringCase.Create("ascending")),
            MultispaceBefore(TagIgnoringCase.Create("asc")),
            Value.Create(StringParsable.Empty, Success.Create<StringParsable>()),
        });
        var ascendingValue = Value.Create(OrderDirection.Ascending, ascendingTag);

        var descendingTag = Alternation.Create(new[]
        {
            MultispaceBefore(TagIgnoringCase.Create("descending")),
            MultispaceBefore(TagIgnoringCase.Create("desc")),
        });
        var descendingValue = Value.Create(OrderDirection.Descending, descendingTag);

        var orderDirectionData = Alternation.Create(new[] { descendingValue, ascendingValue });

        return orderDirectionData;
    }

    private IOrdering OrderingMap((IExpression Expression, OrderDirection Direction) x)
    {
        return new Ordering(x.Expression, x.Direction);
    }

    private IParser<StringParsable, ILimit> Limit()
    {
        var limitPrefix = KeywordPrefix("limit");
        var limitQuantity = Preceded.Create(limitPrefix, IntegerValue());
        var limitMap = Map.Create(limitQuantity, x => new Limit(x) as ILimit);
        return limitMap;
    }

    private IParser<StringParsable, IOffset> Offset()
    {
        var offsetPrefix = KeywordPrefix("offset");
        var offsetQuantity = Preceded.Create(offsetPrefix, IntegerValue());
        var offsetMap = Map.Create(offsetQuantity, x => new Offset(x) as IOffset);
        return offsetMap;
    }

    private IParser<StringParsable, IPagination> Pagination()
    {
        var paginatedPrefix = KeywordChainPrefix("paginated", "on");
        var pageData = Delimited.Create(paginatedPrefix, IntegerValue(), Multispace.Create());

        var byPrefix = KeywordPrefix("by");
        var sizeData = Preceded.Create(byPrefix, IntegerValue());

        var rawData = Pair.Create(pageData, sizeData);
        var paginationMap = Map.Create(rawData, PaginationMap);

        return paginationMap;
    }

    private IPagination PaginationMap((int Page, int Size) x) => new Pagination(x.Page, x.Size);

    private IParser<StringParsable, int> IntegerValue() => Map.Create(Digits.Create(), x => int.Parse(x));

    private IParser<StringParsable, double> DoubleValue() => Map.Create(Digits.Create(), x => double.Parse(x));
}
