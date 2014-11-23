using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

// Todo:
// - definitions of whitespace, identifiers (e.g. \w is probably not good)
// - annotations with struct arguments
// - more primitive types (floats)
// - second pass:
//   * parse imports
//   * resolve references
//   * parse unresolved default values
// - test, test, test
// - compare with the c++ impl intsead of just the examples

namespace CapnProto.Schema.Parser
{
   class Field
   {
      public String Name;
      public Int32 Number;
      public CapnpType Type;
      public Value Value;

      public Annotation Annotation;

      public override string ToString()
      {
         return String.Format("Field: {0} @{1} :{2} = {3} {4}", Name, Number, Type, Value, Annotation);
      }
   }

   class Method
   {
      public String Name;
      public Int32 Number;

      //public CapnpLambda Type;
      public Parameter[] Arguments;
      public Parameter ReturnType;

      public Annotation Annotation;

      public override string ToString()
      {
         return Name + " @" + Number + "(" + String.Join(", ", (IEnumerable<Parameter>)Arguments) + ") -> (" + ReturnType + ")";
      }
   }

   class Parameter
   {
      public String Name;
      public CapnpType Type;
      public Value DefaultValue;
      public Annotation Annotation;

      public override string ToString()
      {
         var d = DefaultValue == null ? "" : " = " + DefaultValue;
         var a = Annotation == null ? "" : " " + Annotation;
         return Name + " :" + Type + d + a;
      }
   }

   class Annotation
   {
      public String Name;

      public Value Argument;

      public override string ToString()
      {
         var args = Argument == null ? "" : "(" + Argument + ")";
         return "$" + Name + args;
      }
   }

   class _ParsedCapnpSource
   {
      public Int64 Id;

      public IEnumerable<CapnpStruct> Structs;
      public IEnumerable<CapnpInterface> Interfaces;
      public IEnumerable<CapnpConst> Constants;
      public IEnumerable<CapnpEnum> Enumerations;
      public IEnumerable<CapnpAnnotation> AnnotationDefs;

      // todo: does order matter? i.e. can we use a name before the using?
      public CapnpUsing[] Usings;

      // todo: clean up enum/array stuff
      public Annotation[] Annotations;

      // todo: clean up ToStrings, lineendings (dont assume crlf)
      public override string ToString()
      {
         return "Compiled Source: \r\n" +
                "Id = " + Id + "\r\n" +
                String.Join("\r\n", (IEnumerable<CapnpStruct>)Structs) + "\r\n" +
                String.Join("\r\n", (IEnumerable<CapnpInterface>)Interfaces) + "\r\n" +
                String.Join("\r\n", (IEnumerable<CapnpConst>)Constants) + "\r\n" +
                String.Join("\r\n", (IEnumerable<CapnpEnum>)Enumerations) + "\r\n" +
                String.Join("\r\n", (IEnumerable<CapnpAnnotation>)AnnotationDefs) + "\r\n" +
                String.Join("\r\n", (IEnumerable<Annotation>)Annotations);
      }
   }

   enum TypeKind
   {
      Primitive,
      List,
      Struct
   }

   class Value
   {
      public Value(CapnpType type)
      {
         Type = type;
      }

      public readonly CapnpType Type;
   }

   class UnresolvedValue : Value
   {
      public UnresolvedValue(CapnpReference referenceType) : base(referenceType) { }

      public Int32 Position; // todo
      public String RawData;

      public override string ToString()
      {
         return "Unresolved: \"" + RawData + "\"";
      }
   }

   class VoidValue : Value
   {
      public VoidValue() : base(CapnpType.Void) { }

      public override string ToString()
      {
         return "«void»";
      }
   }

   class TextValue : Value
   {
      public TextValue() : base(CapnpType.Text) { }
      public String Value;

      public override string ToString()
      {
         return "\"" + Value + "\"";
      }
   }

   class BoolValue : Value
   {
      public BoolValue() : base(CapnpType.Bool) { }
      public Boolean Value;

      public override string ToString()
      {
         return Value ? "true" : "false";
      }
   }

   class Int32Value : Value
   {
      public Int32Value() : base(CapnpType.Int32) { }
      public Int32 Value;

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

