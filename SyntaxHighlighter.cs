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
    // A naïve approach potentially
    private static bool IsString(string text) => text.EndsWith("\":") || text.EndsWith("\",\r\n");
    private static readonly int INDENT_SIZE = 2;
    
    // Returns an array of each token with a colour attributed to it
    // not sure why, but for the final closing bracket, there is always a space prefixing it
    public static List<(string, Color)> HighlightJSONString(string jsonText)
    {
        JToken jsonTokens = JToken.Parse(jsonText);

        string[] tokenArr = jsonTokens.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<(string, Color)> highlightedTokens = new();

        // controls the amount of indents
        int depth = 0;
        // this is used to determine whether or not to just put a space or a full indent
        int multiplier = 1;

        Dictionary<Func<string, bool>, Color> colourMappings = new()
        {
            { token => IsString(token) && token.EndsWith(':'), Color.FromRgb(144, 238, 144) }, // String keys
            { token => IsString(token) && token.EndsWith(",\r\n"), Color.FromRgb(220, 20, 60) }, // String values
            { token => token.StartsWith('{') || token.StartsWith('['), Color.FromRgb(0, 183, 235) }, // Opening braces/brackets
            { token => token.StartsWith('}') || token.StartsWith(']'), Color.FromRgb(0, 139, 139) }, // Closing braces/brackets
            { token => float.TryParse(token, out _) || float.TryParse(token[..^3], out _), Color.FromRgb(206, 255, 0) }, // Numbers
            { token => bool.TryParse(token, out _) || bool.TryParse(token[..^3], out _), Color.FromRgb(255, 160, 122) }, // Booleans
        };

        foreach (string token in tokenArr)
        {
            Color tokenColour = Color.FromRgb(255, 255, 255); // Default colour
            
            foreach (var entry in colourMappings)
            {
                if (entry.Key(token))
                {
                    tokenColour = entry.Value;
                    break;
                }
            }

            int spaceCount = depth * INDENT_SIZE * multiplier + multiplier ^ 1;

            // can't figure out why, but closing brackets have a depth one higher than they should be, so i just minus one here
            if (token.StartsWith('}') || token.StartsWith(']'))
            {
                spaceCount = (depth - 1) * INDENT_SIZE;
                depth--;
            }
            else if (token.StartsWith('{') || token.StartsWith('['))
                depth++;

            // this prevents the value from key from also having indents
            // it allows the equation to instead return just one space
            multiplier = token.EndsWith(':') ? 0 : 1;

            string formattedToken = new string(' ', spaceCount) + token;
            highlightedTokens.Add((formattedToken, tokenColour));
        }

        return highlightedTokens;
    }
}
