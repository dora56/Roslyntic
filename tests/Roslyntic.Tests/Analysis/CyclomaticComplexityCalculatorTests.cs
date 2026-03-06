using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Roslyntic.Analysis;
using Xunit;

namespace Roslyntic.Tests.Analysis;

public class CyclomaticComplexityCalculatorTests
{
    // Helper: parse a method out of an inline class definition
    private static MethodDeclarationSyntax ParseMethod(string methodBody)
    {
        var source = $"class C {{ {methodBody} }}";
        var tree = CSharpSyntaxTree.ParseText(source);
        return tree.GetRoot()
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First();
    }

    // ── baseline ──────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_EmptyMethod_ReturnsOne()
    {
        // Given: a method with no branching
        var method = ParseMethod("void M() {}");

        // When
        var complexity = CyclomaticComplexityCalculator.Calculate(method);

        // Then: baseline = 1
        Assert.Equal(1, complexity);
    }

    // ── branching constructs ───────────────────────────────────────────────────

    [Fact]
    public void Calculate_SingleIf_ReturnsTwo()
    {
        // Given: one if adds 1
        var method = ParseMethod("void M(bool x) { if (x) {} }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_IfWithElseIf_ReturnsThree()
    {
        // Given: if + else if adds 2
        var method = ParseMethod("void M(bool a, bool b) { if (a) {} else if (b) {} }");

        Assert.Equal(3, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_SwitchWithThreeCaseLabels_ReturnsFour()
    {
        // Given: each case label (excluding default) adds 1
        var method = ParseMethod(
            "void M(int x) { switch(x) { case 1: break; case 2: break; case 3: break; } }");

        Assert.Equal(4, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ForLoop_ReturnsTwo()
    {
        var method = ParseMethod("void M() { for (int i = 0; i < 10; i++) {} }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ForeachLoop_ReturnsTwo()
    {
        var method = ParseMethod("void M(int[] xs) { foreach (var x in xs) {} }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_WhileLoop_ReturnsTwo()
    {
        var method = ParseMethod("void M(bool cond) { while (cond) {} }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_DoWhileLoop_ReturnsTwo()
    {
        var method = ParseMethod("void M() { do {} while (true); }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_CatchClause_ReturnsTwo()
    {
        var method = ParseMethod(
            "void M() { try {} catch (System.Exception) {} }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_ConditionalOperator_ReturnsTwo()
    {
        // Given: ternary operator adds 1
        var method = ParseMethod("int M(bool x) { return x ? 1 : 0; }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_LogicalAnd_ReturnsTwo()
    {
        var method = ParseMethod("bool M(bool a, bool b) { return a && b; }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_LogicalOr_ReturnsTwo()
    {
        var method = ParseMethod("bool M(bool a, bool b) { return a || b; }");

        Assert.Equal(2, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_SwitchExpressionWithTwoArms_ReturnsThree()
    {
        // Given: switch expression with 2 arms adds 2 (baseline 1 + 2 arms)
        var method = ParseMethod("int M(int x) { return x switch { 1 => 10, _ => 0 }; }");

        Assert.Equal(3, CyclomaticComplexityCalculator.Calculate(method));
    }

    // ── compound method ────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_CompoundMethod_SumsAllConstructs()
    {
        // Given: if(+1) + foreach(+1) + &&(+1) = 4 total
        var method = ParseMethod(
            "void M(bool flag, int[] xs) { if (flag && flag) { foreach (var x in xs) {} } }");

        Assert.Equal(4, CyclomaticComplexityCalculator.Calculate(method));
    }

    // ── threshold detection ────────────────────────────────────────────────────

    [Fact]
    public void Calculate_MethodAtDefaultThreshold_ReturnsExactly15()
    {
        // Given: 14 if-statements → 15 total
        var ifs = string.Concat(Enumerable.Range(0, 14).Select(i => $"if (x) {{}} "));
        var method = ParseMethod($"void M(bool x) {{ {ifs} }}");

        Assert.Equal(15, CyclomaticComplexityCalculator.Calculate(method));
    }

    [Fact]
    public void Calculate_MethodExceedingDefaultThreshold_ReturnsValueAbove15()
    {
        // Given: 15 if-statements → 16 total
        var ifs = string.Concat(Enumerable.Range(0, 15).Select(i => $"if (x) {{}} "));
        var method = ParseMethod($"void M(bool x) {{ {ifs} }}");

        Assert.True(CyclomaticComplexityCalculator.Calculate(method) > 15);
    }
}
