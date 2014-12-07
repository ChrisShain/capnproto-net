using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace CapnProto.Schema.Parser
{
   // Adds services for parsing to the parser.
   partial class CapnpParser
   {
      private static readonly String _kHexRange = "[0-9a-fA-F]";
      private static readonly String _kOctalRange = "[0-7]";

      private Exception _Error(String error, params Object[] args)
      {
         Int32 column = pos;
         var line = _GetLine(_mSource, pos);
         var lineNum = _FindLineNumber(_mSource, ref column);
         var positionalMessage = lineNum + ": " + line + "\r\n" + new String(' ', lineNum.ToString().Length) + "  "; // culture todo
         if (column > 0) positionalMessage += new String(' ', column);
         positionalMessage += "^";
         if (column >= 0) positionalMessage += " (" + column.ToString() + ")";

         var msg = String.Format(error, args);
         throw new Exception(msg + "\r\n\r\n" + positionalMessage);
      }

      static String _GetLine(String source, Int32 position)
      {
         if (position > source.Length - 1) position = source.Length - 1;
         if (position < 0) position = 0;
         var cr = source.IndexOf('\r', position);
         var lf = source.IndexOf('\n', position);
         var end = cr < 0 ? source.Length : cr;
         end = lf < 0 ? end : Math.Min(lf, end);
         cr = position == 0 ? -1 : source.LastIndexOf('\r', position - 1);
         lf = position == 0 ? -1 : source.LastIndexOf('\n', position - 1);
         if (source[position] == '\n' && cr == position - 1)
         {
            cr = source.LastIndexOf('\r', position - 2);
            end -= 1;
         }
         var start = cr < 0 ? 0 : cr + 1;
         start = lf < 0 ? start : Math.Max(start, lf + 1);
         return source.Substring(start, Math.Max(0, end - start));
      }

      static Int32 _FindLineNumber(String source, ref Int32 positionInLine)
      {
         Int32 line, idx, pos = positionInLine, initialPos = positionInLine;
         for (line = 1, idx = 0; ; line++, positionInLine = initialPos - idx, idx++)
         {
            var cr = source.IndexOf('\r', idx);
            var lf = source.IndexOf('\n', idx);
            if (cr < 0 && lf < 0) return line;
            if (cr >= pos && lf >= pos) return line;
            if (lf < cr)
            {
               if (lf > 0)
               {
                  line++;
                  positionInLine = initialPos - lf - 1;
               }
               if (cr >= pos) return line;
               idx = cr + 1;
            }
            else if (lf > cr + 1)
            {
               if (cr > 0)
               {
                  line++;
                  positionInLine = initialPos - cr - 1;
               }
               if (lf >= pos) return line;
               idx = lf + 1;
            }
            else
               idx = lf + 1; // as lf >= cr
         }
      }

      private Char _ParseHexDigit(Char c)
      {
         if (c < 'A') return (Char)(c - '0');
         if (c < 'a') return (Char)(c - 'A' + 10);
         return (Char)(c - 'a' + 10);
      }

      private static readonly Int64 _sWhitespace = (1L << '\f') | (1L << '\r') | (1L << '\n') | (1L << '\t') | (1L << '\v') | (1L << ' ');

      private static Boolean _IsWhiteSpace(Char c)
      {
         return c <= 0x3F && (_sWhitespace & (1L << c)) != 0;
      }

      private IEnumerable<T> _AdvanceCommaSep<T>(String open, String close, Func<T> parseItem)
      {
         _Advance(open);
         while (!_Peek(close))
         {
            yield return parseItem();
            if (!_OptAdvance(",")) break;
         }
         _Advance(close);
      }

      private Char _AdvanceChar()
      {
         if (pos > _mSource.Length - 1)
            _Error("Unexpected end of input.");
         return _mSource[pos++];
      }

      private void _AdvanceWhiteSpace()
      {
         for (; pos < _mSource.Length; )
            if (_mSource[pos] == '#')
               _AdvanceComment();
            else if (_IsWhiteSpace(_mSource[pos]))
               pos += 1;
            else
               break;
      }

      private void _AdvanceComment()
      {
         Debug.Assert(_mSource[pos] == '#');

         pos += 1;
         if (pos == _mSource.Length) return;
         var idx = _mSource.IndexOf('\n', pos);
         if (idx < 0) pos = _mSource.Length;
         else pos = idx + 1;
      }

      private void _Expect(String token)
      {
         if (pos + token.Length > _mSource.Length)
            _Error("Expected '{0}'.", token);

         for (var i = 0; i < token.Length; i++)
            if (token[i] != _mSource[pos + i])
               _Error("Expected '{0}'.", token);
      }

      private void _Advance(String token, Boolean skipWhiteSpace = true)
      {
         _Expect(token);
         pos += token.Length;

         if (skipWhiteSpace)
            _AdvanceWhiteSpace();
      }

      private String _AdvanceOneOf(String firstToken, params String[] tokens)
      {
         // todo -> use _OptAd..
         if (_OptAdvance(firstToken)) return firstToken;
         foreach (var t in tokens.Where(_t => _OptAdvance(_t)))
            return t;

         throw _Error("Expected one of: {0}, {1}", firstToken, String.Join(", ", tokens));
      }

      private Boolean _OptAdvanceOneOf(out String foundToken, params String[] tokens)
      {
         foundToken = null;
         if (tokens == null || tokens.Length == 0) return false;
         foreach (var t in tokens.Where(_t => _OptAdvance(_t)))
         {
            foundToken = t;
            return true;
         }
         return false;
      }

      private String _AdvanceUntil(Char match)
      {
         for (var start = pos; pos < _mSource.Length; pos++)
            if (_mSource[pos] == match)
               return _mSource.Substring(start, pos - start);

         throw _Error("Expected '{0}', unexpected end of input.", match);
      }

      // todo: better error, pass on error message?
      private String _AdvanceExpr(String regex, String expectedToken = null)
      {
         String result = null;
         if (!_OptAdvanceExpr(regex, out result))
         {
            if (expectedToken == null)
               _Error("Expected regex match: " + regex);
            else
               _Error("Expected " + expectedToken);
         }

         return result;
      }

      private Boolean _OptAdvanceExpr(String regex, out String token)
      {
         token = null;
         var r = new Regex(@"\G(" + regex + ")", RegexOptions.CultureInvariant);
         var m = r.Match(_mSource, pos);
         if (!m.Success) return false;
         pos += m.Length;
         _AdvanceWhiteSpace();
         token = m.Value;
         return true;
      }

      private Boolean _OptAdvance(String token, Int32 from)
      {
         var curPos = pos;
         pos = from;
         var b = _OptAdvance(token);
         if (b) return true;
         pos = curPos;
         return false;
      }

      private Boolean _OptAdvance(String token, Boolean skipWhiteSpace = true, Boolean requireWhiteSpace = false)
      {
         Debug.Assert(skipWhiteSpace || !requireWhiteSpace);

         if (pos + token.Length > _mSource.Length) return false;

         var i = 0;
         for (i = 0; i < token.Length; i++)
            if (token[i] != _mSource[pos + i]) return false;

         pos += i;

         if (skipWhiteSpace)
         {
            var prews = pos;
            _AdvanceWhiteSpace();

            if (requireWhiteSpace && prews == pos)
            {
               pos = prews - i;
               return false;
            }
         }

         return true;
      }

      private Boolean _PeekExpr(String expr)
      {
         var c = pos;
         String @null;
         if (_OptAdvanceExpr(expr, out @null))
         {
            pos = c;
            return true;
         }
         return false;
      }

      private Boolean _Peek(String token)
      {
         var c = pos;
         if (_OptAdvance(token, skipWhiteSpace: false))
         {
            pos = c;
            return true;
         }
         return false;
      }
   }
}
