using System.Text.RegularExpressions;

internal sealed record ParsedMethod(string Name, int NameStartIndex, string Body);

internal static class MethodParser
{
    private static readonly Regex MethodRegex = new(
        @"(?m)^\s*(?:public|private|protected|internal)\s+(?:static\s+|virtual\s+|override\s+|sealed\s+|async\s+|partial\s+|new\s+)*[\w<>\[\],\.\?]+\s+(?<name>[A-Za-z_]\w*)\s*\([^;\n]*\)\s*(?<body>\{|=>)",
        RegexOptions.Compiled);

    public static IReadOnlyList<ParsedMethod> Parse(string content)
    {
        var methods = new List<ParsedMethod>();
        var matches = MethodRegex.Matches(content);

        foreach (Match match in matches)
        {
            var nameGroup = match.Groups["name"];
            var bodyToken = match.Groups["body"].Value;
            if (bodyToken == "=>")
            {
                methods.Add(new ParsedMethod(nameGroup.Value, nameGroup.Index, string.Empty));
                continue;
            }

            var openBraceIndex = content.IndexOf('{', match.Index);
            if (openBraceIndex < 0)
            {
                continue;
            }

            var closeBraceIndex = FindMatchingBrace(content, openBraceIndex);
            if (closeBraceIndex <= openBraceIndex)
            {
                continue;
            }

            methods.Add(new ParsedMethod(
                nameGroup.Value,
                nameGroup.Index,
                content.Substring(openBraceIndex + 1, closeBraceIndex - openBraceIndex - 1)));
        }

        return methods;
    }

    private static int FindMatchingBrace(string content, int openBraceIndex)
    {
        var depth = 0;
        for (var i = openBraceIndex; i < content.Length; i++)
        {
            if (content[i] == '{')
            {
                depth++;
            }
            else if (content[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }
}

internal static class ComplexityCalculator
{
    private static readonly Regex IfRegex = new(@"\bif\b", RegexOptions.Compiled);
    private static readonly Regex CaseRegex = new(@"\bcase\b", RegexOptions.Compiled);
    private static readonly Regex ForRegex = new(@"\bfor\b", RegexOptions.Compiled);
    private static readonly Regex ForeachRegex = new(@"\bforeach\b", RegexOptions.Compiled);
    private static readonly Regex WhileRegex = new(@"\bwhile\b", RegexOptions.Compiled);
    private static readonly Regex DoRegex = new(@"\bdo\b", RegexOptions.Compiled);
    private static readonly Regex CatchRegex = new(@"\bcatch\b", RegexOptions.Compiled);

    public static int Calculate(string methodBody)
    {
        var complexity = 1;
        complexity += IfRegex.Matches(methodBody).Count;
        complexity += CaseRegex.Matches(methodBody).Count;
        complexity += ForRegex.Matches(methodBody).Count;
        complexity += ForeachRegex.Matches(methodBody).Count;
        complexity += WhileRegex.Matches(methodBody).Count;
        complexity += DoRegex.Matches(methodBody).Count;
        complexity += CatchRegex.Matches(methodBody).Count;
        complexity += CountToken(methodBody, "&&");
        complexity += CountToken(methodBody, "||");
        complexity += CountToken(methodBody, "?");
        return complexity;
    }

    private static int CountToken(string input, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = input.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }
}

internal static class TextLocator
{
    public static (int Line, int Column) LineColumn(string text, int index)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
                continue;
            }

            if (text[i] != '\r')
            {
                column++;
            }
        }

        return (line, column);
    }
}