   enum PrimitiveName
   {
      // todo
      AnyPointer, Void, Bool, Int8, Int16, Int32, Text, Data
   }

   class CapnpType
   {
      protected readonly CapnpType _parameter;
      protected readonly TypeKind _kind;

      protected CapnpType() { _kind = TypeKind.Primitive; }
      protected CapnpType(TypeKind kind) { _kind = kind; }
      protected CapnpType(TypeKind kind, CapnpType parameter)
         : this(kind)
      {
         Debug.Assert(kind == TypeKind.List);
         _parameter = parameter;
      }

      // probably move
      public static readonly CapnpPrimitive AnyPointer = new CapnpPrimitive(PrimitiveName.AnyPointer);
      public static readonly CapnpPrimitive Void = new CapnpPrimitive(PrimitiveName.Void);
      public static readonly CapnpPrimitive Bool = new CapnpPrimitive(PrimitiveName.Bool);
      public static readonly CapnpPrimitive Int8 = new CapnpPrimitive(PrimitiveName.Int8);
      public static readonly CapnpPrimitive Int16 = new CapnpPrimitive(PrimitiveName.Int16);
      public static readonly CapnpPrimitive Int32 = new CapnpPrimitive(PrimitiveName.Int32);
      public static readonly CapnpPrimitive Text = new CapnpPrimitive(PrimitiveName.Text);
      public static readonly CapnpPrimitive Data = new CapnpPrimitive(PrimitiveName.Data);

      public static readonly CapnpType Unit = new CapnpType();
   }

   class CapnpPrimitive : CapnpType
   {
      public readonly PrimitiveName Kind;

      public CapnpPrimitive(PrimitiveName kind) { Kind = kind; }

      public override string ToString()
      {
         return Kind.ToString();
      }
   }

   class CapnpList : CapnpType
   {
      public CapnpType Parameter;

      public override string ToString()
      {
         return "List(" + Parameter.ToString() + ")";
      }
   }

   enum AnnotationTypes
   {
      File, Struct, Interface,
      Any
   }

   // todo: we cannot currently handle struct typed annotations as the syntax is $foo(...) rather than $foo( (.... ) ) (ugly, of course).
   class CapnpAnnotation : CapnpType
   {
      public String Name;

      public CapnpType ArgumentType;

      // todo: move to base?
      public Annotation Annotation;

      public Int64? Id;

      public AnnotationTypes[] Targets;

      public override string ToString()
      {
         var arg = ArgumentType == null ? "" : "(" + ArgumentType + ")";
         return "Annotation: " + Name + " " + arg + " " + Annotation + " targets " + String.Join(", ", (IEnumerable<AnnotationTypes>)Targets);
      }
   }

   class CapnpUsing : CapnpType
   {
      public String Name; // can be null
      public CapnpType Target;

      public override String ToString()
      {
         return "Using: " + Name + " = " + Target;
      }
   }

   class CapnpImport : CapnpType
   {
      public String File;

      public CapnpType Type;

      public override string ToString()
      {
         return Type == null ? "Import: " + File :
                               "Import: " + File + "." + Type;
      }
   }

   class CapnpComposite : CapnpType
   {
      public CapnpType[] NestedTypes;

      public Annotation Annotation;
      public Int64? Id;

      public CapnpUsing[] Usings;

      public override string ToString()
      {
         return String.Join("\r\n", (IEnumerable<CapnpType>)NestedTypes);
      }
   }

   class CapnpStruct : CapnpComposite
   {
      public String Name;

      public Field[] Fields;

      public override string ToString()
      {
         var annot = Annotation == null ? "" : Annotation.ToString();
         return "Struct " + Name + " " + annot + "\r\n   " + String.Join("\r\n   ", (IEnumerable<Field>)Fields) + "\r\n\r\n" + base.ToString();
      }
   }

   class CapnpConst : CapnpType
   {
      public String Name;
      public Value Value;

      public Annotation Annotation;

      public override string ToString()
      {
         var a = Annotation == null ? "" : Annotation + " ";
         return "Const " + Name + " " + a + "=" + Value;
      }
   }

