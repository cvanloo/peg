namespace PEG;

/*

# Hierarchical Syntax
Grammar    <- Spacing Definition+ EndOfFile
Definition <- Identifier LEFTARROW Expression

Expression <- Sequence (SLASH Sequence)*
Sequence   <- Prefix*
Prefix     <- (AND / NOT)? Suffix
Suffix     <- Primary (QUESTION / STAR / PLUS)?
Primary    <- Identifier !LEFTARROW
            / OPEN Expression CLOSE
            / Literal / Class / DOT

# Lexical Syntax
Identifier <- IdentStart IdentCont* Spacing
IdentStart <- [a-zA-Z_]
IdentCont  <- IdentStart / [0-9]

Literal    <- ['] (!['] Char)* ['] Spacing
            / ["] (!["] Char)* ["] Spacing
Class      <- '[' (!']' Range)* ']' Spacing
Range      <- Char '-' Char / Char
Char       <- '\\' [nrt'"\[\]\\]
            / '\\' [0-2][0-7][0-7]
            / '\\' [0-7][0-7]?
            / !'\\' .

LEFTARROW  <- '<-' Spacing
SLASH      <- '/' Spacing
AND        <- '&' Spacing
NOT        <- '!' Spacing
QUESTION   <- '?' Spacing
STAR       <- '*' Spacing
PLUS       <- '+' Spacing
OPEN       <- '(' Spacing
CLOSE      <- ')' Spacing
DOT        <- '.' Spacing

Spacing <- (Space / Comment)*
Comment <- '#' (!EndOfLine .)* EndOfLine
Space <- ' ' / '\t' / EndOfLine
EndOfLine <- '\r\n' / '\n' / '\r'
EndOfFile <- !.

*/


using System.Diagnostics.CodeAnalysis;

public sealed class Parser(string input)
{
    // @todo: make this parser generate a packrat parser out of a PEG
    //    -> source generators?

    readonly Tokenizer _tokenizer = new(input);

    char Next() {
        return _tokenizer.Next();
    }
    bool TryNext([NotNullWhen(true)] out char? next) {
        return _tokenizer.TryNext(out next);
    }
    char Peek() {
        return _tokenizer.Peek();
    }
    bool TryPeek([NotNullWhen(true)] out char? peek) {
        return _tokenizer.TryPeek(out peek);
    }
    ReadOnlySpan<char> NextString(int amount) {
        return _tokenizer.NextString(amount);
    }
    bool TryNextString(int amount, [NotNullWhen(true)] out string? next) {
        return _tokenizer.TryNextString(amount, out next);
    }
    ReadOnlySpan<char> PeekString(int amount) {
        return _tokenizer.PeekString(amount);
    }
    bool TryPeekString(int amount, [NotNullWhen(true)] out string? peek) {
        return _tokenizer.TryPeekString(amount, out peek);
    }
    bool AtEnd => _tokenizer.AtEnd;
    Tokenizer.SavedPosition Mark() {
        return _tokenizer.Mark();
    }
    void Reset(Tokenizer.SavedPosition pos) {
        _tokenizer.Reset(pos);
    }
    bool IfMatch(string match) {
        return TryPeekString(match.Length, out var p) && p == match;
    }
    bool IfMatchSkip(string match) {
        return TryNextString(match.Length, out var n) && n == match;
    }

    public bool TryParse([NotNullWhen(true)] out IEnumerable<Rule>? result) {
        return ParseGrammar(out result);
    }

