using System;

namespace CapnProto.Schema.Parser
{
   abstract class Member
   {
      public String Name;
      public Int32 Number;
      public Annotation Annotation;
      public abstract override String ToString();
   }

   class Field : Member
   {
      public CapnpType Type;
      public Value Value;

      public override string ToString()
      {
         var namedType = Type as CapnpNamedType;
         var typeStr = namedType == null ? Type.ToString() : namedType.Name;
         return String.Format("Field: {0} @{1} :{2} = {3} {4}", Name, Number, typeStr, Value, Annotation);
      }
   }

   class Method : Member
   {
      public Parameter[] Arguments = Empty<Parameter>.Array;
      public Parameter ReturnType;

      public override string ToString()
      {
         return Name + " @" + Number + "(" + String.Join<Parameter>(", ", Arguments) + ") -> (" + ReturnType + ")";
      }
   }

   class Parameter : Member
   {
      public CapnpType Type;
      public Value DefaultValue;

      public override string ToString()
      {
         var d = DefaultValue == null ? "" : " = " + DefaultValue;
         var a = Annotation == null ? "" : " " + Annotation;
         return Name + " :" + Type + d + a;
      }
   }

   class Enumerant : Member
   {
      public override string ToString()
      {
         var a = Annotation == null ? "" : " " + Annotation;
         return Name + " @" + Number + a;
      }
   }

   class Annotation
   {
      public CapnpType Declaration;

      public Value Argument;

      public override string ToString()
      {
         var args = Argument == null ? "" : "(" + Argument + ")";
         return "$" + Declaration + args;
      }
   }

   class CapnpModule : CapnpIdType
   {
      public CapnpModule() { }

      public CapnpStruct[] Structs;
      public CapnpInterface[] Interfaces;
      public CapnpConst[] Constants;
      public CapnpEnum[] Enumerations;
      public CapnpAnnotation[] AnnotationDefs;
      public CapnpUsing[] Usings;
      public Annotation[] Annotations;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitModule(this);
      }