   class CapnpReference : CapnpType
   {
      public String FullName;

      public override string ToString()
      {
         return "Reference to " + FullName;
      }
   }

   class CapnpEnum : CapnpType
   {
      public String Name;

      public Annotation Annotation;
      public Int64? Id;

      public class Enumerant
      {
         public String Name;
         public Int32 Number;

         public Annotation Annotation;

         public override string ToString()
         {
            var a = Annotation == null ? "" : " " + Annotation;
            return Name + " @" + Number + a;
         }
      }

      public Enumerant[] Enumerations;

      public override string ToString()
      {
         return "Enum " + Name + " " + Annotation + "\r\n   " + String.Join("\r\n   ", (IEnumerable<Enumerant>)Enumerations);
      }
   }

   class CapnpInterface : CapnpComposite
   {
      public String Name;

      // todo: validaiotn that these are interfaces etc
      public CapnpType[] BaseInterfaces = new CapnpType[0]; // empty<>.ar todo

      public Method[] Methods;

      public override string ToString()
      {
         var extends = BaseInterfaces.Length == 0 ? "" : " extends " + String.Join(", ", (IEnumerable<CapnpType>)BaseInterfaces);
         return "Interface " + Name + extends + " " + Annotation + "\r\n   " + String.Join("\r\n   ", (IEnumerable<Method>)Methods) + "\r\n\r\n" + base.ToString();
      }
   }

   class CapnpUnion : CapnpType
   {
      public Field[] Fields;

      public override string ToString()
      {
         return "Union:\r\n" + String.Join("\r\n", (IEnumerable<Field>)Fields);
      }
   }

   class CapnpGroup : CapnpType
   {
      public Field[] Fields;

      public override string ToString()
      {
         return "Group:\r\n" + String.Join("\r\n", (IEnumerable<Field>)Fields);
      }
   }

   class CapnpParser
   {
      private readonly String _source;
      private Int32 pos;

      public CapnpParser(String capnpSource)
      {
         _source = capnpSource;
         pos = 0;
      }

      private Int64 _ParseId()
      {
         _Advance("@", skipWhiteSpace: false);
         return _ParseInt64();
      }
      private Int64? _OptParseId()
      {
         if (_Peek("@")) return _ParseId();
         return null;
      }

      public _ParsedCapnpSource Parse()
      {
         pos = 0;
         _AdvanceWhiteSpace();

         Int64? id = null;

         var curPos = pos;

         var structs = new List<CapnpStruct>();
         var enums = new List<CapnpEnum>();
         var interfaces = new List<CapnpInterface>();
         var consts = new List<CapnpConst>();
         var annotDefs = new List<CapnpAnnotation>();

         var annotations = new List<Annotation>();
         var usings = new List<CapnpUsing>();

         String token;
         for (; ; )
         {
            if (_OptAdvanceExpr(@"\w+", out token))
               switch (token)
               {
                  case "struct": structs.Add(_ParseStruct()); break;
                  case "interface": interfaces.Add(_ParseInterface()); break;
                  case "enum": enums.Add(_ParseEnum()); break;
                  case "const": consts.Add(_ParseConst()); break;
                  case "annotation": annotDefs.Add(_ParseAnnotationDeclaration()); break;
                  case "using": usings.Add(_ParseUsing()); break;

                  default: throw new Exception("todo: " + token);
               }
            else if (_Peek("$"))
            {
               annotations.Add(_OptParseAnnotation());
               _Advance(";");

            }
            else if (_Peek("@"))
            {
               if (id != null) _Error("Multiple file ids found");
               id = _ParseId();
               _Advance(";");
            }
            else
               break;
         }

         if (pos < _source.Length - 1)
            _Error("Expected end of input.");

         return new _ParsedCapnpSource
         {
            Id = id.Value, // todo: error
            Constants = consts,
            Structs = structs,
            Interfaces = interfaces,
            Enumerations = enums,
            AnnotationDefs = annotDefs,
            Annotations = annotations.ToArray(),
            Usings = usings.ToArray()
         };
      }

