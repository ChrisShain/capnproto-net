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

         return CapnpParser.ParseValue(unresolvedValue.RawData, value.Type); // < todo relative positioning for errors yadida
      }

      protected internal override Annotation VisitAnnotation(Annotation annotation)
      {
         if (annotation == null) return null;

         Debug.Assert(!(annotation.Declaration is CapnpReference));
         Debug.Assert(annotation.Argument.Type == null); // type not known until resolution

         var decl = (CapnpAnnotation)annotation.Declaration;
         var v = (UnresolvedValue)annotation.Argument;

         // Now that we know the argument type, resolve the value.
         var resolvedValue = VisitValue(new UnresolvedValue(decl.ArgumentType)
         {
            Position = -1, // todo
            RawData = v.RawData
         });

         return new Annotation
         {
            Declaration = decl,
            Argument = resolvedValue
         };
      }
   }
}