    // Grammar <- Spacing Definition+ EndOfFile
    bool ParseGrammar([NotNullWhen(true)] out IEnumerable<Rule>? result) {
        var pos = Mark();
        if (ParseSpacing(out _)) {
            if (ParseDefinition(out var rule1)) {
                var rules = new List<Rule>([(Rule)rule1]);
                while (ParseDefinition(out var rule)) {
                    rules.Add((Rule)rule);
                }
                if (AtEnd) {
                    result = rules;
                    return true;
                }
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Definition <- Identifier LEFTARROW Expression
    bool ParseDefinition([NotNullWhen(true)] out Rule? result) {
        var pos = Mark();
        if (ParseIdentifier(out var ident)) {
            if (MatchAndSkip("<-")) {
                if (ParseExpression(out var exp)) {
                    result = new Rule(ident, exp);
                    return true;
                }
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Identifier <- IdentStart IdentCont* Spacing
    bool ParseIdentifier([NotNullWhen(true)] out string? result) {
        var pos = Mark();
        if (ParseIdentStart(out var s)) {
            string ident = "" + s;
            while (ParseIdentCont(out var c)) {
                ident += c;
            }
            if (ParseSpacing(out _)) {
                result = ident;
                return true;
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // IdentStart <- [a-zA-Z_]
    bool ParseIdentStart([NotNullWhen(true)] out char? result) {
        if (TryPeek(out var c) && ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')) {
            result = Next();
            return true;
        }
        result = null;
        return false;
    }

    // IdentCont <- IdentStart / [0-9]
    bool ParseIdentCont([NotNullWhen(true)] out char? result) {
        if (TryPeek(out var c) && ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')) {
            result = Next();
            return true;
        }
        result = null;
        return false;
    }

    // LEFTARROW  <- '<-' Spacing
    // SLASH      <- '/' Spacing
    // AND        <- '&' Spacing
    // NOT        <- '!' Spacing
    // QUESTION   <- '?' Spacing
    // STAR       <- '*' Spacing
    // PLUS       <- '+' Spacing
    // OPEN       <- '(' Spacing
    // CLOSE      <- ')' Spacing
    // DOT        <- '.' Spacing
    bool MatchAndSkip(string literal) {
        var pos = Mark();
        if (IfMatchSkip(literal)) {
            if (ParseSpacing(out _)) {
                return true;
            }
        }
        Reset(pos);
        return false;
    }

    // Expression <- Sequence (SLASH Sequence)*
    bool ParseExpression([NotNullWhen(true)] out Expression? result) {
        var pos = Mark();
        if (ParseSequence(out var seq1)) {
            var sequence = new List<Expression>([seq1]);
            while (MatchAndSkip("/") && ParseSequence(out var seq)) {
                sequence.Add(seq);
            }
            result = new PrioritizedChoice(sequence);
            return true;
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Sequence <- Prefix*
    bool ParseSequence([NotNullWhen(true)] out Expression? result) {
        var sequence = new List<Expression>();
        while (ParsePrefix(out var prefix)) {
            sequence.Add(prefix);
        }
        result = new Sequence(sequence);
        return true;
    }

    // Prefix <- (AND / NOT)? Suffix
    bool ParsePrefix([NotNullWhen(true)] out Expression? result) {
        var pos = Mark();
        if (MatchAndSkip("&")) {
            if (ParseSuffix(out var andSuffix)) {
                result = new AndPredicate(andSuffix);
                return true;
            }
        }
        Reset(pos);
        if (MatchAndSkip("!")) {
            if (ParseSuffix(out var notSuffix)) {
                result = new NotPredicate(notSuffix);
                return true;
            }
        }
        Reset(pos);
        if (ParseSuffix(out var suffix)) {
            result = suffix;
            return true;
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Suffix <- Primary (QUESTION / STAR / PLUS)?
    bool ParseSuffix([NotNullWhen(true)] out Expression? result) {
        // result might be just the primary, or the primary rapped in an Option, ZeroOrMore, or OneOrMore
        if (ParsePrimary(out var primary)) {
            if (MatchAndSkip("?")) {
                result = new Option(primary);
                return true;
            }
            if (MatchAndSkip("*")) {
                result = new ZeroOrMore(primary);
                return true;
            }
            if (MatchAndSkip("+")) {
                result = new OneOrMore(primary);
                return true;
            }
            result = primary;
            return true;
        }
        result = null;
        return false;
    }

    // Primary <- Identifier !LEFTARROW
    //          / OPEN Expression CLOSE
    //          / Literal / Class / DOT
    bool ParsePrimary([NotNullWhen(true)] out Expression? result) {
        var pos = Mark();
        if (ParseIdentifier(out var ident)) {
            if (!MatchAndSkip("<-")) {
                result = new NonTerminal(ident);
                return true;
            }
        }
        Reset(pos);
        if (MatchAndSkip("(")) {
            if (ParseExpression(out var exp)) {
                if (MatchAndSkip(")")) {
                    result = exp;
                    return true;
                }
            }
        }
        Reset(pos);
        if (ParseLiteral(out var literal)) {
            result = new Terminal(literal);
            return true;
        }
        if (ParseClass(out var klass)) {
            result = new CharacterClass(klass);
            return true;
        }
        if (MatchAndSkip(".")) {
            result = new AnyTerminal();
            return true;
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Literal <- ['] (!['] Char)* ['] Spacing
    //          / ["] (!["] Char)* ["] Spacing
    bool ParseLiteral([NotNullWhen(true)] out string? result) {
        var pos = Mark();
        if (IfMatchSkip("'")) {
            var literal = "";
            while (TryPeek(out var sq) && sq != '\'' && ParseChar(out var c)) {
                literal += c;
            }
            if (IfMatchSkip("'")) {
                if (ParseSpacing(out _)) {
                    result = literal;
                    return true;
                }
            }
        }
        Reset(pos);
        if (IfMatchSkip("\"")) {
            var literal = "";
            while (TryPeek(out var dq) && dq != '"' && ParseChar(out var c)) {
                literal += c;
            }
            if (IfMatchSkip("\"")) {
                if (ParseSpacing(out _)) {
                    result = literal;
                    return true;
                }
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Class <- '[' (!']' Range)* ']' Spacing
    bool ParseClass([NotNullWhen(true)] out CharSet? result) {
        var pos = Mark();
        if (IfMatchSkip("[")) {
            var ranges = new List<CharSet>();
            while (TryPeek(out var cb) && cb != ']' && ParseRange(out var range)) {
                ranges.Add((Range)range);
            }
            if (IfMatchSkip("]")) {
                if (ParseSpacing(out _)) {
                    result = new Ranges(ranges);
                    return true;
                }
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Char <- '\\' [nrt'"\[\]\\]
    //       / '\\' [0-2][0-7][0-7]
    //       / '\\' [0-7][0-7]?
    //       / !'\\' .
    bool ParseChar([NotNullWhen(true)] out char? result) {
        var pos = Mark();
        if (IfMatchSkip("\\")) {
            var posAfterEscaper = Mark();
            if (TryNext(out var c)) {
                switch (c) {
                    case 'n':
                        result = '\n';
                        return true;
                    case 'r':
                        result = '\r';
                        return true;
                    case 't':
                        result = '\t';
                        return true;
                    case '\'':
                        result = '\'';
                        return true;
                    case '"':
                        result = '"';
                        return true;
                    case '[':
                        result = '[';
                        return true;
                    case ']':
                        result = ']';
                        return true;
                    case '\\':
                        result = '\\';
                        return true;
                }
                Reset(posAfterEscaper);
                if (TryNext(out var n1) && '0' <= n1 && n1 <= '2') {
                    if (TryNext(out var n2) && '0' <= n2 && n2 <= '7') {
                        if (TryNext(out var n3) && '0' <= n3 && n3 <= '7') {
                            var o1 = ('0' - n1) * 64; // 8^2
                            var o2 = ('0' - n2) * 8;  // 8^1
                            var o3 = ('0' - n3) * 1;  // 8^0
                            result = (char)(o1 + o2 + o3);
                            return true;
                        }
                    }
                }
                Reset(posAfterEscaper);
                if (TryNext(out var d1) && '0' <= d1 && d1 <= '7') {
                    if (TryPeek(out var p) && '0' <= p && p <= '7') {
                        var o1 = ('0' - d1) * 8;   // 8^1
                        var o2 = ('0' - Next()) * 1; // 8^0
                        result = (char)(o1 + o2);
                        return true;
                    }
                    result = (char)('0' - d1);
                    return true;
                }
            }
        }
        Reset(pos);
        if (!IfMatch("\\")) {
            result = Next();
            return true;
        }
        result = null;
        return false;
    }

    // Range <- Char '-' Char / Char
    bool ParseRange([NotNullWhen(true)] out Range? result) {
        var pos = Mark();
        if (ParseChar(out var start)) {
            if (IfMatchSkip("-")) {
                if (ParseChar(out var end)) {
                    result = new Range((char)start, (char)end);
                    return true;
                }
            }
        }
        Reset(pos);
        if (ParseChar(out var c)) {
            result = new Range((char)c, (char)c);
            return true;
        }
        Reset(pos);
        result = null;
        return false;
    }

    // Spacing <- (Space / Comment)*
    bool ParseSpacing([NotNullWhen(true)] out string? result) {
        result = "";
        while (ParseSpace(out var space) || ParseComment(out space)) {
            result += space;
        }
        return true;
    }

    // Space <- ' ' / '\t' / EndOfLine
    bool ParseSpace([NotNullWhen(true)] out string? result) {
        if (TryPeek(out var p) && (p == ' ' || p == '\t')) {
            result = "" + Next();
            return true;
        }
        if (ParseEndOfLine(out var lineEnd)) {
            result = lineEnd;
            return true;
        }
        result = null;
        return false;
    }

    // Comment <- '#' (!EndOfLine .)* EndOfLine
    bool ParseComment([NotNullWhen(true)] out string? result) {
        var pos = Mark();
        if (IfMatchSkip("#")) {
            var comment = "";
            while (Not(ParseEndOfLine, out string? _) && !AtEnd) {
                comment += Next();
            }
            if (ParseEndOfLine(out var eol)) {
                result = comment + eol;
                return true;
            }
        }
        Reset(pos);
        result = null;
        return false;
    }

    // EndOfLine <- '\r\n' / '\n' / '\r'
    bool ParseEndOfLine([NotNullWhen(true)] out string? result) {
        if (IfMatch("\r\n")) {
            result = NextString(2).ToString();
            return true;
        }
        if (TryPeek(out var p) && (p == '\n' || p == '\r')) {
            result = "" + Next();
            return true;
        }
        result = null;
        return false;
    }

    delegate bool ParserFunc<T>(out T? result);
    bool Not<T>(ParserFunc<T> f, out T? result) {
        var pos = Mark();
        if (!f(out result)) {
            return true;
        }
        Reset(pos);
        result = default;
        return false;
    }
}

public sealed class Tokenizer(string input)
{
    public int Position { get; private set; }

    public bool AtEnd => input.Length == Position;
    public char Next() {
        return input[Position++];
    }
    public bool TryNext([NotNullWhen(true)] out char? next) {
        if (!AtEnd) {
            next = Next();
            return true;
        }
        next = null;
        return false;
    }
    public char Peek() {
        return input[Position];
    }
    public bool TryPeek([NotNullWhen(true)] out char? peek) {
        if (!AtEnd) {
            peek = Peek();
            return true;
        }
        peek = null;
        return false;
    }

    public ReadOnlySpan<char> NextString(int amount) {
        var span = input.AsSpan().Slice(start: Position, length: amount);
        Position += amount;
        return span;
    }
    public bool TryNextString(int amount, [NotNullWhen(true)] out string? next) {
        if (Position < input.Length - amount) {
            next = NextString(amount).ToString();
            return true;
        }
        next = null;
        return false;
    }

    public ReadOnlySpan<char> PeekString(int amount) {
        var span = input.AsSpan().Slice(start: Position, length: amount);
        return span;
    }
    public bool TryPeekString(int amount, [NotNullWhen(true)] out string? peek) {
        if (Position < input.Length - amount) {
            peek = PeekString(amount).ToString();
            return true;
        }
        peek = null;
        return false;
    }

    //public SavedPosition Mark => new SavedPosition(Position);
    public SavedPosition Mark() {
        return new SavedPosition(Position);
    }

    public void Reset(SavedPosition saved) {
        Position = saved.Position;
    }

    public class SavedPosition {
        public int Position { get; private init; }

        internal SavedPosition(int pos) {
            Position = pos;
        }
        public override string ToString() {
            return $"{Position}";
        }
    }

    public string Rest() {
        return input.Substring(Position);
    }
}

public readonly record struct Rule(string Name, Expression Expression) {
    public override string ToString() {
        return $"{Name} <- {Expression}";
    }
}

// @todo: Visitor Pattern
// @todo: Fix pretty printing
public abstract record Expression;

public sealed record Terminal(string Symbol) : Expression {
    public override string ToString() {
        return $"'{Symbol}'";
    }
}

public sealed record NonTerminal(string Name): Expression {
    public override string ToString() {
        return Name;
    }
}

public sealed record Sequence(List<Expression> Expressions) : Expression {
    public override string ToString() {
        return Expressions.Aggregate("", (s, exp) => {
            if (s == "") {
                return exp.ToString();
            }
            return s + " " + exp;
        });
    }
}

public sealed record PrioritizedChoice(List<Expression> Choices) : Expression {
    public override string ToString() {
        return Choices.Aggregate("", (s, exp) => {
            if (s == "") {
                return exp.ToString();
            }
            return s + " / " + exp;
        });
    }
}

public sealed record ZeroOrMore(Expression Inner) : Expression {
    public override string ToString() {
        return $"{Inner}*";
    }
}

public sealed record NotPredicate(Expression Inner) : Expression {
    public override string ToString() {
        return $"!{Inner}";
    }
}

// Sugar

public sealed record AnyTerminal : Expression {
    public override string ToString() {
        return ".";
    }
}

public sealed record CharacterClass(CharSet Klass) : Expression {
    public override string ToString() {
        return $"[{Klass}]";
    }
}

public sealed record Option(Expression Inner) : Expression {
    public override string ToString() {
        return $"{Inner}?";
    }
}

public sealed record OneOrMore(Expression Inner) : Expression {
    public override string ToString() {
        return $"{Inner}+";
    }
}

public sealed record AndPredicate(Expression Inner) : Expression {
    public override string ToString() {
        return $"&{Inner}";
    }
}

public interface CharSet {
    bool Contains(char c);
}

public readonly record struct Range(char Start, char End) : CharSet {
    public bool Contains(char c) {
        return Start <= c && c <= End;
    }
    public override string ToString() {
        if (Start == End) {
            return $"{Start}";
        }
        return $"{Start}-{End}";
    }
}

public readonly record struct Ranges(IEnumerable<CharSet> Sets) : CharSet {
    public bool Contains(char c) {
        return Sets.FirstOrDefault(set => set.Contains(c)) != null;
    }
    public override string ToString() {
        return Sets.Aggregate("", (s, range) => s + range);
    }
}
