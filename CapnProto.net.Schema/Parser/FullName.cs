using System;

namespace CapnProto.Schema.Parser
{
   struct FullName
   {
      private String _mRaw;

      public String Raw
      {
         get
         {
            return _mRaw ?? (_mRaw = String.Join(".", _mNames, _mFromIndex, _mNames.Length - _mFromIndex));
         }
      }

      private readonly Int32 _mFromIndex;
      private readonly String[] _mNames;

      private FullName(String[] parts, Int32 fromIndex)
      {
         _mNames = parts;
         _mFromIndex = fromIndex;
         _mRaw = null;
      }

      private FullName(String fullName, String[] parts, Int32 fromIndex)
         : this(parts, fromIndex)
      {
         _mRaw = fullName;
      }

      public Int32 Count { get { return _mNames.Length - _mFromIndex; } }
      public String this[Int32 i] { get { return _mNames[_mFromIndex + i]; } }

      public override String ToString()
      {
         return Raw;
      }

      public FullName From(Int32 index)
      {
         if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException();

         return new FullName(_mNames, index);
      }

      public override Int32 GetHashCode()
      {
         var x = 0;
         for (var i = _mFromIndex; i < _mNames.Length; i++)
            x = 37 * x + _mNames[i].GetHashCode();
         return x;
      }

      public override Boolean Equals(Object obj)
      {
         var other = obj as FullName?;
         if (other == null) return false;
         if (other.Value.Count != Count) return false;
         for (var i = 0; i < Count; i++)
            if (this[i] != other.Value[i]) return false;
         return true;
      }

      public static Boolean TryParse(String fullName, out FullName parsedName)
      {
         if (String.IsNullOrWhiteSpace(fullName)) goto ERROR;

         var pidx = fullName.IndexOf('.');
         if (pidx < 0)
         {
            parsedName = new FullName(fullName, new[] { fullName }, 0);
            return true;
         }

         if (pidx == 0) // global const ref
         {
            if (fullName.Length == 1) goto ERROR;
            pidx = fullName.IndexOf('.', 1);
            if (pidx >= 0) goto ERROR;
            if (Char.IsUpper(fullName[1])) goto ERROR;
            parsedName = new FullName(fullName, new[] { fullName.Substring(1) }, 0);
            return true;
         }

         var count = 2;
         for (pidx = fullName.IndexOf('.', pidx + 1); pidx >= 0; pidx = fullName.IndexOf('.', pidx + 1), count += 1) ;

         var names = new String[count];
         pidx = fullName.IndexOf('.');
         names[0] = fullName.Substring(0, pidx);
         for (var i = 1; i < count; i++)
         {
            var next = i == count - 1 ? fullName.Length : fullName.IndexOf('.', pidx + 1);

            names[i] = fullName.Substring(pidx + 1, next - pidx - 1);
            pidx = next;
         }

         // Validate.
         for (var i = 0; i < names.Length - 1; i++)
         {
            if (names[i].Length == 0)
               goto ERROR;

            // This name must refer to a scope thus be capitalized.
            if (!names[i].IsCapitalized())
               goto ERROR;
         }

         // The last part can refer either to a type or something else.
         var lastName = names[names.Length - 1];
         if (lastName.Length == 0)
            goto ERROR;

         parsedName = new FullName(fullName, names, 0);
         return true;

      ERROR:
         parsedName = new FullName();
         return false;
      }
   }
}