      private CapnpConst _ParseConst()
      {
         var name = _ParseName();
         _Advance(":");
         var type = _ParseType();
         _Advance("=");
         var value = _ParseDefaultValue(type);

         var annotation = _OptParseAnnotation();

         _Advance(";");

         return new CapnpConst
         {
            Name = name,
            Value = value,
            Annotation = annotation
         };
      }

      private CapnpAnnotation _ParseAnnotationDeclaration()
      {
         var name = _ParseName();

         var id = _OptParseId();

         _Advance("(");

         var targets = new List<AnnotationTypes>();
         if (_OptAdvance("*"))
            targets.Add(AnnotationTypes.Any);
         else
         {
            String target;
            while (_OptAdvanceOneOf(out target, "file", "struct", "field", "union", "enumerant", "enum", "method", "parameter", "annotation", "const", "interface"))
            {
               targets.Add((AnnotationTypes)Enum.Parse(typeof(AnnotationTypes), target)); // todo error
               if (!_OptAdvance(",")) break;
            }
         }

         _Advance(")");

         CapnpType argType = null;
         if (_OptAdvance(":"))
         {
            argType = _ParseType();
         }

         var annotation = _OptParseAnnotation();

         _Advance(";");

         return new CapnpAnnotation
         {
            Name = name,
            Id = id,
            Targets = targets.ToArray(),
            ArgumentType = argType,
            Annotation = annotation
         };
      }

      private Annotation _OptParseAnnotation()
      {
         if (!_OptAdvance("$")) return null;

         var name = _ParseName();

         Value argument = null;
         if (_OptAdvance("(") && !_OptAdvance(")"))
         {
            argument = _ParseDefaultValue(new CapnpReference { FullName = name });
            _Advance(")");
         }

         return new Annotation
         {
            Name = name,
            Argument = argument
         };
      }

      private CapnpImport _ParseImport()
      {
         var file = _ParseText();

         CapnpType type = null;
         if (_OptAdvance("."))
         {
            type = _ParseType();
            if (!(type is CapnpReference))
               _Error("Expected reference to be imported.");
         }

         return new CapnpImport
         {
            File = file,
            Type = type
         };
      }

      private CapnpType _ParseImportOrType()
      {
         if (_OptAdvance("import", requireWhiteSpace: true)) return _ParseImport();
         return _ParseType();
      }

      // using Foo.Bar;
      // using Foo = Bar;
      // using import "blah";
      // using Foo = import "blah"
      private CapnpUsing _ParseUsing()
      {
         if (_OptAdvance("import", requireWhiteSpace: true))
         {
            var result = new CapnpUsing
            {
               Target = _ParseImport()
            };
            _Advance(";");
            return result;
         }

         var name = _ParseName();

         if (!_OptAdvance("="))
         {
            var result = new CapnpUsing
            {
               Target = new CapnpReference
               {
                  FullName = name
               }
            };
            _Advance(";");
            return result;
         }

         var res = new CapnpUsing
         {
            Name = name,
            Target = _ParseImportOrType()
         };
         _Advance(";");
         return res;
      }

      private CapnpStruct _ParseStruct()
      {
         var name = _ParseName();

         var id = _OptParseId();

         var annotation = _OptParseAnnotation();

         _Advance("{");

         var block = _ParseBlock(isInterface: false).ToArray(); // todo toarray just to force unwinding for debugging

         _Advance("}");

         return new CapnpStruct
         {
            Name = name,
            Id = id,
            Fields = block.Where(o => o is Field).Cast<Field>().ToArray(),
            Annotation = annotation,
            NestedTypes = block.Where(o => o is CapnpType).Cast<CapnpType>().ToArray()
         };
      }

