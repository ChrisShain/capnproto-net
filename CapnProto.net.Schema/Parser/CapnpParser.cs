using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Todo:
// - annotations with struct arguments
// - test, test, test
// - compare with the c++ impl intsead of just the examples
// - validate ordinals, they cannot contain holes (e.g. number @3 after @1)
// - enforce UTF8 + BOM
// - add position information to each "type"

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
         return _ParseInteger<Int64>();
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

         if (id == null)
            _Error("Missing id in module");

         return new CapnpModule
         {
            Id = id.Value,
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

         var name = _ParseFullName();
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

         var name = _ParseFullName();

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
         return _AdvanceExpr("[_a-zA-Z][_a-zA-Z0-9]*", "valid identifier");
      }
      private String _ParseFullName()
      {
         return _AdvanceExpr("[._a-zA-Z][._a-zA-Z0-9]*", "valid identifier");
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
            number = _ParseInteger<Int32>();

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
         var number = _ParseInteger<Int32>();

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
         var text = _ParseFullName();

         switch (text)
         {
            // Builtin types.
            case "Int8": return CapnpPrimitive.Int8;
            case "Int16": return CapnpPrimitive.Int16;
            case "Int32": return CapnpPrimitive.Int32;
            case "Int64": return CapnpPrimitive.Int64;
            case "UInt8": return CapnpPrimitive.UInt8;
            case "UInt16": return CapnpPrimitive.UInt16;
            case "UInt32": return CapnpPrimitive.UInt32;
            case "UInt64": return CapnpPrimitive.UInt64;
            case "Float32": return CapnpPrimitive.Float32;
            case "Float64": return CapnpPrimitive.Float64;
            case "Text": return CapnpPrimitive.Text;
            case "Data": return CapnpPrimitive.Data;
            case "Void": return CapnpPrimitive.Void;
            case "Bool": return CapnpPrimitive.Bool;
            case "AnyPointer": return CapnpPrimitive.AnyPointer;

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

      private String _Unescape(Char c)
      {
         switch (c)
         {
            case 'a': return "\a";
            case 'b': return "\b";
            case 'f': return "\f";
            case 'n': return "\n";
            case 'r': return "\r";
            case 't': return "\t";
            case 'v': return "\v";
            case 'x':
               {
                  var hex = _AdvanceExpr(_kHexRange.Times(2), "hex escape sequence");
                  return ((Char)Int32.Parse(hex, NumberStyles.HexNumber)).ToString();
               }
            default:
               if (c >= '0' && c <= '7')
               {
                  // octal escape sequence
                  var first = (Int32)c;
                  String d;
                  if (_OptAdvanceExpr(_kOctalRange, out d))
                  {
                     first = (first << 3) | Int32.Parse(d, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
                     if (_OptAdvanceExpr(_kOctalRange, out d))
                        first = (first << 3) | Int32.Parse(d, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
                  }
                  return ((Char)first).ToString();
               }

               return c.ToString();
         }
      }

      private String _ParseText()
      {
         var delimiter = _AdvanceOneOf("\""); // it appears not supported, capnp throws error "'");
         var delimiterChar = delimiter[0];

         Char c;
         StringBuilder buffer = new StringBuilder();
         for (; ; )
         {
            c = _AdvanceChar();
            switch (c)
            {
               case '\n': _Error("Text may not contain linefeeds"); break;

               case '"':
               case '\'':
                  if (c != delimiterChar) goto default;
                  return buffer.Length == 0 ? "" : buffer.ToString();

               case '\\':
                  pos += 1;
                  buffer.Append(_Unescape(_AdvanceChar()));
                  continue;

               default:
                  buffer.Append(c);
                  continue;
            }
         }
      }

      private Byte[] _ParseBlob()
      {
         _Advance("0x\"");

         String hex;
         List<Byte> blob = new List<Byte>();
         while (_OptAdvanceExpr(_kHexRange.Times(2), out hex))
         {
            Byte b;
            if (!Byte.TryParse(hex, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out b))
               _Error("Expected valid hex sequence");
            blob.Add(b);
         }

         _Advance("\"");

         return blob.ToArray();
      }

      private String _ParseRawValue()
      {
         var start = pos;
         if (_Peek("[")) _AdvanceCommaSep("[", "]", _ParseRawValue).LastOrDefault();
         else if (_Peek("(")) _AdvanceCommaSep<String>("(", ")", () => { _ParseName(); _Advance("="); _ParseRawValue(); return null; }).LastOrDefault();
         else if (_Peek("\"") || _Peek("'")) _ParseText();
         else
         {
            _AdvanceExpr(@"(\w|\d|_|\.|e|x|[a-f]|[A-F]|"")+", "invalid default value");
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
            if (type == CapnpPrimitive.Bool)
            {
               var token = _ParseFullName();

               if (token == "true") return new BoolValue { Value = true };
               else if (token == "false") return new BoolValue { Value = false };

               return new ConstRefValue(type)
               {
                  FullConstName = token
               };
            }

            if (type.IsNumeric)
            {
               // NOTE: capnp disallows numbers starting with . (e.g. .2)
               if (_PeekExpr("\\d|-"))
               {
                  if (type == CapnpPrimitive.Int8)
                     return new Int8Value { Value = _ParseInteger<SByte>() };
                  else if (type == CapnpPrimitive.Int16)
                     return new Int16Value { Value = _ParseInteger<Int16>() };
                  else if (type == CapnpPrimitive.Int32)
                     return new Int32Value { Value = _ParseInteger<Int32>() };
                  else if (type == CapnpPrimitive.Int64)
                     return new Int64Value { Value = _ParseInteger<Int64>() };
                  else if (type == CapnpPrimitive.UInt8)
                     return new UInt8Value { Value = (Byte)_ParseInteger<Byte>() }; // tood
                  else if (type == CapnpPrimitive.UInt16)
                     return new UInt16Value { Value = (UInt16)_ParseInteger<UInt16>() }; // todo: ensure this fits
                  else if (type == CapnpPrimitive.UInt32)
                     return new UInt32Value { Value = (UInt32)_ParseInteger<UInt32>() };
                  else if (type == CapnpPrimitive.UInt64)
                     return new UInt64Value { Value = (UInt64)_ParseInteger<UInt64>() };
                  else if (type == CapnpPrimitive.Float32)
                     return new Float32Value { Value = _ParseFloat32() };
                  else if (type == CapnpPrimitive.Float64)
                     return new Float64Value { Value = _ParseFloat64() };

                  throw new Exception("numeric type not yet finished: " + type);
               }
               else
               {
                  var name = _ParseFullName();

                  if (name == "inf")
                  {
                     if (type == CapnpPrimitive.Float32)
                        return new Float32Value { Value = Single.PositiveInfinity };
                     else if (type == CapnpPrimitive.Float64)
                        return new Float64Value { Value = Double.PositiveInfinity };
                     _Error("Uexpected token 'inf'");
                  }

                  return new ConstRefValue(type)
                  {
                     FullConstName = name
                  };
               }

            }

            if (type == CapnpPrimitive.Void)
            {
               var token = _ParseFullName();
               if (token == "void") return new VoidValue();
               return new ConstRefValue(type) { FullConstName = token };
            }

            if (type == CapnpPrimitive.Text)
            {
               if (_Peek("\"") || _Peek("'"))
                  return new TextValue { Value = _ParseText() };
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };
            }

            if (type == CapnpPrimitive.Data)
            {
               if (_Peek("0x\""))
                  return new DataValue { Blob = _ParseBlob() };
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };
            }

            if (type == CapnpPrimitive.AnyPointer)
               throw new Exception("todo: can a void* have a default value?");

            throw new Exception("todo");
         }
         else if (type is CapnpList)
         {
            if (!_OptAdvance("["))
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };

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
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };

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

      private delegate Boolean TryParse<T>(String s, NumberStyles style, NumberFormatInfo i, out T result);

      private T _ParseInteger<T>() where T : struct
      {
         T result;
         var isHex = false;

         var text = "";
         if (_OptAdvance("-"))
            text = "-";
         else if (_OptAdvance("0x", skipWhiteSpace: false)) // note: 0X explicitely not supported
         {
            isHex = true;
            text = _AdvanceExpr(_kHexRange.OneOrMore());
         }
         else if (_OptAdvance("0", skipWhiteSpace: false)) // octal
         {
            var octal = _AdvanceExpr(_kOctalRange.ZeroOrMore(), "octal number");
            if (octal.Length == 0)
               return default(T);
            else if (!NumberParser<T>.TryParseOctal(octal, out result))
               _Error("Invalid octal number");
            return result;
         }

         if (!isHex)
            text += _AdvanceExpr("[0-9]+");

         var formatInfo = NumberFormatInfo.InvariantInfo;
         var style = isHex ? NumberStyles.HexNumber : NumberStyles.Integer;

         if (!NumberParser<T>.TryParse(text, style, formatInfo, out result)) _Error("Valid integer");
         return result;
      }

      // todo: verify there are no problems due to binary float format used
      private Single _ParseFloat32()
      {
         // Capnp appears to disallow E.
         var negate = _OptAdvance("-");

         Single result;
         if (_OptAdvance("inf"))
            result = Single.PositiveInfinity;
         else
         {
            const String errorMsg = "valid float32 literal";
            var token = _AdvanceExpr(@"(\d|\.|e)+", errorMsg);
            if (!Single.TryParse(token, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out result)) _Error(errorMsg);
         }

         return negate ? -result : result;
      }
      private Double _ParseFloat64()
      {
         // Capnp appears to disallow E.
         var negate = _OptAdvance("-");

         Double result;
         if (_OptAdvance("inf"))
            result = Double.PositiveInfinity;
         else
         {
            const String errorMsg = "valid float64 literal";
            var token = _AdvanceExpr(@"(\d|\.|e)+", errorMsg);
            if (!Double.TryParse(token, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out result)) _Error(errorMsg);
         }

         return negate ? -result : result;
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
            var number = _ParseInteger<Int32>();
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
