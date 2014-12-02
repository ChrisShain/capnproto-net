using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapnProto.Schema.Parser
{

   abstract class Value
   {
      public Value(CapnpType type)
      {
         Type = type;
      }

      public readonly CapnpType Type;
   }

   class ConstRefValue : Value
   {
      public ConstRefValue(CapnpType constType)
         : base(constType)
      {
         Debug.Assert(!(constType is CapnpReference));
      }

      public String FullConstName;

      public override string ToString()
      {
         return "Const ref to " + FullConstName;
      }
   }

   class UnresolvedValue : Value
   {
      public UnresolvedValue(CapnpType referenceType) : base(referenceType) { }

      public Int32 Position; // todo
      public String RawData;

      public override string ToString()
      {
         return "Unresolved: \"" + RawData + "\"";
      }
   }

   class VoidValue : Value
   {
      public VoidValue() : base(CapnpPrimitive.Void) { }

      public override string ToString()
      {
         return "«void»";
      }
   }

   class TextValue : Value
   {
      public TextValue() : base(CapnpPrimitive.Text) { }
      public String Value;

      public override string ToString()
      {
         return "\"" + Value + "\"";
      }
   }

   // todo: what does a data value look like?
   class DataValue : Value
   {
      public DataValue() : base(CapnpPrimitive.Data) { }
      public Byte[] Blob;

      public override string ToString()
      {
         return "blob"; // todo
      }
   }

   class BoolValue : Value
   {
      public BoolValue() : base(CapnpPrimitive.Bool) { }
      public Boolean Value;

      public override string ToString()
      {
         return Value ? "true" : "false";
      }
   }

   class Int8Value : Value
   {
      public Int8Value()
         : base(CapnpPrimitive.Int8)
      {
      }

      public SByte Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class Int16Value : Value
   {
      public Int16Value() : base(CapnpPrimitive.Int16) { }
      public Int16 Value;
      public override string ToString()
      {
         return base.ToString();
      }
   }

   class Int32Value : Value
   {
      public Int32Value() : base(CapnpPrimitive.Int32) { }
      public Int32 Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class Int64Value : Value
   {
      public Int64Value() : base(CapnpPrimitive.Int64) { }
      public Int64 Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class UInt8Value : Value
   {
      public UInt8Value() : base(CapnpPrimitive.UInt8) { }
      public Byte Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class UInt64Value : Value
   {
      public UInt64Value() : base(CapnpPrimitive.UInt64) { }
      public UInt64 Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class UInt32Value : Value
   {
      public UInt32Value() : base(CapnpPrimitive.UInt32) { }
      public UInt32 Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class UInt16Value : Value
   {
      public UInt16Value() : base(CapnpPrimitive.UInt16) { }
      public UInt16 Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class Float32Value : Value
   {
      public Float32Value() : base(CapnpPrimitive.Float32) { }
      public Single Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class Float64Value : Value
   {
      public Float64Value() : base(CapnpPrimitive.Float64) { }
      public Double Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class ListValue : Value
   {
      public ListValue(CapnpList type) : base(type) { }

      public List<Value> Values;

      public override string ToString()
      {
         return "[" + String.Join(", ", Values) + "]";
      }
   }

   class StructValue : Value
   {
      public StructValue(CapnpStruct type) : base(type) { }
      public Dictionary<String, Value> FieldValues;

      public override string ToString()
      {
         return "struct value todo";
      }
   }
}
