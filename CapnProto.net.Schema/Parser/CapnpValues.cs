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

      public FullName FullConstName;

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

   class DataValue : Value
   {
      public DataValue() : base(CapnpPrimitive.Data) { }
      public Byte[] Blob;

      public override string ToString()
      {
         return "«blob»";
      }
   }

   abstract class PrimitiveValue<TPrim> : Value
   {
      protected PrimitiveValue(CapnpPrimitive p) : base(p) { }

      public TPrim Value;

      public override string ToString()
      {
         return Value.ToString();
      }
   }

   class BoolValue : PrimitiveValue<Boolean>
   {
      public BoolValue() : base(CapnpPrimitive.Bool) { }
   }

   class Int8Value : PrimitiveValue<SByte>
   {
      public Int8Value() : base(CapnpPrimitive.Int8) { }
   }

   class Int16Value : PrimitiveValue<Int16>
   {
      public Int16Value() : base(CapnpPrimitive.Int16) { }
   }

   class Int32Value : PrimitiveValue<Int32>
   {
      public Int32Value() : base(CapnpPrimitive.Int32) { }
   }

   class Int64Value : PrimitiveValue<Int64>
   {
      public Int64Value() : base(CapnpPrimitive.Int64) { }
   }

   class UInt8Value : PrimitiveValue<Byte>
   {
      public UInt8Value() : base(CapnpPrimitive.UInt8) { }
   }

   class UInt64Value : PrimitiveValue<UInt64>
   {
      public UInt64Value() : base(CapnpPrimitive.UInt64) { }
   }

   class UInt32Value : PrimitiveValue<UInt32>
   {
      public UInt32Value() : base(CapnpPrimitive.UInt32) { }
   }

   class UInt16Value : PrimitiveValue<UInt16>
   {
      public UInt16Value() : base(CapnpPrimitive.UInt16) { }
   }

   class Float32Value : PrimitiveValue<Single>
   {
      public Float32Value() : base(CapnpPrimitive.Float32) { }
   }

   class Float64Value : PrimitiveValue<Double>
   {
      public Float64Value() : base(CapnpPrimitive.Float64) { }
   }

   class EnumValue : PrimitiveValue<Int32>
   {
      // not sure this is the best but will do for now
      public EnumValue() : base(CapnpPrimitive.Int32) { }
      public String Name;
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

   class UnionValue : Value
   {
      public UnionValue(CapnpUnion union) : base(union) { }

      public String FieldName;
      public Value Value;

      public override string ToString()
      {
         return "Union value for " + FieldName + " = " + Value;
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
