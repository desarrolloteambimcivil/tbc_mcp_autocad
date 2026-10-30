using System.Globalization;
using System.Text;

namespace Bimwright.Dwg.Plugin.Cad
{
    /// <summary>
    /// Turns stored AutoCAD text into what the drawing shows, for reading only: %% control codes
    /// (%%c diameter, %%d degree, %%p plus-minus, %%nnn character code), \U+XXXX escapes and, for
    /// MText, inline formatting codes. Rewrites keep working from the raw text.
    /// </summary>
    internal static class CadTextDecoder
    {
        internal const char Diameter = 'Ø'; // Ø, as AutoCAD fonts draw %%c

        /// <summary>Plain text of a DBText or attribute value.</summary>
        internal static string Plain(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            return DecodeControlCodes(DecodeUnicodeEscapes(raw));
        }

        /// <summary>Plain text of MText contents: formatting removed, paragraphs as \n.</summary>
        internal static string PlainMText(string contents)
        {
            if (string.IsNullOrEmpty(contents)) return contents;
            var sb = new StringBuilder(contents.Length);
            for (var i = 0; i < contents.Length; i++)
            {
                var c = contents[i];
                if (c == '{' || c == '}') continue;
                if (c != '\\' || i + 1 >= contents.Length) { sb.Append(c); continue; }
                var code = contents[++i];
                switch (code)
                {
                    case '\\': case '{': case '}': sb.Append(code); break;
                    case 'P': case 'N': case 'X': sb.Append('\n'); break;
                    case '~': sb.Append(' '); break;
                    case 'U':
                        if (TryUnicodeEscape(contents, i - 1, out var ch)) { sb.Append(ch); i += 5; }
                        else sb.Append(code);
                        break;
                    case 'S':
                        var end = contents.IndexOf(';', i + 1);
                        if (end < 0) { sb.Append(contents, i + 1, contents.Length - i - 1); i = contents.Length; break; }
                        var stack = contents.Substring(i + 1, end - i - 1).Trim();
                        sb.Append(stack.StartsWith("^") ? stack.Substring(1).Trim() : stack.Replace('^', '/').Replace('#', '/'));
                        i = end;
                        break;
                    case 'A': case 'C': case 'c': case 'F': case 'f': case 'H': case 'Q': case 'T': case 'W': case 'p':
                        var stop = contents.IndexOf(';', i + 1);
                        i = stop < 0 ? contents.Length : stop;
                        break;
                    case 'L': case 'l': case 'O': case 'o': case 'K': case 'k':
                        break;
                    default: sb.Append(code); break;
                }
            }
            return DecodeControlCodes(sb.ToString());
        }

        private static string DecodeUnicodeEscapes(string text)
        {
            if (text.IndexOf("\\U+", System.StringComparison.OrdinalIgnoreCase) < 0) return text;
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && TryUnicodeEscape(text, i, out var ch)) { sb.Append(ch); i += 6; }
                else sb.Append(text[i]);
            }
            return sb.ToString();
        }

        // \U+XXXX starting at index of the backslash.
        private static bool TryUnicodeEscape(string text, int backslash, out char value)
        {
            value = '\0';
            if (backslash + 7 > text.Length) return false;
            if ((text[backslash + 1] != 'U' && text[backslash + 1] != 'u') || text[backslash + 2] != '+') return false;
            if (!int.TryParse(text.Substring(backslash + 3, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)) return false;
            value = (char)code;
            return true;
        }

        private static string DecodeControlCodes(string text)
        {
            if (text.IndexOf("%%", System.StringComparison.Ordinal) < 0) return text;
            var sb = new StringBuilder(text.Length);
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] != '%' || i + 2 >= text.Length || text[i + 1] != '%') { sb.Append(text[i]); continue; }
                var code = text[i + 2];
                switch (char.ToLowerInvariant(code))
                {
                    case 'c': sb.Append(Diameter); i += 2; continue;
                    case 'd': sb.Append('°'); i += 2; continue;
                    case 'p': sb.Append('±'); i += 2; continue;
                    case '%': sb.Append('%'); i += 2; continue;
                    case 'u': case 'o': case 'k': i += 2; continue;
                }
                if (i + 4 < text.Length && char.IsDigit(code) && char.IsDigit(text[i + 3]) && char.IsDigit(text[i + 4]))
                {
                    sb.Append((char)int.Parse(text.Substring(i + 2, 3), CultureInfo.InvariantCulture));
                    i += 4;
                    continue;
                }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }
}
