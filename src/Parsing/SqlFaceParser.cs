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
        var parser = CreateSyntaxTreeParser();
        var result = parser.Parse(code);
        return result.Output;
    }

    public IParser<StringParsable, ISyntaxTree> CreateSyntaxTreeParser()
    {
        var selectQuery = CreateSelectQueryParser();
        var statementAlternation = Alternation.Create(new[] { selectQuery });
        var statements = ManyOrNone.Create(statementAlternation);
        var syntaxTree = Map.Create(statements,
            (ICollection<IStatement> statements) => new SyntaxTree(statements) as ISyntaxTree);
        return syntaxTree;
    }

    public IParser<StringParsable, IStatement> CreateSelectQueryParser()
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var multispace = Multispace.Create();
        var alphanumerics = Alphanumerics.Create();
        var commaSeparation = Character.Create(',');

        var assignmentTag = TagIgnoringCase.Create("=");
        var selectTag = TagIgnoringCase.Create("select");
        var asTag = TagIgnoringCase.Create("as");
        var fromTag = TagIgnoringCase.Create("from");

        var keyword = Alternation.Create(new[]
        {
            assignmentTag,
            selectTag,
            asTag,
            fromTag,
        });
        var notKeyword = Not.Create(keyword);
        
        var selectPrefix = Delimited.Create(multispaceOrNone, selectTag, multispace);
        var selectionsSeparator = Sequence.Create(multispaceOrNone, commaSeparation, multispaceOrNone);

        var objectIdentifierNotVerified = Pair.Create(SatisfiedBy.Create(c => char.IsLetter(c) || c == '_'), alphanumerics);

        var objectIdentifierRaw = Verify.Create(objectIdentifierNotVerified, x =>
        {
            try
            {
                notKeyword.Parse(x.Item1 + x.Item2);
                return true;
            }
            catch
            {
                return false;
            }
        });
        
        var objectIdentifierMap = Map.Create(
            parser: objectIdentifierRaw,
            mapDelegate: x => new ObjectIdentifier(x.Item1 + x.Item2) as IObjectPathValue);

        var objectWildcardMap = Map.Create(
            parser: Many.Create(Character.Create('*')),
            mapDelegate: levels => new ObjectWildcard(levels.Count) as IObjectPathValue);

        var objectPathValue = Alternation.Create(new[]
        {
            objectIdentifierMap,
            objectWildcardMap,
        });
        
        var objectPathMap = Map.Create(objectPathValue, x => new ObjectPath(x) as IObjectPath);

        var assigmentSeparation = Sequence.Create(multispaceOrNone, assignmentTag, multispaceOrNone);
        var selectionAssignmentRaw = SeparatedPair.Create(objectPathMap, assigmentSeparation, objectPathMap);
        var selectionAssignmentMap = Map.Create(selectionAssignmentRaw, TupleSelectionAssignmentMap);

        var asSeparation = Sequence.Create(multispaceOrNone, asTag, multispaceOrNone);
        var selectionOutputPath = Optional.Create(Preceded.Create(asSeparation, objectPathMap));        
        var selectionExpressionRaw = Pair.Create(objectPathMap, selectionOutputPath);
        var selectionExpressionMap = Map.Create(selectionExpressionRaw, TupleSelectionExpressionMap);

        var selectionAtom = Alternation.Create(new[] { selectionAssignmentMap, selectionExpressionMap });
        var selectionAtomList = SeparatedList.Create(selectionsSeparator, selectionAtom);
        var selectionAtoms = Delimited.Create(selectPrefix, selectionAtomList, multispaceOrNone);
        var selectionMap = Map.Create(selectionAtoms, TupleSelectionMap);

        var fromPrefix = Pair.Create(fromTag, multispace);
        var precededSource = Preceded.Create(fromPrefix, objectPathMap);
        var sourceMap = Map.Create(precededSource, x => new SourceReference(x) as ISourceReference);

        var termination = Pair.Create(multispaceOrNone, Optional.Create(Character.Create(';')));
        var selectionSourcePair = Terminated.Create(Pair.Create(selectionMap, sourceMap), termination);
        var selectQueryMap = Map.Create(selectionSourcePair, SelectQueryMapAsStatement);
        
        return selectQueryMap;
    }

    private IStatement SelectQueryMapAsStatement((ITupleSelection Selection, ISourceReference Source) x) => SelectQueryMap(x);
        

    private ISelectQuery SelectQueryMap((ITupleSelection Selection, ISourceReference Source) x)
        => new SelectQuery(
            selection: x.Selection,
            selectable: x.Source);

    private ITupleSelection TupleSelectionMap(ICollection<ITupleSelectionAtom> atoms) => new TupleSelection(atoms);

    private ITupleSelectionAtom TupleSelectionAssignmentMap((IObjectPath Alias, IObjectPath Name) x)
        => new TupleSelectionAssignment(
            outputPath: x.Alias,
            expression: x.Name);

    private ITupleSelectionAtom TupleSelectionExpressionMap((IObjectPath Name, IObjectPath? Alias) x)
        => new TupleSelectionExpression(x.Name)
        {
            OutputPath = x.Alias,
        };
}
