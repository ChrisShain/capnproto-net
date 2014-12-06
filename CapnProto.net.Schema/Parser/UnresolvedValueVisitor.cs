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

         if (value.Type == CapnpPrimitive.Void)
         {
            Debug.Assert(unresolvedValue.RawData == null);
            return value;
         }

         return CapnpParser.ParseValue(unresolvedValue.RawData, value.Type); // < todo relative positioning for errors yadida
      }

      // As the declaration may have been a reference, we can now resolve the argument because we know
      // the type from the declaration.
      protected internal override Annotation VisitAnnotation(Annotation annotation)
      {
         if (annotation == null) return null;

         Debug.Assert(!(annotation.Declaration is CapnpReference));

         var decl = (CapnpAnnotation)annotation.Declaration;

         if (annotation.Argument == null)
         {
            Debug.Assert(decl.ArgumentType == CapnpPrimitive.Void);
            return annotation;
         }

         var v = annotation.Argument as UnresolvedValue;
         if (v == null) return annotation;

         // Now that we know the argument type, resolve the value.
         var resolvedValue = VisitValue(new UnresolvedValue(decl.ArgumentType)
         {
            Position = -1, // todo
            RawData = v == null ? null : v.RawData
         });

         return new Annotation
         {
            Declaration = decl,
            Argument = resolvedValue
         };
      }
   }
}