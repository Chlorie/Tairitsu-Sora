using System.Diagnostics.CodeAnalysis;
using System.Text;
using LanguageExt;
using TairitsuSora.Core;
using TairitsuSora.Utils;

namespace TairitsuSora.Commands;

[RegisterCommand]
public class Dice : Command
{
    public override CommandInfo Info => new()
    {
        Trigger = "d",
        Summary = "模拟掷骰子"
    };

    [MessageHandler(Signature = "$notation", Description = "模拟掷骰子结果，采用通用骰子记号", ReplyException = true)]
    public string Roll([ShowDefaultValueAs("d6")] FullString notation)
    {
        string text = notation.Text.Trim().ToLower();
        if (text == "") text = "d6";
        Expression expr = new NotationParser(text).Parse();
        DiceRoller roller = new();
        try
        {
            int result = expr.Roll(roller);
            string desc = roller.TotalRolls > 0 ? $"\n({roller.DescribeRolls()})" : "";
            return $"{text} = {result}{desc}";
        }
        catch (OverflowException) { return "计算结果溢出"; }
        catch (DivideByZeroException) { return "计算过程中发生除以 0 错误"; }
    }

    private class NotationParser(string notation)
    {
        public Expression Parse()
        {
            Either<Expression, char>? prevItem = null;
            while (true)
            {
                var token = GetNextToken();
                if (token.Length == 0) break;
                var item = ProcessToken(token).Map(TranslateUnary);
                HandleItem(item);
                prevItem = item;
            }
            ProcessRemainingSymbols();
            return GetResult();

            [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
            char TranslateUnary(char symbol)
            {
                if (symbol == '(' && prevItem is { IsLeft: true })
                    throw new InvalidOperationException("无法分析表达式");
                if (symbol is not ('+' or '-')) return symbol;
                if (prevItem is not { } item) return symbol == '+' ? 'p' : 'n';
                return item.Match(
                    Left: _ => symbol,
                    Right: c => c == ')' ? symbol : symbol == '+' ? 'p' : 'n'
                );
            }
        }

        private const string Symbols = "+-*/×÷x()";
        private int _pos;
        private Stack<char> _symbols = [];
        private Stack<Expression> _exprs = [];

        private static int Precedence(char symbol) => symbol switch
        {
            '(' => 1,
            '+' or '-' => 2,
            '*' or '×' or 'x' or '/' or '÷' => 3,
            'p' or 'n' => 4,
            ')' => 5,
            _ => 0
        };

        private ReadOnlySpan<char> GetNextToken()
        {
            while (_pos < notation.Length && char.IsWhiteSpace(notation[_pos])) _pos++;
            if (_pos == notation.Length) return "";
            if (Symbols.Contains(notation[_pos]))
            {
                _pos++;
                return notation.AsSpan()[(_pos - 1).._pos];
            }
            int start = _pos;
            while (_pos < notation.Length &&
                   !Symbols.Contains(notation[_pos]) &&
                   !char.IsWhiteSpace(notation[_pos]))
                _pos++;
            return notation.AsSpan()[start.._pos];
        }

        private static Either<Expression, char> ProcessToken(ReadOnlySpan<char> token)
        {
            if (token.Length == 1 && Symbols.Contains(token[0]))
                return token[0] switch
                {
                    '+' or '-' or '*' or '/' or 'x' or '(' or ')' => token[0],
                    '×' => '*',
                    '÷' => '/',
                    _ => throw new InvalidOperationException()
                };

            if (int.TryParse(token, out int cnst)) return new Constant(cnst);
            if (token.Count('d') != 1) throw new InvalidOperationException($"无法识别记号 {token}");
            int dPos = token.IndexOf('d');
            int count = 1;
            if (dPos != 0 && !int.TryParse(token[..dPos], out count))
                throw new InvalidOperationException($"无法识别记号 {token}");
            if (!int.TryParse(token[(dPos + 1)..], out int faces))
                throw new InvalidOperationException($"无法识别记号 {token}");
            if (count == 0)
                throw new InvalidOperationException("掷骰子次数不能为 0");
            if (faces is < 2 or > 100)
                throw new InvalidOperationException("骰子面数必须在 2 到 100 之间");
            return new DiceRoll(count, faces);
        }

        private char TryPeekSymbol() => _symbols.Count == 0 ? '\0' : _symbols.Peek();

        private void HandleItem(Either<Expression, char> item)
        {
            if (item.IsLeft)
            {
                _exprs.Push(item.GetLeft());
                return;
            }
            char symbol = item.GetRight();
            switch (symbol)
            {
                case '(':
                    _symbols.Push(symbol);
                    return;
                case ')':
                    while (TryPeekSymbol() != '(')
                    {
                        if (_symbols.Count == 0)
                            throw new InvalidOperationException("无法找到配对的左括号");
                        PopSymbol();
                    }
                    _symbols.Pop();
                    return;
                default:
                    while (Precedence(TryPeekSymbol()) >= Precedence(symbol)) PopSymbol();
                    _symbols.Push(symbol);
                    return;
            }
        }

        private void PopSymbol()
        {
            char top = _symbols.Pop();
            switch (top)
            {
                case '(': case 'p': return;
                case 'n': _exprs.Push(new Negation(CheckedPop())); return;
                default: // All binary operators
                    Expression rhs = CheckedPop(), lhs = CheckedPop();
                    switch (top)
                    {
                        case '+': _exprs.Push(lhs + rhs); return;
                        case '-': _exprs.Push(lhs - rhs); return;
                        case '*': _exprs.Push(lhs * rhs); return;
                        case '/': _exprs.Push(lhs / rhs); return;
                        case 'x':
                            if (lhs is not Constant) (lhs, rhs) = (rhs, lhs);
                            if (lhs is not Constant { Value: var value and > 0 })
                                throw new InvalidOperationException("重掷次数必须是正整数");
                            _exprs.Push(new Reroll(value, rhs));
                            return;
                        default: throw new InvalidOperationException();
                    }
            }

            Expression CheckedPop()
            {
                if (_exprs.Count > 0) return _exprs.Pop();
                throw new InvalidOperationException("无法分析表达式");
            }
        }

        private void ProcessRemainingSymbols()
        {
            while (_symbols.Count > 0)
            {
                if (_symbols.Peek() == '(')
                    throw new InvalidOperationException("表达式有括号没有闭合");
                PopSymbol();
            }
        }

        private Expression GetResult()
        {
            return _exprs.Count switch
            {
                0 => throw new InvalidOperationException("输入的表达式是空的"),
                > 1 => throw new Exception("输入的记号并不是一个表达式"),
                _ => _exprs.Pop()
            };
        }
    }

