using Samples.Infrastructure;

namespace Samples.UI;

/// <summary>
/// Intentionally complex method to trigger AGCOMP0001 (complexity > 15).
/// </summary>
public class Controller
{
    public string Process(int x, bool a, bool b, bool c, bool d, int[] items)
    {
        if (x == 0) return "zero";
        if (x > 100) return "big";
        if (a && b) return "ab";
        if (a || c) return "ac";
        if (b && c) return "bc";
        if (d) return "d";

        string result = "";
        foreach (var item in items)
        {
            if (item > 0)
                result += "+";
            else if (item < 0)
                result += "-";
            else
                result += "0";
        }

        switch (x % 3)
        {
            case 0: result += "x0"; break;
            case 1: result += "x1"; break;
            case 2: result += "x2"; break;
        }

        return result.Length > 0 ? result : "empty";
    }
}
