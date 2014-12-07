using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

// todo: to support generics
// 1. we need to track scopes, so a scope needs to know its parent scope because we need to resolve generic paremeters for a generic parent
// 2. we need to enhance FullName to become a list of names with FullName parameters
// 3. we need to update resolve ref to deal with his
// 4. given a closed generic type, we must be able to resolve a member type for annotation values or default values in other classes


// Todo:
// - Syntax:
//   * data lists can have string values
//   * @xx! numbers (what are these?)
// - test, test, test
// - compare with the c++ impl intsead of just the examples
// - validate ordinals, they cannot contain holes (e.g. number @3 after @1 but no @2)
// - enforce UTF8 + BOM (well, allow to override)
// - add position information to each "type"

namespace CapnProto.Schema.Parser
{
   partial class CapnpParser
   {
      internal const UInt64 MIN_UID = 1UL << 63;

      private readonly String _mSource;

      private CapnpComposite _mCurrentScope;
      private Int32 pos;

      public CapnpParser(String capnpSource)
      {
         _mSource = capnpSource;
         pos = 0;
      }

      private UInt64 _ParseId()
      {
         _Advance("@", skipWhiteSpace: false);
         var id = _ParseInteger<UInt64>();
         if (id < MIN_UID) _Error("invalid id, too small");
         _OptAdvance("!"); // todo what is this?
         return id;
      }
      private UInt64? _OptParseId()
      {
         if (_Peek("@")) return _ParseId();
         return null;
      }

      public CapnpModule Parse()
      {
         pos = 0;
         _AdvanceWhiteSpace();

         UInt64? id = null;

         var curPos = pos;

         var structs = new List<CapnpStruct>();
         var enums = new List<CapnpEnum>();
         var interfaces = new List<CapnpInterface>();
         var consts = new List<CapnpConst>();
         var annotDefs = new List<CapnpAnnotation>();

         var annotations = new List<Annotation>();
         var usings = new List<CapnpUsing>();

         var module = new CapnpModule();

         Debug.Assert(_mCurrentScope == null);
         _mCurrentScope = module;

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

         if (pos < _mSource.Length - 1)
            _Error("Expected end of input.");

         if (id == null)
            _Error("Missing id in module");

         module.Id = id.Value;
         module.Constants = consts.ToArray();
         module.Structs = structs.ToArray();
         module.Interfaces = interfaces.ToArray();
         module.Enumerations = enums.ToArray();
         module.AnnotationDefs = annotDefs.ToArray();
         module.Annotations = annotations.ToArray();
         module.Usings = usings.ToArray();
         return module;
      }

      // For now split, may combine into something later.
      public CapnpModule ProcessParsedSource(CapnpModule source, Func<String, String> getImportSource)
      {
         new IdGeneratingVisitor().VisitModule(source);

         new ImportResolutionVisitor(getImportSource).VisitModule(source);

         new ReferenceResolutionVisitor(source).ResolveReferences();

         new UnresolvedValueVisitor().VisitModule(source);

         new ConstRefVisitor().VisitModule(source);

         new ValidationVisitor().VisitModule(source);

         return source;
      }

      private CapnpConst _ParseConst()
      {
         var name = _ParseNonCapitalizedName();
         _Advance(":");
         var type = _ParseType();
         _Advance("=");
         var value = _ParseDefaultValue(type);

         var annotation = _OptParseAnnotation();

         _Advance(";");

         return new CapnpConst
         {
            Name = name,
            Scope = _mCurrentScope,
            Value = value,
            Annotation = annotation
         };
      }

      private CapnpAnnotation _ParseAnnotationDeclaration()
      {
         var name = _ParseNonCapitalizedName();

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
               targets.Add((AnnotationTypes)Enum.Parse(typeof(AnnotationTypes), target));
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
            Scope = _mCurrentScope,
            Id = id,
            Targets = targets.ToArray(),
            ArgumentType = argType,
            Annotations = new[] { annotation }
         };
      }

