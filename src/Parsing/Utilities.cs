using Nom;
using Nom.Branches;
using Nom.Characters;
using Nom.Combinators;
using Nom.Sequences;
using Nom.Strings;

namespace SqlFace.Parsing;

public static class Utilities
{
    public static IParser<StringParsable, T> ParenthesisDelimited<T>(IParser<StringParsable, T> parser)
    {
        var delimitation = Delimited.Create(Symbol('('), parser, Symbol(')'));
        return delimitation;
    }

    public static IParser<StringParsable, StringParsable> Quote()
    {
        return Alternation.Create(new[] { Character.Create('\''), Character.Create('"') });
    }
    
    public static IParser<StringParsable, StringParsable> Symbol(char symbolChar)
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var symbol = Character.Create(symbolChar);
        return Delimited.Create(multispaceOrNone, symbol, multispaceOrNone);
    }

    public static IParser<StringParsable, ICollection<StringParsable>> Symbols(string symbols)
    {
        var multispaceOrNone = MultispaceOrNone.Create();
        var symbol = Sequence.Create(symbols.Select(Character.Create));
        return Delimited.Create(multispaceOrNone, symbol, multispaceOrNone);
    }

    public static IParser<StringParsable, T> MultispaceDelimited<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Delimited.Create(Multispace.Create(), parser, Multispace.Create());
        return spaceAfter;
    }

    public static IParser<StringParsable, T> MultispaceBefore<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Preceded.Create(Multispace.Create(), parser);
        return spaceAfter;
    }

    public static IParser<StringParsable, T> MultispaceAfter<T>(IParser<StringParsable, T> parser)
    {
        var spaceAfter = Terminated.Create(parser, Multispace.Create());
        return spaceAfter;
    }

    public static IParser<StringParsable, StringParsable> KeywordPrefix(string keyword)
    {
        var keywordTag = TagIgnoringCase.Create(keyword);
        var keywordPrefix = Preceded.Create(keywordTag, Multispace.Create());
        return keywordPrefix;
    }

    public static IParser<StringParsable, ICollection<StringParsable>> KeywordChainPrefix(params string[] keywords)
    {
        var parserChain = keywords
            .Select(k => new IParser<StringParsable, StringParsable>[] { TagIgnoringCase.Create(k), Multispace.Create() })
            .SelectMany(x => x);

        return Sequence.Create(parserChain);
    }

    public static IParser<StringParsable, StringParsable?> StatementTermination()
    {
        var semicolonSymbol = Character.Create(';');
        var multispaceOrNone = MultispaceOrNone.Create();
        return Delimited.Create(multispaceOrNone, Optional.Create(semicolonSymbol), multispaceOrNone);
    }
}
