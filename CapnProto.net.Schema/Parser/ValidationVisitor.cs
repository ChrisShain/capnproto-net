using System;

namespace CapnProto.Schema.Parser
{
   /// <summary>
   /// Validate that the "tree" makes sense. 
   /// </summary>
   class ValidationVisitor : CapnpVisitor
   {
      // todo, first collect some rules

      // This should never happen with a correct parser.
      protected internal override CapnpType VisitReference(CapnpReference @ref)
      {
         throw new InvalidOperationException("unexpected reference");
      }

      protected internal override CapnpType VisitImport(CapnpImport import)
      {
         throw new InvalidOperationException("unexpected remaining import");
      }

      protected internal override Value VisitValue(Value value)
      {
         if (value is UnresolvedValue)
            throw new InvalidOperationException("unexpected unresolved default value");
         return value;
      }
   }
}