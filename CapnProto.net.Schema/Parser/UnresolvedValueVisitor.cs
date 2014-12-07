using System;
using System.Diagnostics;

namespace CapnProto.Schema.Parser
{
   // Once all types are known, parse all unresolved values into their actual values with type.
   class UnresolvedValueVisitor : CapnpVisitor
   {
      protected internal override Value VisitValue(Value value)
      {
         if (value == null) return null;

         var unresolvedValue = value as UnresolvedValue;
         if (unresolvedValue == null) return value;

         if (value.Type is CapnpReference) throw new Exception("unexpected reference in value");

         //if (value.Type == CapnpPrimitive.Void)
         //{
         //   if (unresolvedValue.RawData == "void") return value;
         //   throw new Exception("invalid Void value: " + unresolvedValue.RawData);
         //}

         var genericType = value.Type as CapnpGenericType;
         if (genericType != null && genericType.IsGeneric)
            throw new InvalidOperationException("cannot parse unresolved value with open generic type");

         if (value.Type is CapnpGenericParameter)
            throw new InvalidOperationException("cannot parse an unresolved value for a generic parameter");

         // todo: double check no const refs here?
         return CapnpParser.ParseValue(unresolvedValue.RawData, value.Type); // < todo relative positioning for errors yadida
      }

      // As the declaration may have been a reference, we can now resolve the argument because we know
      // the type from the declaration.
      protected internal override Annotation VisitAnnotation(Annotation annotation)
      {
         if (annotation == null) return null;

         Debug.Assert(!(annotation.Declaration is CapnpReference));

         var decl = annotation.Declaration as CapnpAnnotation;

         var genericDeclaration = annotation.Declaration as CapnpBoundGenericType;
         if (genericDeclaration != null)
         {
            decl = (CapnpAnnotation)genericDeclaration.OpenType;


            //// The annotation is declared within a generic type, so close that type.
            //argType = new CapnpClosedGenericType
            //{
            //   OpenType = genericDeclaration.OpenType,
            //   TypeParameters = genericDeclaration.ParentScope.TypeParameters,
            //   ParentScope = genericDeclaration.ParentScope.ParentScope
            //};
         }





         if (decl != null && annotation.Argument == null)
         {
            // todo: what abotu generics? is this even valid?
            Debug.Assert(decl.ArgumentType == CapnpPrimitive.Void);
            return annotation;
         }

         var v = annotation.Argument as UnresolvedValue;
         if (v == null) return annotation;

         var argType = decl.ArgumentType;
         if (genericDeclaration != null && argType is CapnpGenericParameter)
            argType = genericDeclaration.ResolveGenericParameter((CapnpGenericParameter)argType);

         // Now that we know the argument type, resolve the value.
         var resolvedValue = VisitValue(new UnresolvedValue(argType)
         {
            Position = -1, // todo
            RawData = v == null ? null : v.RawData
         });

         return new Annotation
         {
            Declaration = genericDeclaration == null ? (CapnpType)decl : genericDeclaration,
            Argument = resolvedValue
         };
      }
   }
}