      private Annotation _OptParseAnnotation()
      {
         if (!_OptAdvance("$")) return null;

         var name = _ParseFullName();
         var decl = new CapnpReference
         {
            FullName = name
         };

         Value argument = null;
         if (_Peek("("))
         {
            argument = _ParseDefaultValue(null);
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

         // todo: capitalization?
         var name = _ParseFullName();

         if (name.HasGenericParameters)
            throw new Exception("check"); // todo, does this ever make sense?

         if (!_OptAdvance("="))
         {
            var result = new CapnpUsing
            {
               Name = name.Last.Name, // this implements using Foo.Bar which is effectively using Bar = Foo.Bar
               Target = new CapnpReference
               {
                  FullName = name,
               }
            };
            _Advance(";");
            return result;
         }

         Debug.Assert(name.IsSimple);

         var res = new CapnpUsing
         {
            Name = name.ToString(),
            Target = _ParseImportOrType()
         };
         _Advance(";");
         return res;
      }

      private IEnumerable<String> _OptParseGenericParameters()
      {
         if (_OptAdvance("("))
         {
            yield return _ParseName();

            while (_OptAdvance(","))
               yield return _ParseName();

            _Advance(")");
         }
      }

      private CapnpStruct _ParseStruct()
      {
         var name = _ParseCapitalizedName();

         // Push scope.
         var @struct = new CapnpStruct();
         var previousScope = _mCurrentScope;
         _mCurrentScope = @struct;

         // Optional type parameters for a generic struct.
         var typeParams = _OptParseGenericParameters().Select(n => new CapnpGenericParameter { Name = n }).ToArray();

         var id = _OptParseId();

         var annotation = _OptParseAnnotation();

         _Advance("{");

         var block = _ParseBlock(isInterface: false).ToArray();

         _Advance("}");

         // Pop scope.
         _mCurrentScope = previousScope;

         @struct.Name = name;
         @struct.Scope = previousScope;
         @struct.Id = id;
         @struct.TypeParameters = typeParams;
         @struct.Fields = block.OfType<Field>().ToArray();
         @struct.Annotations = annotation.SingleOrEmpty();
         @struct.Structs = block.OfType<CapnpStruct>().ToArray();
         @struct.Interfaces = block.OfType<CapnpInterface>().ToArray();
         @struct.Enumerations = block.OfType<CapnpEnum>().ToArray();
         @struct.AnnotationDefs = block.OfType<CapnpAnnotation>().ToArray();
         @struct.Constants = block.OfType<CapnpConst>().ToArray();
         @struct.Usings = block.OfType<CapnpUsing>().ToArray();
         return @struct;
      }

      private CapnpInterface _ParseInterface()
      {
         var name = _ParseCapitalizedName();

         // Push scope.
         var @interface = new CapnpInterface();
         var previousScope = _mCurrentScope;
         _mCurrentScope = @interface;

         var typeParameters = _OptParseGenericParameters().Select(n => new CapnpGenericParameter { Name = n }).ToArray();

         var id = _OptParseId();

         List<CapnpType> extendedIfaces = null;
         if (_OptAdvance("extends"))
         {
            _Advance("(");

            extendedIfaces = new List<CapnpType>();
            do
            {
               extendedIfaces.Add(_ParseType());
            }
            while (_OptAdvance(","));

            _Advance(")");
         }

         // todo: this is correct placement?
         var annotation = _OptParseAnnotation();

         _Advance("{");

         var block = _ParseBlock(isInterface: true).ToArray(); // todo toarr unwrap

         _Advance("}");

         // Pop scope.
         _mCurrentScope = previousScope;

         @interface.Name = name;
         @interface.Scope = previousScope;
         @interface.Id = id;
         @interface.TypeParameters = typeParameters;
         @interface.Annotations = annotation.SingleOrEmpty();
         @interface.Methods = block.OfType<Method>().ToArray();
         @interface.BaseInterfaces = extendedIfaces == null ? Empty<CapnpType>.Array : extendedIfaces.ToArray();
         @interface.Structs = block.OfType<CapnpStruct>().ToArray();
         @interface.Interfaces = block.OfType<CapnpInterface>().ToArray();
         @interface.Enumerations = block.OfType<CapnpEnum>().ToArray();
         @interface.AnnotationDefs = block.OfType<CapnpAnnotation>().ToArray();
         @interface.Constants = block.OfType<CapnpConst>().ToArray();
         @interface.Usings = block.OfType<CapnpUsing>().ToArray();
         return @interface;
      }

      private String _ParseCapitalizedName()
      {
         var name = _ParseName();
         if (Char.IsLower(name[0])) _Error("Name must be capitalized"); // todo this could be a non-terminating error
         return name;
      }
      private String _ParseNonCapitalizedName()
      {
         var name = _ParseName();
         if (Char.IsUpper(name[0])) _Error("Name must start with lower cased character.");
         return name;
      }
      private String _ParseName()
      {
         return _AdvanceExpr("[_a-zA-Z][_a-zA-Z0-9]*", "valid identifier");
      }
      //private String _ParseFullName()
      //{
      //   return _AdvanceExpr("[._a-zA-Z][._a-zA-Z0-9]*", "valid identifier");
      //}

      public FullName _ParseFullName()
      {
         if (_Peek(".")) // constref
         {
            var name = _AdvanceExpr("\\.[_a-zA-Z][_a-zA-Z0-9]*", "valid const ref");
            return new FullName(new[] { new NamePart(name.Substring(1), null) }, isTopLevelConst: true);
         }

         var names = new List<NamePart>();
         do
         {
            var name = _AdvanceExpr("[_a-zA-Z][_a-zA-Z0-9]*", "valid name");

            List<FullName> typeParams = null;
            if (name.IsCapitalized() && _OptAdvance("(")) // we have type parameters
            {
               typeParams = new List<FullName>();

               do
               {
                  typeParams.Add(_ParseFullName());
               }
               while (_OptAdvance(","));

               _Advance(")");
            }

            names.Add(new NamePart(name, typeParams == null ? null : typeParams.ToArray()));

            if (!name.IsCapitalized()) break; // not a scope, so break
         }
         while (_OptAdvance("."));

         // All parts except the last must be capitalized.
         for (var i = 0; i < names.Count - 1; i++)
            if (!names[i].Name.IsCapitalized()) _Error("name must be capitalized");

         // todo: pass full raw string here, we have it ready (whitespace?)
         return new FullName(names.ToArray());




         //   if (String.IsNullOrWhiteSpace(fullName)) goto ERROR;

         //   var pidx = fullName.IndexOf('.');
         //   if (pidx < 0)
         //   {
         //      parsedName = new FullName(fullName, new[] { fullName }, 0);
         //      return true;
         //   }

         //   if (pidx == 0) // global const ref
         //   {
         //      if (fullName.Length == 1) goto ERROR;
         //      pidx = fullName.IndexOf('.', 1);
         //      if (pidx >= 0) goto ERROR;
         //      if (Char.IsUpper(fullName[1])) goto ERROR;
         //      parsedName = new FullName(fullName, new[] { fullName.Substring(1) }, 0);
         //      return true;
         //   }

         //   var count = 2;
         //   for (pidx = fullName.IndexOf('.', pidx + 1); pidx >= 0; pidx = fullName.IndexOf('.', pidx + 1), count += 1) ;

         //   var names = new String[count];
         //   pidx = fullName.IndexOf('.');
         //   names[0] = fullName.Substring(0, pidx);
         //   for (var i = 1; i < count; i++)
         //   {
         //      var next = i == count - 1 ? fullName.Length : fullName.IndexOf('.', pidx + 1);

         //      names[i] = fullName.Substring(pidx + 1, next - pidx - 1);
         //      pidx = next;
         //   }

         //   // Validate.
         //   for (var i = 0; i < names.Length - 1; i++)
         //   {
         //      if (names[i].Length == 0)
         //         goto ERROR;

         //      // This name must refer to a scope thus be capitalized.
         //      if (!names[i].IsCapitalized())
         //         goto ERROR;
         //   }

         //   // The last part can refer either to a type or something else.
         //   var lastName = names[names.Length - 1];
         //   if (lastName.Length == 0)
         //      goto ERROR;

         //   parsedName = new FullName(fullName, names, 0);
         //   return true;

         //ERROR:
         //   parsedName = new FullName();
         //   return false;
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
                  yield return _ParseAnnotationDeclaration(); break;

               case "union":
                  if (isInterface) _Error("Interfaces cannot contain anonymous unions");
                  yield return _ParseAnonymousUnion(); break;

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
         var name = _ParseNonCapitalizedName();

         var number = -1;

         CapnpType type;
         Value defaultValue = null;
         Annotation annotation = null;

         // todo: simply _ParseId()?
         if (_OptAdvance("@", skipWhiteSpace: false))
         {
            // todo: check correct type
            number = _ParseInteger<Int32>();

            _OptAdvance("!"); // todo what is this?
         }

         _Advance(":");
         type = _ParseType();

         var isUnionOrGroup = type is CapnpUnion || type is CapnpGroup;

         if (!isUnionOrGroup && _OptAdvance("="))
         {
            defaultValue = _ParseDefaultValue(type);
         }

         annotation = _OptParseAnnotation();

         if (!isUnionOrGroup)
            _Advance(";");

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
         var name = _ParseNonCapitalizedName();

         _Advance("@", skipWhiteSpace: false);
         var number = _ParseInteger<Int32>();

         if (_OptAdvance("["))
            _Error("Generic methods not yet supported."); // todo

         // Somewhat odd syntax imo, anyway, support either (..) or a struct as parameter definition.
         ParamOrStruct<Parameter[], CapnpType> arguments;
         if (_Peek("("))
            arguments = new ParamOrStruct<Parameter[], CapnpType>(_AdvanceCommaSep<Parameter>("(", ")", _ParseParameter).ToArray());
         else
            arguments = new ParamOrStruct<Parameter[], CapnpType>(_ParseType());

         ParamOrStruct<Parameter[], CapnpType> returnType;
         if (_OptAdvance("->"))
         {
            if (_Peek("("))
               returnType = new ParamOrStruct<Parameter[], CapnpType>(_AdvanceCommaSep<Parameter>("(", ")", _ParseParameter).ToArray());
            else
               returnType = new ParamOrStruct<Parameter[], CapnpType>(_ParseType());
         }
         else
            // No return type is effectively void.
            returnType = new ParamOrStruct<Parameter[], CapnpType>(CapnpPrimitive.Void);

         var annotation = _OptParseAnnotation();

         _Advance(";");

         return new Method
         {
            Name = name,
            Number = number,
            Arguments = arguments,
            Annotation = annotation,
            ReturnType = returnType
         };
      }

      private Parameter _ParseParameter()
      {
         // Note: no case rule applies to parameters.
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
         return _ParseTypeStr(_ParseFullName());
      }

      private CapnpType _ParseTypeStr(FullName fullName)
      {
         if (fullName.IsSimple)
         {
            if (_mCurrentScope is CapnpModule)
            {
               // Resolve this early. Could decide to defer this through a reference (?).
               CapnpPrimitive primitive;
               if (CapnpPrimitive.TryParse(fullName[0].Name, out primitive))
                  return primitive;
            }

            switch (fullName[0].Name)
            {
               // Note: it is possible to nest such a type within a struct
               // thus Int8 depends entirely on context!
               //// Builtin types.
               //case "Int8": return CapnpPrimitive.Int8;
               //case "Int16": return CapnpPrimitive.Int16;
               //case "Int32": return CapnpPrimitive.Int32;
               //case "Int64": return CapnpPrimitive.Int64;
               //case "UInt8": return CapnpPrimitive.UInt8;
               //case "UInt16": return CapnpPrimitive.UInt16;
               //case "UInt32": return CapnpPrimitive.UInt32;
               //case "UInt64": return CapnpPrimitive.UInt64;
               //case "Float32": return CapnpPrimitive.Float32;
               //case "Float64": return CapnpPrimitive.Float64;
               //case "Text": return CapnpPrimitive.Text;
               //case "Data": return CapnpPrimitive.Data;
               //case "Void": return CapnpPrimitive.Void;
               //case "Bool": return CapnpPrimitive.Bool;
               //case "AnyPointer": return CapnpPrimitive.AnyPointer;

               case "union": return _ParseGroupOrUnion(true);
               case "group": return _ParseGroupOrUnion(false);
               case "import": return _ParseImport();

               default: break;
            }
         }

         // Todo: it's possible to override the List(T) type with a struct, should we not special case this?
         if (fullName.Count == 1 && fullName[0].Name == "List" && fullName[0].TypeParameters.Length == 1)
            return new CapnpList { Parameter = _ParseTypeStr(fullName[0].TypeParameters[0]) };

         return new CapnpReference
         {
            FullName = fullName
         };
      }

      //private FullName _GetFullName(String fullName)
      //{
      //    tood: the error here could be more descriptive
      //   FullName result;
      //   if (!FullName.TryParse(fullName, out result))
      //      _Error("The given full name '{0}' is not valid.", fullName);
      //   return result;
      //}

      private Field _ParseAnonymousUnion()
      {
         return new Field
        {
           Name = null, // unnamed union
           Type = _ParseGroupOrUnion(isUnion: true)
        };
      }

      private CapnpType _ParseGroupOrUnion(Boolean isUnion)
      {
         var annotations = _OptParseAnnotation().SingleOrEmpty();

         _Advance("{");

         var flds = new List<Field>();
         while (!_OptAdvance("}"))
         {
            if (!isUnion)
            {
               // Look for anonymous union.
               var p = pos;
               var token = _ParseName();
               if (token == "union")
               {
                  flds.Add(_ParseAnonymousUnion());
                  continue;
               }
               pos = p; // todo, bit manual this
            }

            flds.Add(_ParseField());
         }

         if (isUnion)
            return new CapnpUnion { Fields = flds.ToArray(), Annotations = annotations };
         else
            return new CapnpGroup { Fields = flds.ToArray() }; // can these have annotations? todo
      }

      private Char _Unescape(Char c)
      {
         switch (c)
         {
            case 'a': return '\a';
            case 'b': return '\b';
            case 'f': return '\f';
            case 'n': return '\n';
            case 'r': return '\r';
            case 't': return '\t';
            case 'v': return '\v';
            case 'x':
               {
                  var hex = _AdvanceExpr(_kHexRange.Times(2), "hex escape sequence");
                  return ((Char)Int32.Parse(hex, NumberStyles.HexNumber));
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
                  return (Char)first;
               }

               return c;
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
                  _AdvanceWhiteSpace();
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

      public static Value ParseValue(String rawValue, CapnpType type)
      {
         var parser = new CapnpParser(rawValue);
         return parser._ParseDefaultValue(type);
      }

      private String _ParseRawValue()
      {
         var start = pos;
         if (_Peek("[")) _AdvanceCommaSep("[", "]", () => _ParseDefaultValue(null)).LastOrDefault();
         else if (_Peek("\"")) _ParseText();
         else if (_PeekExpr("\\d|-"))
         {
            if (_Peek("0x\"")) _ParseBlob();
            else
            {
               var negate = _OptAdvance("-");
               if (_OptAdvance("inf")) return negate ? "-inf" : "inf";
               if (_OptAdvance("nan"))
               {
                  if (negate) _Error("cannot negate nan"); // cant we? todo
                  return "nan";
               }
               _AdvanceExpr(@"(\d|-)(\.|[exa-fA-F]|\d|-)*", "valid number");
            }
         }
         else
         {
            var fullName = _ParseFullName();

            if (fullName.CouldBeConstRef)
               // If it turns out it isnt a const ref it'll fail later on.
               return fullName.ToString();

            if (!fullName.IsSimple) _Error("Invalid fully qualified name at this location.");

            var name = fullName[0].Name;

            if (name == "true" || name == "false" || name == "inf")
               return name;

            // If a name it could be an enumerant.
            if (!_OptAdvance("="))
               return name;

            _ParseDefaultValue(null);

            // Skip struct definition.
            while (_OptAdvance(","))
            {
               _ParseName();
               _Advance("=");
               _ParseDefaultValue(null);
            }
         }

         return _mSource.Substring(start, pos - start);
      }

      private Value _ParseDefaultValue(CapnpType type)
      {
         // Strip opening parentheses.
         var parenthesesCount = 0;
         while (_OptAdvance("(")) parenthesesCount += 1;

         var result = _ParseDefaultValueDirect(type);

         for (; parenthesesCount > 0; parenthesesCount--)
            _Advance(")");

         return result;
      }

      private Value _ParseDefaultValueDirect(CapnpType type)
      {
         if (type is CapnpPrimitive)
         {
            if (type == CapnpPrimitive.Bool)
            {
               var fullName = _ParseFullName();

               if (fullName.IsSimple)
               {
                  var token = fullName.ToString();
                  if (token == "true") return new BoolValue { Value = true };
                  else if (token == "false") return new BoolValue { Value = false };
               }

               return new ConstRefValue(type)
               {
                  FullConstName = fullName
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
                     return new UInt8Value { Value = _ParseInteger<Byte>() }; // todo
                  else if (type == CapnpPrimitive.UInt16)
                     return new UInt16Value { Value = _ParseInteger<UInt16>() }; // todo: ensure this fits
                  else if (type == CapnpPrimitive.UInt32)
                     return new UInt32Value { Value = _ParseInteger<UInt32>() };
                  else if (type == CapnpPrimitive.UInt64)
                     return new UInt64Value { Value = _ParseInteger<UInt64>() };
                  else if (type == CapnpPrimitive.Float32)
                     return new Float32Value { Value = _ParseFloat32() };
                  else if (type == CapnpPrimitive.Float64)
                     return new Float64Value { Value = _ParseFloat64() };

                  throw new Exception("numeric type not yet finished: " + type);
               }
               else
               {
                  var fullName = _ParseFullName();
                  var token = fullName.ToString();

                  if (token == "inf")
                  {
                     if (type == CapnpPrimitive.Float32)
                        return new Float32Value { Value = Single.PositiveInfinity };
                     else if (type == CapnpPrimitive.Float64)
                        return new Float64Value { Value = Double.PositiveInfinity };
                     _Error("Uexpected token 'inf'");
                  }
                  if (token == "nan")
                  {
                     if (type == CapnpPrimitive.Float32)
                        return new Float32Value { Value = Single.NaN };
                     else if (type == CapnpPrimitive.Float64)
                        return new Float64Value { Value = Double.NaN };
                     _Error("unexpected token 'nan'");
                  }

                  if (!fullName.CouldBeConstRef) _Error("Invalid const reference");
                  return new ConstRefValue(type)
                  {
                     FullConstName = fullName
                  };
               }
            }

            if (type == CapnpPrimitive.Void)
            {
               var fullName = _ParseFullName();
               // todo: these tostrings aren't necessary, provide some support instead
               if (fullName.ToString() == "void") return new VoidValue();
               return new ConstRefValue(type) { FullConstName = fullName };
            }

            if (type == CapnpPrimitive.Text)
            {
               if (_Peek("\""))
                  return new TextValue { Value = _ParseText() };
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };
            }

            if (type == CapnpPrimitive.Data)
            {
               if (_Peek("0x\""))
                  return new DataValue { Blob = _ParseBlob() };
               if (_Peek("\""))
               {
                  // It's possible to assign text as a blob.
                  var text = _ParseText();
                  return new DataValue { Blob = Encoding.UTF8.GetBytes(text) };
               }
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };
            }

            if (type == CapnpPrimitive.AnyPointer)
               throw new Exception("todo: can a void* have a default value?");

            throw new InvalidOperationException();
         }
         else if (type is CapnpList)
         {
            if (!_OptAdvance("["))
               return new ConstRefValue(type) { FullConstName = _ParseFullName() };

            var listType = (CapnpList)type;
            var values = new List<Value>();
            while (_mSource[pos] != ']')
            {
               values.Add(_ParseDefaultValue(listType.Parameter));
               if (!_OptAdvance(",")) break;
            }

            _Advance("]");

            return new ListValue(listType) { Values = values };
         }
         else if (type is CapnpEnum)
         {
            var @enum = (CapnpEnum)type;
            var fullName = _ParseFullName();  // _ParseNonCapitalizedName();

            if (fullName.CouldBeConstRef)
               return new ConstRefValue(type) { FullConstName = fullName };

            if (!fullName.IsSimple) _Error("Invalid enumerant name.");

            var token = fullName.ToString(); // todo, with below
            if (token.IsCapitalized()) _Error("Invalid enumerant: must start with lower case");

            var enumerant = @enum.Enumerants.Where(e => e.Name == token).Single(); // < todo
            return new EnumValue
            {
               Name = token,
               Value = enumerant.Number
            };
         }
         else if (type is CapnpStruct || type is CapnpUnion || type is CapnpBoundGenericType)
         {
            var canHaveConstRef = true;

            var defs = new Dictionary<String, Value>();
            while (true)
            {
               var fullName = _ParseFullName();

               // A const ref *always* contains a period.
               if (fullName.CouldBeConstRef)
               {
                  if (canHaveConstRef)
                     return new ConstRefValue(type) { FullConstName = fullName };
                  else
                     _Error("invalid field name");
               }
               else if (!fullName.IsSimple || fullName[0].Name.IsCapitalized())
                  _Error("Field name in default value must start with a lower case letter.");

               var name = fullName[0].Name;
               canHaveConstRef = false;

               _Advance("=");

               CapnpType fldType;

               // Same syntax as for structs, but one can define only a single field.
               if (type is CapnpUnion)
               {
                  CapnpUnion union = (CapnpUnion)type;
                  fldType = union.Fields.Single(f => f.Name == name).Type; // todo error
                  return new UnionValue((CapnpUnion)type)
                  {
                     FieldName = name,
                     Value = _ParseDefaultValue(fldType)
                  };
               }

               CapnpBoundGenericType generic = type as CapnpBoundGenericType;
               CapnpStruct @struct = generic == null ? (CapnpStruct)type : (CapnpStruct)generic.OpenType; // .. it must be a struct

               // todo: we need to verify union names don't clash with their containing scope field names
               // todo: unions within unions?
               var structFields = @struct.Fields.Where(f => f.Name != null).Concat(
                                  @struct.Fields.Where(f => f.Name == null).SelectMany(f => ((CapnpUnion)f.Type).Fields));

               fldType = structFields.Single(f => f.Name == name).Type; // todo error blah

               if (generic != null)
               {
                  if (fldType is CapnpGenericParameter)
                  {
                     fldType = generic.ResolveGenericParameter((CapnpGenericParameter)fldType);
                     if (fldType == null) _Error("Cannot parse a default value for a generic type parameter type " + fldType);
                  }

                  // The field type comes from the open type, thus if it contains a closed generic type it may only be partially closed.
                  // Within our fully closed generic scope we can resolve all parameters.
                  if (fldType is CapnpBoundGenericType)
                  {
                     var genericFldType = (CapnpBoundGenericType)fldType;
                     if (!genericFldType.IsFullyClosed)
                     {
                        // Resolve any generic parameters using their bindings from the given closed type.
                        // Note: we do not require that the type is fully closed after this operation. It is possible that the default
                        // value does not refer to any generic parameters (e.g. see innerBound in test.capnp).
                        fldType = genericFldType.CloseWith(generic);
                     }
                  }
               }

               defs.Add(name, _ParseDefaultValue(fldType));
               if (!_OptAdvance(",")) break;
            }

            var finalStruct = type is CapnpStruct ? (CapnpStruct)type : (CapnpStruct)((CapnpBoundGenericType)type).OpenType;
            return new StructValue(finalStruct) // todo
            {
               FieldValues = defs
            };
         }
         else if (type is CapnpReference || type == null)
         {
            var result = new UnresolvedValue(type)
            {
               Position = pos,
               RawData = _ParseRawValue()
            };

            return result;
         }

         throw new InvalidOperationException("cannot parse a default value for type " + type.GetType().FullName);
      }

      private delegate Boolean TryParse<T>(String s, NumberStyles style, NumberFormatInfo i, out T result);

      private T _ParseInteger<T>() where T : struct
      {
         T result;
         var isHex = false;

         var text = _OptAdvance("-") ? "-" : "";
         if (_OptAdvance("0x", skipWhiteSpace: false)) // note: 0X explicitely not supported
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
         else if (_OptAdvance("nan"))
            result = Single.NaN;
         else
         {
            const String errorMsg = "valid float32 literal";
            var token = _AdvanceExpr(@"(\d|\.|e|-)+", errorMsg);
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
         else if (_OptAdvance("nan"))
            result = Double.NaN;
         else
         {
            const String errorMsg = "valid float64 literal";
            var token = _AdvanceExpr(@"(\d|\.|e|-)+", errorMsg);
            if (!Double.TryParse(token, NumberStyles.Float, NumberFormatInfo.InvariantInfo, out result)) _Error(errorMsg);
         }

         return negate ? -result : result;
      }

      private CapnpEnum _ParseEnum()
      {
         var name = _ParseCapitalizedName();

         var id = _OptParseId();

         var annotation = _OptParseAnnotation();

         _Advance("{");

         var fields = new List<Enumerant>();
         while (!_Peek("}"))
         {
            var fldName = _ParseNonCapitalizedName();
            _Advance("@", skipWhiteSpace: false);
            var number = _ParseInteger<Int32>();
            var enumerantAnnot = _OptParseAnnotation();
            _Advance(";");

            fields.Add(new Enumerant
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
            Scope = _mCurrentScope,
            Id = id,
            Annotations = annotation.SingleOrEmpty(),
            Enumerants = fields.ToArray()
         };
      }
   }
}
