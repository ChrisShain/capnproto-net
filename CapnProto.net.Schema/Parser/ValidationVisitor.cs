using System;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   /// <summary>
   /// Validate that the "tree" makes sense. 
   /// </summary>
   class ValidationVisitor : CapnpVisitor
   {
      // todo, first collect some rules
      // - numbers should not contain any 'holes'
      // - correct application of annotations (finish below todo)

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

      private Boolean _mVisitingModule;

      protected internal override CapnpModule VisitModule(CapnpModule module)
      {
         if (_mVisitingModule) throw new InvalidOperationException("already visiting a module");
         _mVisitingModule = true;
         var m = base.VisitModule(module);
         _mVisitingModule = false;
         return m;
      }

      protected internal override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         _ValidateAnnotation(@struct.Annotation, AnnotationTypes.@struct);
         return base.VisitStruct(@struct);
      }

      protected internal override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         _ValidateAnnotation(@interface.Annotation, AnnotationTypes.@interface);
         return base.VisitInterface(@interface);
      }

      private static void _ValidateAnnotation(Annotation annotation, AnnotationTypes type)
      {
         if (annotation == null) return;
         var decl = (CapnpAnnotation)annotation.Declaration;
         if (!decl.Targets.Any(t => t == AnnotationTypes.any || t == type))
            throw new Exception("invalid annotation, cannot be applied to this declaration");
      }
   }
}