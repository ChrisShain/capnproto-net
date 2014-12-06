using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace CapnProto.Schema.Parser
{
   internal static class Empty<T>
   {
      public static readonly T[] Array = new T[0];
      static Empty() { }
   }

   internal static class GenericExtensions
   {
      public static T[] SingleOrEmpty<T>(this T item) where T : class
      {
         return item == null ? Empty<T>.Array : new[] { item };
      }

      public static Boolean IsCapitalized(this String str)
      {
         return str != null && str.Length > 0 && Char.IsUpper(str[0]);
      }
   }

   internal static class RegularExtensions
   {
      public static String Group(this String expr)
      {
         return "(" + expr + ")";
      }

      public static String OneOrMore(this String expr)
      {
         return expr.Group() + "+";
      }

      public static String ZeroOrMore(this String expr)
      {
         return expr.Group() + "*";
      }

      public static String Or(this String expr, String right)
      {
         return expr.Group() + "|" + right.Group();
      }

      public static String Times(this String expr, Int32 number)
      {
         Debug.Assert(number >= 0);
         return expr.Group() + "{" + number.ToString(NumberFormatInfo.InvariantInfo) + "}";
      }
   }

   internal class NumberParser<T> where T : struct
   {
      private delegate Boolean TryParseDel(String s, NumberStyles st, IFormatProvider f, out T result);

      private static readonly TryParseDel _tryParse;
      private static readonly Func<String, Int32, T> _convert;

      //private static readonly Func<UInt64, T> _convert;

      internal static readonly T Min;
      internal static readonly T Max;

      static NumberParser()
      {
         const BindingFlags flags = BindingFlags.Static | BindingFlags.Public;
         var tryParseMethod = typeof(T).GetMethod("TryParse", flags, null, new System.Type[] { typeof(String), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType() }, null);

         var param = Expression.Parameter(typeof(String));
         var numStyle = Expression.Parameter(typeof(NumberStyles));
         var format = Expression.Parameter(typeof(IFormatProvider));
         var result = Expression.Parameter(typeof(T).MakeByRefType());

         var lambda = Expression.Lambda<TryParseDel>(Expression.Call(null, tryParseMethod, new[] { param, numStyle, format, result }), param, numStyle, format, result);
         _tryParse = lambda.Compile();

         //var convertMethod = typeof(Convert).GetMethod("To" + typeof(T).Name, flags, null, new[] { typeof(UInt64) }, null);
         //var p = Expression.Parameter(typeof(UInt64));
         //_convert = Expression.Lambda<Func<UInt64, T>>(Expression.Call(null, convertMethod, p), p).Compile();

         var convertMethod = typeof(Convert).GetMethod("To" + typeof(T).Name, flags, null, new[] { typeof(String), typeof(Int32) }, null);
         if (convertMethod != null)
         {
            var p = Expression.Parameter(typeof(String));
            var b = Expression.Parameter(typeof(Int32));
            _convert = Expression.Lambda<Func<String, Int32, T>>(Expression.Call(null, convertMethod, p, b), p, b).Compile();
         }

         Min = (T)typeof(T).GetField("MinValue", flags).GetValue(null);
         Max = (T)typeof(T).GetField("MaxValue", flags).GetValue(null);
      }

      public static Boolean TryParse(String s, NumberStyles style, IFormatProvider format, out T result)
      {
         return _tryParse(s, style, format, out result);
      }

      public static Boolean TryParseOctal(String s, out T result)
      {
         if (_convert == null) throw new InvalidOperationException();

         result = default(T);
         try
         {
            result = _convert(s, 8);
            return true;
         }
         catch
         {
            return false;
         }
      }

      // "generic" parse as octal by parsing to the largest "common" denominator.
      // *could* even do this entirely with an expression tree but.. the work
      //public static Boolean TryParseOctal(String s, out T result)
      //{
      //   result = default(T);

      //   try
      //   {
      //      checked
      //      {
      //         UInt64 v = 0, d = 0;
      //         for (var i = 0; i < s.Length; i++)
      //         {
      //            if (!UInt64.TryParse(s[i].ToString(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out d)) return false;
      //            v <<= 3;
      //            v += d;
      //         }
      //         result = _convert(v);
      //      }
      //   }
      //   catch (OverflowException)
      //   {
      //      return false;
      //   }

      //   return true;
      //}
   }
}
