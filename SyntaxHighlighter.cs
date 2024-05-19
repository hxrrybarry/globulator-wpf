using Newtonsoft.Json.Linq;
using Color = System.Windows.Media.Color;

namespace globulator;

public static class SyntaxHighlighter
{
    private static bool IsString(string text) => text.EndsWith("\":") || text.EndsWith("\",\r") || text.EndsWith('"');
    private static readonly int INDENT_SIZE = 4;

    // this is a potentially naive approach to split a json string
    // this is because a key may have a space in the string, which this parser-
    // - will interpret as a new token
    private static readonly char[] delimeters = ['\n', ' '];

    // Returns an array of each token with a colour attributed to it
    public static List<(string, Color)> HighlightJSONString(string jsonText)
    {
        JToken jsonTokens = JToken.Parse(jsonText);

        string[] tokenStrings = jsonTokens.ToString().Split(delimeters, StringSplitOptions.RemoveEmptyEntries);
        List<(string, Color)> highlightedTokens = new();

        // controls the amount of indents
        int depth = 0;
        // this is used to determine whether or not to just put a space or a full indent
        int isIndentOrSpace = 1;

        Dictionary<Func<string, bool>, Color> colourMappings = new()
        {
            { token => IsString(token) && token.EndsWith(':'), Color.FromRgb(144, 238, 144) }, // String keys
            { token => IsString(token) && token.EndsWith(",\r"), Color.FromRgb(220, 20, 60) }, // String values
            { token => token.StartsWith('{') || token.StartsWith('['), Color.FromRgb(0, 183, 235) }, // Opening braces/brackets
            { token => token.StartsWith('}') || token.StartsWith(']'), Color.FromRgb(0, 139, 139) }, // Closing braces/brackets
            { token => float.TryParse(token, out _) || float.TryParse(token[..^2], out _), Color.FromRgb(206, 255, 0) }, // Numbers
            { token => bool.TryParse(token, out _) || bool.TryParse(token[..^2], out _), Color.FromRgb(255, 160, 122) } // Booleans
        };

        foreach (string token in tokenStrings)
        {
            Color tokenColour = Color.FromRgb(255, 228, 225); // Default colour
            
            foreach (var entry in colourMappings)
            {
                if (entry.Key(token))
                {
                    tokenColour = entry.Value;
                    break;
                }
            }

            int spaceCount = depth * INDENT_SIZE * isIndentOrSpace + isIndentOrSpace ^ 1;

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
            isIndentOrSpace = token.EndsWith(':') ? 0 : 1;

            string formattedToken = new string(' ', spaceCount) + token;
            highlightedTokens.Add((formattedToken, tokenColour));
        }

        return highlightedTokens;
    }
}