      // todo: these are bad, clean up ToStrings, lineendings (dont assume crlf)
      public override string ToString()
      {
         return "Compiled Source: \r\n" +
                "Id = " + Id + "\r\n" +
                String.Join<CapnpStruct>("\r\n", Structs) + "\r\n" +
                String.Join<CapnpInterface>("\r\n", Interfaces) + "\r\n" +
                String.Join<CapnpConst>("\r\n", Constants) + "\r\n" +
                String.Join<CapnpEnum>("\r\n", Enumerations) + "\r\n" +
                String.Join<CapnpAnnotation>("\r\n", AnnotationDefs) + "\r\n" +
                String.Join<Annotation>("\r\n", Annotations);
      }
   }

   enum PrimitiveName
   {
      AnyPointer, Void, Bool, Int8, Int16, Int32, Int64, Text, Data, UInt32, UInt8, UInt16, UInt64, Float32, Float64
   }

   class CapnpType
   {
      protected CapnpType() { }

      public static readonly CapnpType Unit = new CapnpType();

      public virtual Boolean IsNumeric { get { return false; } }

      internal protected virtual CapnpType Accept(CapnpVisitor visitor)
      {
         throw new NotImplementedException();
      }
   }

   class CapnpPrimitive : CapnpType
   {
      public readonly PrimitiveName Kind;

      private CapnpPrimitive(PrimitiveName kind) { Kind = kind; }

      public static readonly CapnpPrimitive AnyPointer = new CapnpPrimitive(PrimitiveName.AnyPointer);
      public static readonly CapnpPrimitive Void = new CapnpPrimitive(PrimitiveName.Void);
      public static readonly CapnpPrimitive Bool = new CapnpPrimitive(PrimitiveName.Bool);
      public static readonly CapnpPrimitive Int8 = new CapnpPrimitive(PrimitiveName.Int8);
      public static readonly CapnpPrimitive Int16 = new CapnpPrimitive(PrimitiveName.Int16);
      public static readonly CapnpPrimitive Int32 = new CapnpPrimitive(PrimitiveName.Int32);
      public static readonly CapnpPrimitive Int64 = new CapnpPrimitive(PrimitiveName.Int64);
      public static readonly CapnpPrimitive UInt8 = new CapnpPrimitive(PrimitiveName.UInt8);
      public static readonly CapnpPrimitive UInt16 = new CapnpPrimitive(PrimitiveName.UInt16);
      public static readonly CapnpPrimitive UInt32 = new CapnpPrimitive(PrimitiveName.UInt32);
      public static readonly CapnpPrimitive UInt64 = new CapnpPrimitive(PrimitiveName.UInt64);
      public static readonly CapnpPrimitive Float32 = new CapnpPrimitive(PrimitiveName.Float32);
      public static readonly CapnpPrimitive Float64 = new CapnpPrimitive(PrimitiveName.Float64);
      public static readonly CapnpPrimitive Text = new CapnpPrimitive(PrimitiveName.Text);
      public static readonly CapnpPrimitive Data = new CapnpPrimitive(PrimitiveName.Data);

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitPrimitive(this);
      }

      public override Boolean IsNumeric
      {
         get
         {
            return Kind == PrimitiveName.Int8 || Kind == PrimitiveName.Int16 || Kind == PrimitiveName.Int32 || Kind == PrimitiveName.Int64 ||
                   Kind == PrimitiveName.UInt8 || Kind == PrimitiveName.UInt16 || Kind == PrimitiveName.UInt32 || Kind == PrimitiveName.UInt64 ||
                   Kind == PrimitiveName.Float32 || Kind == PrimitiveName.Float64;
         }
      }

      public override string ToString()
      {
         return Kind.ToString();
      }
   }

   class CapnpList : CapnpType
   {
      public CapnpType Parameter;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitList(this);
      }

      public override string ToString()
      {
         var namedType = Parameter as CapnpNamedType;
         var paramStr = namedType == null ? Parameter.ToString() : namedType.Name;
         return "List(" + paramStr + ")";
      }
   }

   enum AnnotationTypes
   {
      file, @struct, field, union, enumerant, @enum, method, param, annotation, @const, @interface, group,
      any
   }

   class CapnpNamedType : CapnpType
   {
      public String Name;
   }
   class CapnpIdType : CapnpNamedType
   {
      public UInt64? Id;
   }
   class CapnpAnnotatedType : CapnpIdType
   {
      public Annotation Annotation;
   }

   class CapnpAnnotation : CapnpAnnotatedType
   {
      public CapnpType ArgumentType;

      public AnnotationTypes[] Targets;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitAnnotationDecl(this);
      }

      public override string ToString()
      {
         var arg = ArgumentType == null ? "" : "(" + ArgumentType + ")";
         return "Annotation: " + Name + " " + arg + " " + Annotation + " targets " + String.Join<AnnotationTypes>(", ", Targets);
      }
   }

   class CapnpUsing : CapnpNamedType
   {
      // note: can have null name

      public CapnpType Target;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitUsing(this);
      }

      public override String ToString()
      {
         return "Using: " + Name + " = " + Target;
      }
   }

   class CapnpImport : CapnpType
   {
      public String File;

      public CapnpType Type;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitImport(this);
      }

      public override string ToString()
      {
         return Type == null ? "Import: " + File :
                               "Import: " + File + "." + Type;
      }
   }

   class CapnpComposite : CapnpAnnotatedType
   {
      public CapnpType[] NestedTypes;

      public CapnpUsing[] Usings;

      public override string ToString()
      {
         return String.Join<CapnpType>("\r\n", NestedTypes);
      }
   }

   class CapnpStruct : CapnpComposite
   {
      public Field[] Fields;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitStruct(this);
      }

      public override string ToString()
      {
         var annot = Annotation == null ? "" : Annotation.ToString();
         return "Struct " + Name + " " + annot + "\r\n   " + String.Join<Field>("\r\n   ", Fields) + "\r\n\r\n" + base.ToString();
      }
   }

   class CapnpConst : CapnpNamedType
   {
      public Value Value;

      public Annotation Annotation;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitConst(this);
      }

      public override string ToString()
      {
         var a = Annotation == null ? "" : Annotation + " ";
         return "Const " + Name + " " + a + "=" + Value;
      }
   }

   class CapnpReference : CapnpType
   {
      public String FullName;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitReference(this);
      }

      public override string ToString()
      {
         return "Reference to " + FullName;
      }
   }

   class CapnpEnum : CapnpAnnotatedType
   {
      public Enumerant[] Enumerants;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitEnum(this);
      }

      public override string ToString()
      {
         return "Enum " + Name + " " + Annotation + "\r\n   " + String.Join<Enumerant>("\r\n   ", Enumerants);
      }
   }

   class CapnpInterface : CapnpComposite
   {
      // todo: validaiotn that these are interfaces etc
      public CapnpType[] BaseInterfaces = new CapnpType[0]; // empty<>.ar todo

      public Method[] Methods;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitInterface(this);
      }

      public override string ToString()
      {
         var extends = BaseInterfaces.Length == 0 ? "" : " extends " + String.Join<CapnpType>(", ", BaseInterfaces);
         return "Interface " + Name + extends + " " + Annotation + "\r\n   " + String.Join<Method>("\r\n   ", Methods) + "\r\n\r\n" + base.ToString();
      }
   }

   // Can these introduce scopes?
   class CapnpUnion : CapnpType
   {
      public Field[] Fields;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitUnion(this);
      }

      public override string ToString()
      {
         return "Union:\r\n" + String.Join<Field>("\r\n", Fields);
      }
   }

   class CapnpGroup : CapnpType
   {
      public Field[] Fields;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitGroup(this);
      }

      public override string ToString()
      {
         return "Group:\r\n" + String.Join<Field>("\r\n", Fields);
      }
   }
}