      private CapnpInterface _ParseInterface()
      {
         var name = _ParseName();

         var id = _OptParseId();

         List<CapnpType> extendedIfaces = null;
         if (_OptAdvance("extends"))
         {
            _Advance("(");

            extendedIfaces = new List<CapnpType>();
            do
            {
               extendedIfaces.Add(_ParseType()); // todo: abuse of ispar
            } while (_OptAdvance(","));

            _Advance(")");
         }

         // todo: this is correct placement?
         var annotation = _OptParseAnnotation();

         _Advance("{");

         var block = _ParseBlock(isInterface: true).ToArray(); // todo toarr unwrap

         _Advance("}");

         return new CapnpInterface
         {
            Name = name,
            Id = id,
            Annotation = annotation,
            Methods = block.Where(o => o is Method).Cast<Method>().ToArray(),
            NestedTypes = block.Where(o => o is CapnpType).Cast<CapnpType>().ToArray(),
            BaseInterfaces = extendedIfaces == null ? new CapnpType[0] : extendedIfaces.ToArray()
         };
      }

      private String _ParseName()
      {
         return _AdvanceExpr(@"(\w|\.)+"); // todo
      }

      private IEnumerable<Object> _ParseBlock(Boolean isInterface)
      {
         for (var prev = pos; ; prev = pos)
         {
            if (_Peek("}"))
               yield break;

            var name = _ParseName();

            switch (name)
            {
               case "using":
                  yield return _ParseUsing(); break;

               case "struct":
                  yield return _ParseStruct(); break;

               case "interface":
                  yield return _ParseInterface(); break;

               case "enum":
                  yield return _ParseEnum(); break;

               case "const":
                  yield return _ParseConst(); break;

               case "annotation":
                  throw new Exception();

               case "union":
                  if (isInterface) _Error("Interfaces cannot contain anonymous unions");

                  yield return new Field
                  {
                     Name = null, // unnamed union
                     Type = _ParseGroupOrUnion("union")
                  }; break;

               default:
                  pos = prev;
                  if (isInterface)
                     yield return _ParseMethod();
                  else
                     yield return _ParseField();
                  break;
            }
         }
      }

      private Field _ParseField()
      {
         var name = _ParseName();

         var number = -1;

         CapnpType type;
         Value defaultValue = null;
         Annotation annotation = null;
         if (_OptAdvance("@", skipWhiteSpace: false))
         {
            number = _ParseInt32();

            _Advance(":");
            type = _ParseType();

            if (_OptAdvance("="))
            {
               defaultValue = _ParseDefaultValue(type);
            }

            annotation = _OptParseAnnotation();

            _Advance(";");
         }
         else
         {
            _Advance(":");
            type = _ParseGroupOrUnion(_AdvanceOneOf("union", "group"));
         }

         return new Field
         {
            Name = name,
            Number = number,// < nullable?
            Type = type,
            Annotation = annotation,
            Value = defaultValue
         };
      }

      private Method _ParseMethod()
      {
         var name = _ParseName();

         _Advance("@", skipWhiteSpace: false);
         var number = _ParseInt32();

         _Advance("(");

         var arguments = new List<Parameter>();

         while (!_Peek(")"))
         {
            arguments.Add(_ParseParameter());
            if (!_OptAdvance(",")) break;
         }

         _Advance(")");

         Parameter returnType = null;
         if (_OptAdvance("->"))
         {
            // have a return type
            _Advance("(");

            returnType = _ParseParameter();

            _Advance(")");
         }

         var annotation = _OptParseAnnotation();

         _Advance(";");

         return new Method
         {
            Name = name,
            Number = number,
            Arguments = arguments.ToArray(),
            Annotation = annotation,
            ReturnType = returnType
         };
      }

      private Parameter _ParseParameter()
      {
         var name = _ParseName();

         _Advance(":");
         var type = _ParseType();

         Value defaultValue = null;
         if (_OptAdvance("="))
            defaultValue = _ParseDefaultValue(type);

         return new Parameter
         {
            Name = name,
            Type = type,
            DefaultValue = defaultValue,
            Annotation = _OptParseAnnotation()
         };
      }

      private CapnpType _ParseType()
      {
         var text = _AdvanceExpr(@"\w(\w|\.)*\w");

         switch (text)
         {
            // Builtin types.
            case "Int32": return CapnpType.Int32;
            case "Text": return CapnpType.Text;
            case "Void": return CapnpType.Void;
            case "Bool": return CapnpType.Bool;

            case "union": return _ParseGroupOrUnion("union");
            case "group": return _ParseGroupOrUnion("group");

            case "import": return _ParseImport();

            case "List":
               {
                  _Advance("(");
                  var result = new CapnpList { Parameter = _ParseType() };
                  _Advance(")");
                  return result;
               }

            default:
               // Type may not yet be defined, so first return a reference which we'll resolve after parsing.
               return new CapnpReference
               {
                  FullName = text
               };
         }

         throw new Exception();
      }