    private class DiceRoller
    {
        public int TotalRolls { get; private set; }

        public void Reset()
        {
            TotalRolls = 0;
            _rolls = [];
        }

        public int[] Roll(int count, int faces)
        {
            TotalRolls += count;
            if (TotalRolls > MaxRolls)
                throw new InvalidOperationException("掷骰子次数过多，最多 100 次");
            int[] results = new int[count];
            for (int i = 0; i < count; i++)
                results[i] = Random.Shared.Next(faces) + 1;
            _rolls.Add(new DiceRollRecord(count, faces, results));
            return results;
        }

        public string DescribeRolls()
        {
            StringBuilder sb = new();
            foreach (var roll in _rolls)
                sb.Append($"{roll.Count}d{roll.Faces}: {string.Join(", ", roll.Results)}; ");
            return sb.ToString()[..^2];
        }

        private const int MaxRolls = 100;
        private record struct DiceRollRecord(int Count, int Faces, int[] Results);
        private List<DiceRollRecord> _rolls = [];
    }

    private abstract class Expression
    {
        public abstract int Roll(DiceRoller recorder);

        public static Expression operator +(Expression left, Expression right) => new BinaryOperator(left, right, Add);
        public static Expression operator -(Expression left, Expression right) => new BinaryOperator(left, right, Subtract);
        public static Expression operator *(Expression left, Expression right) => new BinaryOperator(left, right, Multiply);
        public static Expression operator /(Expression left, Expression right) => new BinaryOperator(left, right, Divide);

        private static readonly Func<int, int, int> Add = static (a, b) => checked(a + b);
        private static readonly Func<int, int, int> Subtract = static (a, b) => checked(a - b);
        private static readonly Func<int, int, int> Multiply = static (a, b) => checked(a * b);
        private static readonly Func<int, int, int> Divide = static (a, b) => a / b;
    }

    private class Constant(int value) : Expression
    {
        public int Value => value;
        public override int Roll(DiceRoller recorder) => value;
    }

    private class DiceRoll(int count, int faces) : Expression
    {
        public override int Roll(DiceRoller recorder) => recorder.Roll(count, faces).Sum();
    }

    private class Negation(Expression expr) : Expression
    {
        public override int Roll(DiceRoller recorder) => checked(-expr.Roll(recorder));
    }

    private class BinaryOperator(Expression left, Expression right, Func<int, int, int> operation) : Expression
    {
        public override int Roll(DiceRoller recorder) => operation(left.Roll(recorder), right.Roll(recorder));
    }

    private class Reroll(int times, Expression expr) : Expression
    {
        public override int Roll(DiceRoller recorder)
        {
            int result = 0;
            for (int i = 0; i < times; i++)
                result += expr.Roll(recorder);
            return result;
        }
    }
}
