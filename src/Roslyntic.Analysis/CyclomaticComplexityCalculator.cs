using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyntic.Analysis;

/// <summary>
/// Computes cyclomatic complexity for a method declaration.
/// Baseline = 1. Each branching construct adds 1:
///   if / else if / switch case labels / switch expression arms / for / foreach / while / do / catch / ?: / &amp;&amp; / ||
/// </summary>
public static class CyclomaticComplexityCalculator
{
    public static int Calculate(MethodDeclarationSyntax method)
    {
        int complexity = 1;

        foreach (var node in method.DescendantNodes())
        {
            complexity += node switch
            {
                IfStatementSyntax                                               => 1,
                SwitchSectionSyntax s when s.Labels.Any(l => l is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax) => s.Labels.Count(l => l is CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax),
                SwitchExpressionArmSyntax                                       => 1,
                ForStatementSyntax                                              => 1,
                ForEachStatementSyntax                                          => 1,
                WhileStatementSyntax                                            => 1,
                DoStatementSyntax                                               => 1,
                CatchClauseSyntax                                               => 1,
                ConditionalExpressionSyntax                                     => 1,
                BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) => 1,
                BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalOrExpression)  => 1,
                _                                                               => 0,
            };
        }

        return complexity;
    }
}