      private CapnpType _ParseGroupOrUnion(String kind)
      {
         _Advance("{");

         var flds = new List<Field>();
         while (!_OptAdvance("}"))
            flds.Add(_ParseField());

         if (kind == "union")
         {
            return new CapnpUnion { Fields = flds.ToArray() };
         }
         else
         {
            return new CapnpGroup { Fields = flds.ToArray() };
         }
      }

      // todo: use
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

      private String _ParseText()
      {
         _Advance("\"");
         // todo string escaping
         var str = _AdvanceUntil('"');
         _Advance("\"");
         return str;
      }

      private String _ParseRawValue()
      {
         var start = pos;
         if (_Peek("[")) _AdvanceCommaSep("[", "]", _ParseRawValue).LastOrDefault();
         else if (_Peek("(")) _AdvanceCommaSep<String>("(", ")", () => { _ParseName(); _Advance("="); _ParseRawValue(); return null; }).LastOrDefault();
         else if (_Peek("\"")) _ParseText();
         else
         {
            _AdvanceExpr(@"(\w|\d|\.|e|E|x|X|[a-f]|[A-F])+"); // skip numbers of any shape, todo
         }

         return _source.Substring(start, pos - start);
      }

      private Value _ParseDefaultValue(CapnpType type)
      {
         if (type is CapnpPrimitive)
         {
            if (type == CapnpType.Bool)
            {
               // var result = _Choice(s => Boolean.Parse(s), "true", "false") ?

               if (_OptAdvance("true")) return new BoolValue { Value = true };
               if (_OptAdvance("false")) return new BoolValue { Value = false };
               throw new Exception();
            }

            if (type == CapnpType.Int32)
            {
               return new Int32Value { Value = _ParseInt32() };
            }

            if (type == CapnpType.Void)
            {
               _Advance("void");
               return new VoidValue(); // singleton?
            }

            if (type == CapnpType.Text)
            {
               return new TextValue { Value = _ParseText() };
            }

            throw new Exception("todo");
         }
         else if (type is CapnpList)
         {
            _Advance("[");

            var listType = (CapnpList)type;
            var values = new List<Value>();
            while (_source[pos] != ']')
            {
               values.Add(_ParseDefaultValue(listType.Parameter));
               if (!_OptAdvance(",")) break;
            }

            _Advance("]");

            return new ListValue(listType) { Values = values };
         }
         else if (type is CapnpStruct)
         {
            _Advance("(");

            var defs = new Dictionary<String, Value>();
            while (_source[pos] != ')')
            {
               var name = _ParseName();
               _Advance("=");

               CapnpType fldType;
               CapnpStruct @struct = (CapnpStruct)type;

               fldType = @struct.Fields.Single(f => f.Name == name).Type; // todo error blah

               defs.Add(name, _ParseDefaultValue(fldType));
            }

            _Advance(")");
         }
         else if (type is CapnpReference)
         {
            var result = new UnresolvedValue((CapnpReference)type)
            {
               Position = pos,
               RawData = _ParseRawValue() // endIdx < 0 ? null : _source.Substring(pos, endIdx.Value - pos - 1)
            };

            return result;
         }

         throw new Exception("todo");
      }

      private Int32 _ParseInt32()
      {
         var isHex = false;
         var text = "";

         // is a number "0123" valid?
         if (_OptAdvance("0x", skipWhiteSpace: false) || _OptAdvance("0X", skipWhiteSpace: false))
         {
            isHex = true;
            text = _AdvanceExpr("([a-f]|[A-F]|[0-9])+");
         }
         else
            text = _AdvanceExpr("[0-9]+");

         Int32 asInt = 0;
         if (isHex && !Int32.TryParse(text, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out asInt))
            _Error("valid hex number, got '{0}'", text);
         if (!isHex && !Int32.TryParse(text, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out asInt))
            _Error("valid integer, got '{0}'", text);

         return asInt;
      }

