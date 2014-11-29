using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
// - validate ordinals, they cannot contain holes (e.g. number @3 after @1)

namespace CapnProto.Schema.Parser
{
   partial class CapnpParser
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

      public CapnpModule Parse()
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

                  default: _Error("Unexpected token '{0}'.", token); break;
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

         return new CapnpModule
         {
            Id = id.Value, // todo: error
            Constants = consts.ToArray(),
            Structs = structs.ToArray(),
            Interfaces = interfaces.ToArray(),
            Enumerations = enums.ToArray(),
            AnnotationDefs = annotDefs.ToArray(),
            Annotations = annotations.ToArray(),
            Usings = usings.ToArray(),
         };
      }

      // For now split, may combine into something later.
      public CapnpModule ProcessParsedSource(CapnpModule source, Func<String, String> getImportSource)
      {
         new ImportResolutionVisitor(getImportSource).VisitModule(source);

         new ReferenceResolutionVisitor(source).ResolveReferences();

         new UnresolvedValueVisitor().VisitModule(source);

         new ConstRefVisitor().VisitModule(source);

         new ValidationVisitor().VisitModule(source);

         return source;
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
            targets.Add(AnnotationTypes.any);
         else
         {
            String target;
            while (_OptAdvanceOneOf(out target, "file", "struct", "field", "union", "enumerant", "enum", "method", "param", "annotation", "const", "interface", "group"))
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
         var decl = new CapnpReference { FullName = name };

         Value argument = null;
         if (_OptAdvance("(") && !_OptAdvance(")"))
         {
            // todo: can this have multiple arguments?
            argument = _ParseDefaultValue(null);
            _Advance(")");
         }

         return new Annotation
         {
            Declaration = decl,
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
            NestedTypes = block.Where(o => o is CapnpType && !(o is CapnpUsing)).Cast<CapnpType>().ToArray(),
            Usings = block.Where(o => o is CapnpUsing).Cast<CapnpUsing>().ToArray()
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
            case "Int8": return CapnpType.Int8;
            case "Int16": return CapnpType.Int16;
            case "Int32": return CapnpType.Int32;
            case "Int64": return CapnpType.Int64;
            case "UInt8": return CapnpType.UInt8;
            case "UInt16": return CapnpType.UInt16;
            case "UInt32": return CapnpType.UInt32;
            case "UInt64": return CapnpType.UInt64;
            case "Float32": return CapnpType.Float32;
            case "Float64": return CapnpType.Float64;
            case "Text": return CapnpType.Text;
            case "Data": return CapnpType.Data;
            case "Void": return CapnpType.Void;
            case "Bool": return CapnpType.Bool;
            case "AnyPointer": return CapnpType.AnyPointer;

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

      public static Value ParseValue(String rawValue, CapnpType type)
      {
         var parser = new CapnpParser(rawValue);
         return parser._ParseDefaultValue(type);
      }

      private Value _ParseDefaultValue(CapnpType type)
      {
         if (type is CapnpPrimitive)
         {
            if (type == CapnpType.Bool)
            {
               var token = _ParseName();

               if (token == "true") return new BoolValue { Value = true };
               else if (token == "false") return new BoolValue { Value = false };

               return new ConstRefValue(type)
               {
                  FullConstName = token
               };
            }

            if (type.IsNumeric)
            {
               // todo: can we have doubles .2 (i.e. no 0?)
               if (_PeekExpr("\\d|-"))
               {
                  if (type == CapnpType.Int8)
                     return new Int8Value { Value = (SByte)_ParseInt32() }; // todo: pass size to parseIntxx method
                  else if (type == CapnpType.Int16)
                     throw new Exception("todo: int16");
                  else if (type == CapnpType.Int32)
                     return new Int32Value { Value = _ParseInt32() };
                  else if (type == CapnpType.Int64)
                     return new Int64Value { Value = _ParseInt64() };
                  else if (type == CapnpType.UInt8)
                     return new UInt8Value { Value = (Byte)_ParseInt32() }; // tood
                  else if (type == CapnpType.UInt16)
                     return new UInt16Value { Value = (UInt16)_ParseInt32() }; // todo: ensure this fits
                  else if (type == CapnpType.UInt32)
                     return new UInt32Value { Value = (UInt32)_ParseInt32() };
                  else if (type == CapnpType.UInt64)
                     return new UInt64Value { Value = (UInt64)_ParseInt64() };
                  else if (type == CapnpType.Float32)
                     return new Float32Value { Value = _ParseFloat32() };
                  else if (type == CapnpType.Float64)
                     return new Float64Value { Value = _ParseFloat32() }; // < todo

                  throw new Exception("numeric type not yet finished: " + type);
               }
               else
                  return new ConstRefValue(type)
                  {
                     FullConstName = _ParseName()
                  };
            }

            if (type == CapnpType.Void)
            {
               var token = _ParseName();
               if (token == "void") return new VoidValue();
               return new ConstRefValue(type) { FullConstName = token };
            }

            if (type == CapnpType.Text)
            {
               if (_Peek("\""))
                  return new TextValue { Value = _ParseText() };
               return new ConstRefValue(type) { FullConstName = _ParseName() };
            }

            if (type == CapnpType.Data)
               throw new Exception("todo: what does a data value look like?");

            if (type == CapnpType.AnyPointer)
               throw new Exception("todo: can a void* have a default value?");

            throw new Exception("todo");
         }
         else if (type is CapnpList)
         {
            if (!_OptAdvance("["))
               return new ConstRefValue(type) { FullConstName = _ParseName() };

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
            if (!_OptAdvance("("))
               return new ConstRefValue(type) { FullConstName = _ParseName() };

            var defs = new Dictionary<String, Value>();
            while (_source[pos] != ')')
            {
               var name = _ParseName();
               _Advance("=");

               CapnpType fldType;
               CapnpStruct @struct = (CapnpStruct)type;

               fldType = @struct.Fields.Single(f => f.Name == name).Type; // todo error blah

               defs.Add(name, _ParseDefaultValue(fldType));
               if (!_OptAdvance(",")) break;
            }

            _Advance(")");

            return new StructValue((CapnpStruct)type)
            {
               FieldValues = defs
            };
         }
         else if (type is CapnpReference || type == null)
         {
            var result = new UnresolvedValue(type)
            {
               Position = pos,
               RawData = _ParseRawValue() // endIdx < 0 ? null : _source.Substring(pos, endIdx.Value - pos - 1)
            };

            return result;
         }

         throw new Exception("todo: def value for " + type.GetType().FullName);
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
         // todo: this is obv. wrong
         _AdvanceExpr(@"\d|\.|e|E");
         return -7.42f;
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
            Enumerants = fields.ToArray()
         };
      }
   }
}
