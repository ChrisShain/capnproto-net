using System;
using System.Diagnostics;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   struct NamePart // < todo better name
   {
      internal NamePart(String name, FullName[] typeParams)
      {
         Debug.Assert(!name.Contains("."));
         Name = name;
         TypeParameters = typeParams ?? Empty<FullName>.Array;
      }

      public readonly String Name;
      public readonly FullName[] TypeParameters;

      public override String ToString()
      {
         if (TypeParameters.Length == 0)
            return Name;

         return Name + "(" + String.Join(", ", TypeParameters) + ")";
      }

      public override Boolean Equals(Object obj)
      {
         throw new Exception("todo");
      }
   }

   struct FullName
   {
      private String _mRaw;

      public String Raw
      {
         get
         {
            if (_mRaw == null)
               _mRaw = (IsTopLevelConst ? "." : "") + String.Join(".", _mNames.Select(n => n.ToString()).ToArray(), _mFromIndex, _mNames.Length - _mFromIndex); // todo
            return _mRaw;
         }
      }

      private readonly Int32 _mFromIndex;
      private readonly NamePart[] _mNames;

      //internal FullName(Name[] parts, Int32 fromIndex)
      //{
      //   _mNames = parts;
      //   _mFromIndex = fromIndex;
      //   _mRaw = null;

      //   IsTopLevelConst = false;
      //}

      private FullName(NamePart[] parts, Int32 fromIndex)
      {
         Debug.Assert(parts.All(p => p.TypeParameters != null));

         _mNames = parts;
         _mFromIndex = fromIndex;
         _mRaw = null;
         IsTopLevelConst = false;
      }

      internal FullName(NamePart[] parts, Boolean isTopLevelConst = false)
      {
         _mNames = parts;
         _mFromIndex = 0;
         _mRaw = null;

         Debug.Assert(!isTopLevelConst || parts.Length == 1);
         Debug.Assert(parts.All(p => p.TypeParameters != null));

         IsTopLevelConst = isTopLevelConst;
      }

      public Boolean IsSimple
      {
         get
         {
            return Count == 1 && this[0].TypeParameters.Length == 0;
         }
      }

      public NamePart Last
      {
         get { return this[Count - 1]; }
      }

      public readonly Boolean IsTopLevelConst;

      public Boolean CouldBeConstRef
      {
         // A const ref always contains a period.
         get { return (IsTopLevelConst || Count > 1) && this[Count - 1].TypeParameters.Length == 0 && !this[Count - 1].Name.IsCapitalized(); }
      }

      public Boolean HasGenericParameters
      {
         get
         {
            return _mNames.Skip(_mFromIndex).Any(n => n.TypeParameters.Length > 0);
         }
      }

      public Int32 Count { get { return _mNames.Length - _mFromIndex; } }
      public NamePart this[Int32 i] { get { return _mNames[_mFromIndex + i]; } }

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
            if (!(this[i].Equals(other.Value[i]))) return false;
         return true;
      }
   }
}