      private Int64 _ParseInt64()
      {
         var isHex = false;
         var text = "";

         // is a number "0123" valid?
         if (_OptAdvance("0x", skipWhiteSpace: false) || _OptAdvance("0X", skipWhiteSpace: false))
         {
            isHex = true;
            text = _AdvanceExpr("([a-f]|[A-F]|[0-9])+");
         }
         else
            text = _AdvanceExpr("[0-9]+");

         Int64 asInt = 0;
         if (isHex && !Int64.TryParse(text, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out asInt))
            _Error("valid hex number, got '{0}'", text);
         if (!isHex && !Int64.TryParse(text, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out asInt))
            _Error("valid integer, got '{0}'", text);

         return asInt;
      }

      private Single _ParseFloat32()
      {
         throw new Exception();
      }

      private CapnpEnum _ParseEnum()
      {
         var name = _ParseName();

         var id = _OptParseId();

         var annotation = _OptParseAnnotation();

         _Advance("{");

         var fields = new List<CapnpEnum.Enumerant>();
         while (!_Peek("}"))
         {
            var fldName = _ParseName();
            _Advance("@", skipWhiteSpace: false);
            var number = _ParseInt32();
            var enumerantAnnot = _OptParseAnnotation();
            _Advance(";");

            fields.Add(new CapnpEnum.Enumerant
            {
               Name = fldName,
               Number = number,
               Annotation = enumerantAnnot,
            });
         }

         _Advance("}");

         return new CapnpEnum
         {
            Name = name,
            Id = id,
            Annotation = annotation,
            Enumerations = fields.ToArray()
         };
      }



      #region Utils

      private Exception _Error(String error, params Object[] args)
      {
         throw new Exception(String.Format(error, args) + " at " + pos); // < todo
      }

      private static Boolean _IsWhiteSpace(Char c)
      {
         return Char.IsWhiteSpace(c);
      }

      private void _AdvanceWhiteSpace()
      {
         for (; pos < _source.Length; pos++)
            if (_source[pos] == '#')
               _AdvanceComment();
            else if (!_IsWhiteSpace(_source[pos]))
               break;
      }

      private void _AdvanceComment()
      {
         Debug.Assert(_source[pos] == '#');
         for (; pos < _source.Length; pos++)
            if (_source[pos] == '\n') break;
      }

      private void _Expect(String token)
      {
         if (pos + token.Length >= _source.Length)
            _Error("Expected '{0}'.", token);

         for (var i = 0; i < token.Length; i++)
            if (token[i] != _source[pos + i])
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

      private String _AdvanceUntil(char match)
      {
         for (var start = pos; pos < _source.Length; pos++)
            if (_source[pos] == match)
               return _source.Substring(start, 1 + pos - start);

         throw _Error("Expected '{0}', unexpected end of input.", match);
      }

      // todo: better error, pass on error message?
      private String _AdvanceExpr(String regex)
      {
         String result = null;
         if (!_OptAdvanceExpr(regex, out result))
            _Error("Expected regex match: " + regex);
         return result;
      }

      private Boolean _OptAdvanceExpr(String regex, out String token)
      {
         token = null;
         var r = new Regex("^" + regex, RegexOptions.CultureInvariant);
         var m = r.Match(_source.Substring(pos)); // todo
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

      // Advances only if there is whitespace following the given token.
      private Boolean _OptAdvanceToken(String token)
      {
         var start = pos;
         if (!_OptAdvance(token)) return false;
         if (pos == start + token.Length)
         {
            pos = start;
            return false;
         }
         return true;
      }

      private Boolean _OptAdvance(String token, Boolean skipWhiteSpace = true, Boolean requireWhiteSpace = false)
      {
         Debug.Assert(skipWhiteSpace || !requireWhiteSpace);

         if (pos + token.Length >= _source.Length) return false;

         var i = 0;
         for (i = 0; i < token.Length; i++)
            if (token[i] != _source[pos + i]) return false;

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

      #endregion
   }
}
