using Nom;
using Nom.Branches;
using Nom.Characters;
using Nom.Collections;
using Nom.Combinators;
using Nom.Sequences;
using Nom.Strings;
using System.Globalization;

namespace SqlFace.Parsing;

public static class PrimitiveValues
{
    public static IParser<StringParsable, string> StringValue()
    {
        var quote = Utilities.Quote();
        var chars = Many.Create(SatisfiedBy.Create(c => c != '\'' && c != '"' && c != '\n'));
        var value = Map.Create(chars, chars => chars.Aggregate("", (accum, next) => accum + next));
        return Delimited.Create(quote, value, quote);
    }

    public static IParser<StringParsable, bool> BooleanValueByKeyword()
    {
        var trueValue = Value.Create(true, TagIgnoringCase.Create("true"));
        var falseValue = Value.Create(false, TagIgnoringCase.Create("false"));
        return Alternation.Create(new[] { trueValue, falseValue });
    }

    public static IParser<StringParsable, int> NonNegativeIntegerValue() => Map.Create(Digits.Create(), x => int.Parse(x, CultureInfo.InvariantCulture));

    public static IParser<StringParsable, double> NonNegativeDoubleValue()
    {
        var dotChar = Character.Create('.');
        var digits = Digits.Create();

        var bothDigits = Map.Create(SeparatedPair.Create(digits, dotChar, digits), x => $"{(string)x.Item1}.{(string)x.Item2}");
        var headSkipped = Map.Create(Preceded.Create(dotChar, digits), x => $"0.{(string)x}");
        var tailSkipped = Map.Create(Terminated.Create(digits, dotChar), x => $"{(string)x}.0");

        var alternation = Alternation.Create(new[]
        {
            bothDigits,
            headSkipped,
            tailSkipped,
        });

        return Map.Create(alternation, x => double.Parse(x, CultureInfo.InvariantCulture));
    }
}
