using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Color = System.Windows.Media.Color;

namespace globulator;

public static class SyntaxHighlighter
{
    // a naiive approach, assumes json is formatted correctly
    private static bool IsString(string text) => text.EndsWith("\":") || text.EndsWith("\",");

    // returns an array of each word with a colour attributed to it
    public static (string, Color)[] HighlightJSONString(string jsonText)
    {
        JArray jsonTokens = JArray.Parse(jsonText);

        string[] itemArr = jsonTokens.ToString().Split(' ');
        (string, Color)[] highlightedTokens = new (string, Color)[itemArr.Length];

        for (int i = 0; i < itemArr.Length; i++)
        {
            string token = itemArr[i];

            if (IsString(token) && token.EndsWith(':'))
                highlightedTokens[i] = (token, Color.FromRgb(144, 238, 144));
            else if (token == "[" || token == "]")
                highlightedTokens[i] = (token, Color.FromRgb(220, 220, 220));
            else if (token == "{" || token == "}")
                highlightedTokens[i] = (token, Color.FromRgb(119, 136, 153));
            else if (IsString(token) && token.EndsWith(','))
                highlightedTokens[i] = (token, Color.FromRgb(224, 255, 255));
            else if (float.TryParse(token, out float _))
                highlightedTokens[i] = (token, Color.FromRgb(206, 255, 0));
            else if (bool.TryParse(token, out bool _))
                highlightedTokens[i] = (token, Color.FromRgb(255, 160, 122));
            else
                highlightedTokens[i] = (token, Color.FromRgb(255, 255, 255));
        }

        return highlightedTokens;
    }
}
