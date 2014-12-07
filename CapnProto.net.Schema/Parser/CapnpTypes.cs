using System;
using System.Diagnostics;
using System.Linq;

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

      public override String ToString()
      {
         var namedType = Type as CapnpNamedType;
         var typeStr = namedType == null ? Type.ToString() : namedType.Name;
         return String.Format("Field: {0} @{1} :{2} = {3} {4}", Name, Number, typeStr, Value, Annotation);
      }
   }

   class ParamOrStruct<T1, T2>
   {
      public ParamOrStruct(T1 left) { Params = left; Struct = default(T2); }
      public ParamOrStruct(T2 right) { Params = default(T1); Struct = right; }

      public T1 Params;
      public T2 Struct;
   }

   class Method : Member
   {
      public ParamOrStruct<Parameter[], CapnpType> Arguments;

      public ParamOrStruct<Parameter[], CapnpType> ReturnType;

      public override String ToString()
      {
         var args = Arguments.Params == null ? Arguments.Struct.ToString()
                                           : "(" + String.Join<Parameter>(", ", Arguments.Params) + ")";

         var rets = ReturnType.Params == null ? ReturnType.Struct.ToString()
                                           : "(" + String.Join<Parameter>(", ", ReturnType.Params) + ")";

         return Name + " @" + Number + args + "-> " + rets;
      }
   }

   class Parameter : Member
   {
      public CapnpType Type;
      public Value DefaultValue;

      public override String ToString()
      {
         var d = DefaultValue == null ? "" : " = " + DefaultValue;
         var a = Annotation == null ? "" : " " + Annotation;
         return Name + " :" + Type + d + a;
      }
   }

   class Enumerant : Member
   {
      public override String ToString()
      {
         var a = Annotation == null ? "" : " " + Annotation;
         return Name + " @" + Number + a;
      }
   }

   class Annotation
   {
      public CapnpType Declaration;

      public Value Argument;

      public override String ToString()
      {
         var args = Argument == null ? "" : "(" + Argument + ")";
         return "$" + Declaration + args;
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
      public virtual Boolean IsGeneric { get { return false; } }

      internal protected virtual CapnpType Accept(CapnpVisitor visitor)
      {
         throw new NotImplementedException("todo: implement Accept for " + this.GetType().Name);
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

      public static Boolean TryParse(String token, out CapnpPrimitive result)
      {
         result = null;
         switch (token)
         {
            case "Int8": result = CapnpPrimitive.Int8; return true;
            case "Int16": result = CapnpPrimitive.Int16; return true;
            case "Int32": result = CapnpPrimitive.Int32; return true;
            case "Int64": result = CapnpPrimitive.Int64; return true;
            case "UInt8": result = CapnpPrimitive.UInt8; return true;
            case "UInt16": result = CapnpPrimitive.UInt16; return true;
            case "UInt32": result = CapnpPrimitive.UInt32; return true;
            case "UInt64": result = CapnpPrimitive.UInt64; return true;
            case "Float32": result = CapnpPrimitive.Float32; return true;
            case "Float64": result = CapnpPrimitive.Float64; return true;
            case "Text": result = CapnpPrimitive.Text; return true;
            case "Data": result = CapnpPrimitive.Data; return true;
            case "Void": result = CapnpPrimitive.Void; return true;
            case "Bool": result = CapnpPrimitive.Bool; return true;
            case "AnyPointer": result = CapnpPrimitive.AnyPointer; return true;

            default: return false;
         }
      }

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

      public override String ToString()
      {
         return Kind.ToString();
      }
   }

   class CapnpList : CapnpType
   {
      // todo: unify generic types with struct
      public CapnpType Parameter;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitList(this);
      }

      public override String ToString()
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

      public CapnpComposite Scope;
   }
   class CapnpIdType : CapnpNamedType
   {
      public UInt64? Id;
   }
   class CapnpAnnotatedType : CapnpIdType
   {
      public Annotation[] Annotations;
   }

   class CapnpAnnotation : CapnpAnnotatedType
   {
      public CapnpType ArgumentType;

      public AnnotationTypes[] Targets;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitAnnotationDecl(this);
      }

      public override String ToString()
      {
         var arg = ArgumentType == null ? "" : "(" + ArgumentType + ")";
         return "Annotation: " + Name + " " + arg + " " + Annotations + " targets " + String.Join<AnnotationTypes>(", ", Targets);
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

      public override String ToString()
      {
         return Type == null ? "Import: " + File :
                               "Import: " + File + "." + Type;
      }
   }

   class CapnpComposite : CapnpAnnotatedType
   {
      public CapnpStruct[] Structs;
      public CapnpInterface[] Interfaces;

      public CapnpConst[] Constants;
      public CapnpEnum[] Enumerations;
      public CapnpAnnotation[] AnnotationDefs;
      public CapnpUsing[] Usings;

      public CapnpModule Module
      {
         get
         {
            if (this is CapnpModule) return (CapnpModule)this;
            for (var p = Scope; p != null; p = p.Scope)
               if (p.Scope == null)
                  return (CapnpModule)p.Scope;
            throw new InvalidOperationException();
         }
      }

      private CapnpPrimitive _ResolvePrimitive(String name)
      {
         if (!(this is CapnpModule)) return null;

         CapnpPrimitive result;
         return CapnpPrimitive.TryParse(name, out result) ? result : null;
      }

      /// <summary>
      /// Returns a generic type that is closed using the generic parameters, that is to say it is open.
      /// </summary>
      public CapnpBoundGenericType MakeOpenGenericType()
      {
         var generic = this as CapnpGenericType;
         if (generic == null) return null;

         return new CapnpBoundGenericType
         {
            OpenType = generic,
            ParentScope = generic.Scope.MakeOpenGenericType(),
            TypeParameters = generic.TypeParameters
         };
      }

      /// <summary>
      /// Look for the given name in contained types or constants or annotations.
      /// </summary>
      private CapnpType _ResolveName(String name)
      {
         Debug.Assert(!name.Contains("."));
         Debug.Assert(!String.IsNullOrEmpty(name));

         Predicate<CapnpNamedType> predicate = i => i.Name == name;

         if (Char.IsUpper(name[0]))
         {
            // It's a type
            CapnpUsing @using;

            // Note: modules are never generic.
            var genericThis = this as CapnpGenericType;
            var genericParms = genericThis == null ? Empty<CapnpGenericParameter>.Array
                                                   : genericThis.TypeParameters.Cast<CapnpGenericParameter>().ToArray();

            // Note: todo we must double check this order is correct
            // todo: a top level struct must not be called "Text" > must detect
            return Array.Find(genericParms, predicate) ??
                   Array.Find(Structs, predicate) ??
                   Array.Find(Interfaces, predicate) ??
                   Array.Find(Enumerations, predicate) ??
                   ((@using = Array.Find<CapnpUsing>(Usings, predicate)) == null ? null : @using.Target) ??
                   _ResolvePrimitive(name);
         }
         else
         {
            // It's, well, not a type.
            return Array.Find(Constants, predicate) ??
                   Array.Find(AnnotationDefs, predicate);
         }
      }

      private CapnpType _ResolveName(NamePart part, CapnpComposite nameScope, CapnpBoundGenericType genericScope = null)
      {
         var result = _ResolveName(part.Name);

         if (result == null)
         {
            // Todo: this is somewhat "bolted" on, can we unify with generics?
            if (part.Name == "List" && part.TypeParameters.Length == 1)
            {
               var param = nameScope.ResolveFullName(part.TypeParameters[0]);
               if (param == null) return null;
               return new CapnpList
               {
                  Parameter = param
               };
            }
         }

         // E.g. an unresolved Using.
         if (result is CapnpReference) return result;

         if (result is CapnpGenericType)
         {
            // Resolve generic parameters given our generic state.
            // Note that the name could refer to a generic parameter creating a *partially closed* type.
            var generic = (CapnpGenericType)result;

            // We don't allow *partial* closing of a generic type (there's no syntax for it).
            if (generic.TypeParameters.Length != part.TypeParameters.Length && part.TypeParameters.Length > 0)
               return null;

            // Nothing to parameterize.
            if (generic.TypeParameters.Length == 0 && part.TypeParameters.Length > 0)
               return null;

            // If the type does not actually have generic parameters, and we're not a nested type of a generic type, we're done.
            if (!generic.IsGeneric && genericScope == null)
               return result;

            Debug.Assert(generic.TypeParameters.Length == part.TypeParameters.Length || part.TypeParameters.Length == 0);

            var resolvedParams = part.TypeParameters.Length == 0 ? generic.TypeParameters : // the type is fully open
                                                                   part.TypeParameters.Select(p => nameScope.ResolveFullName(p)).ToArray();

            // If any of the type parameters are unresolved, we have to wait.
            if (resolvedParams.Any(p => p == null || p is CapnpReference)) return null;

            // The generic scope is the scope in which generic parameters our bound.
            // If none are defined the parent scope is closed using just the generic parameters.
            var parentScope = genericScope ?? generic.Scope.MakeOpenGenericType();

            return new CapnpBoundGenericType
            {
               OpenType = generic,
               ParentScope = parentScope,
               TypeParameters = resolvedParams
            };
         }
         else if (result is CapnpBoundGenericType)
         {
            var closed = (CapnpBoundGenericType)result;

            if (part.TypeParameters.Length > 0 && closed.TypeParameters.Length != part.TypeParameters.Length)
               return null;

            var resolvedParams = closed.TypeParameters;

            if (part.TypeParameters.Length > 0)
            {
               // Cannot be partially closed and it makes no sense to close an already fully closed generic type
               Debug.Assert(closed.TypeParameters.All(p => (p is CapnpGenericParameter)));

               resolvedParams = part.TypeParameters.Select(p => nameScope.ResolveFullName(p)).ToArray();
               if (resolvedParams.Any(p => p == null || p is CapnpReference)) return null;
            }

            // Return a new closed generic whose scope is the current genericScope if defined.
            // Note that in the case both genericScope and clode.ParentScope exist, genericScope is "more closed" but point
            // to the same open type.
            Debug.Assert(genericScope == null || closed.ParentScope == null || genericScope.OpenType == closed.ParentScope.OpenType);

            return new CapnpBoundGenericType
            {
               OpenType = closed.OpenType,
               ParentScope = genericScope ?? closed.ParentScope,
               TypeParameters = resolvedParams
            };
         }
         else if (part.TypeParameters.Length > 0)
            // The name defines generic parameters but the type is not generic.
            return null;
         else if (result is CapnpGenericParameter)
            return result;
         else if (genericScope != null)
         {
            // An annotation or enumeration or whatever contained within a generic struct is itself generic.
            return new CapnpBoundGenericType
            {
               OpenType = (CapnpNamedType)result,
               ParentScope = genericScope,
               TypeParameters = Empty<CapnpType>.Array
            };
         }
         else
            return result;
      }

      public CapnpType ResolveFullName(FullName fullName)
      {
         // Find the first scope in which to start looking.
         CapnpComposite startScope = this;
         CapnpType result = null;
         for (var scope = startScope; scope != null; scope = scope.Scope)
         {
            result = scope._ResolveName(fullName[0], this, null);
            if (result != null) break;
         }

         if (result == null || fullName.Count == 1)
            return result;

         // Resolve rest.
         var container = result;
         for (var i = 1; i < fullName.Count; i++)
         {
            if (container is CapnpBoundGenericType)
            {
               var generic = (CapnpBoundGenericType)container;
               container = ((CapnpGenericType)generic.OpenType)._ResolveName(fullName[i], this, generic); // todo: this cast should succeed with correct grammar, but perhaps may fail with a bad one

               // If the reference is to a generic parameter it must resolve.
               // Note that if the type is still open it could resolve to a generic parameter.
               if (container is CapnpGenericParameter)
                  container = generic.ResolveGenericParameter((CapnpGenericParameter)container);

               if (container == null) return null;
            }
            else if (container is CapnpComposite)
            {
               container = ((CapnpComposite)container)._ResolveName(fullName[i].Name);
               if (container == null) return null;
            }
            else
               return null;
         }
         return container;
      }

      public override String ToString()
      {
         return "todo";
         //return String.Join<CapnpType>("\r\n", NestedTypes);
      }
   }

   class CapnpModule : CapnpComposite
   {
      public CapnpModule() { }

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitModule(this);
      }

      // todo: these are bad, clean up ToStrings, lineendings (dont assume crlf)
      public override String ToString()
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

   /// <summary>
   /// Represents a type with generic parameters bound.
   /// Note, however, that this binding could be to generic parameters so this type could still be fully "open".
   /// </summary>
   class CapnpBoundGenericType : CapnpType
   {
      // Note that some of these could be themselves generic, if this type is only partially closed.
      public CapnpType[] TypeParameters;

      // Note: usually this is a generic type, however, an annotation contained with a generic struct is also generic.
      public CapnpNamedType OpenType;

      public CapnpBoundGenericType ParentScope;

      public Boolean IsFullyClosed
      {
         get
         {
            return (TypeParameters.Length == 0 || !TypeParameters.Any(p => p is CapnpGenericParameter)) &&
                   (ParentScope == null || ParentScope.IsFullyClosed);
         }
      }

      // Returns true if (recursively) all generic parameters have been bound.
      public Boolean IsFullyOpen
      {
         get
         {
            return (TypeParameters.Length == 0 || TypeParameters.All(p => p is CapnpGenericParameter)) &&
                   (ParentScope == null || ParentScope.IsFullyOpen);
         }
      }

      /// <summary>
      /// Given the generic and its generic parameter bindings, bind for as much as possible any generic parameters in the current
      /// partially open type.
      /// </summary>
      public CapnpBoundGenericType CloseWith(CapnpBoundGenericType generic)
      {
         Debug.Assert(!IsFullyClosed);

         var typeParams = this.TypeParameters.Select(param => param is CapnpGenericParameter ? generic.ResolveGenericParameter((CapnpGenericParameter)param) ?? param
                                                                                             : param).ToArray();

         return new CapnpBoundGenericType
         {
            OpenType = this.OpenType,
            ParentScope = this.ParentScope == null ? null : this.ParentScope.CloseWith(generic),
            TypeParameters = typeParams
         };
      }

      public CapnpType ResolveGenericParameter(CapnpGenericParameter parameter)
      {
         var genericOpenType = OpenType as CapnpGenericType;
         var index = genericOpenType == null ? -2 : Array.IndexOf(genericOpenType.TypeParameters, parameter);
         if (index < 0) return ParentScope == null ? null : ParentScope.ResolveGenericParameter(parameter);
         var result = TypeParameters[index];
         if (result is CapnpGenericParameter && ParentScope != null)
            return ParentScope.ResolveGenericParameter((CapnpGenericParameter)result);
         return result;
      }

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitClosedType(this);
      }
   }

   class CapnpGenericType : CapnpComposite
   {
      // todo: validation, once resolved, must be valid type
      public CapnpGenericParameter[] TypeParameters;

      //// If closed, points to the open type. Null otherwise.
      //// dont set (todo)
      //public CapnpType OpenType;

      /// <summary>
      /// Returns true if this type has type parameeters, or its parent does.
      /// </summary>
      public override Boolean IsGeneric
      {
         get
         {
            return TypeParameters.Length > 0 || (this.Scope != null && this.Scope is CapnpGenericType && ((CapnpGenericType)this.Scope).IsGeneric);
         }
      }

      //public Boolean IsOpenGenericType
      //{
      //   get
      //   {
      //      //return IsGeneric && (TypeParameters[0] is CapnpGenericParameter);
      //      return OpenType == null;
      //   }
      //}

      //public Boolean IsClosedGenericType
      //{
      //   get
      //   {
      //      return OpenType != null;
      //      //return IsGeneric && !IsOpenGenericType;
      //   }
      //}

      //public CapnpGenericType Close(CapnpType[] @params)
      //{
      //   Debug.Assert(@params.Length == TypeParameters.Length);

      //   throw new Exception("todo");
      //}
   }

   class CapnpStruct : CapnpGenericType
   {
      public Field[] Fields;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitStruct(this);
      }

      public override String ToString()
      {
         var annot = Annotations == null ? "" : Annotations.ToString();
         return "Struct " + Name + " " + annot + "\r\n   " + String.Join<Field>("\r\n   ", Fields) + "\r\n\r\n" + base.ToString();
      }
   }

   class CapnpInterface : CapnpGenericType
   {
      // todo: validaiotn that these are interfaces etc
      public CapnpType[] BaseInterfaces = new CapnpType[0]; // empty<>.ar todo

      public Method[] Methods;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitInterface(this);
      }

      public override String ToString()
      {
         var extends = BaseInterfaces.Length == 0 ? "" : " extends " + String.Join<CapnpType>(", ", BaseInterfaces);
         return "Interface " + Name + extends + " " + Annotations + "\r\n   " + String.Join<Method>("\r\n   ", Methods) + "\r\n\r\n" + base.ToString();
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

      public override String ToString()
      {
         var a = Annotation == null ? "" : Annotation + " ";
         return "Const " + Name + " " + a + "=" + Value;
      }
   }

   // todo: validation, if parameters are used the type must be generic

   class CapnpGenericParameter : CapnpNamedType
   {
      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitGenericParameter(this);
      }

      public override String ToString()
      {
         return "Generic parm: " + Name;
      }
   }

   class CapnpReference : CapnpType
   {
      public FullName FullName;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitReference(this);
      }

      public override String ToString()
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

      public override String ToString()
      {
         return "Enum " + Name + " " + Annotations + "\r\n   " + String.Join<Enumerant>("\r\n   ", Enumerants);
      }
   }

   // Can these introduce scopes?
   class CapnpUnion : CapnpAnnotatedType
   {
      public Field[] Fields;

      protected internal override CapnpType Accept(CapnpVisitor visitor)
      {
         return visitor.VisitUnion(this);
      }

      public override String ToString()
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

      public override String ToString()
      {
         return "Group:\r\n" + String.Join<Field>("\r\n", Fields);
      }
   }
}
