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

      protected internal override CapnpUsing VisitUsing(CapnpUsing @using)
      {
         return base.VisitUsing(@using);
      }

      protected internal override Method VisitMethod(Method method)
      {
         _ValidateAnnotation(method.Annotation, AnnotationTypes.method);
         return base.VisitMethod(method);
      }

      protected internal override Field VisitField(Field fld)
      {
         _ValidateAnnotation(fld.Annotation, AnnotationTypes.field);
         return base.VisitField(fld);
      }

      protected internal override CapnpType VisitList(CapnpList list)
      {
         return base.VisitList(list);
      }

      protected internal override Parameter VisitParameter(Parameter p)
      {
         _ValidateAnnotation(p.Annotation, AnnotationTypes.param);
         return base.VisitParameter(p);
      }

      protected internal override CapnpAnnotation VisitAnnotationDecl(CapnpAnnotation annotation)
      {
         _ValidateAnnotation(annotation.Annotation, AnnotationTypes.annotation);
         _ValidateHaveId(annotation);
         return base.VisitAnnotationDecl(annotation);
      }

      protected internal override Enumerant VisitEnumerant(Enumerant e)
      {
         _ValidateAnnotation(e.Annotation, AnnotationTypes.enumerant);
         return base.VisitEnumerant(e);
      }

      protected internal override CapnpEnum VisitEnum(CapnpEnum @enum)
      {
         _ValidateAnnotation(@enum.Annotation, AnnotationTypes.@enum);
         _ValidateHaveId(@enum);
         _ValidateNumbering(@enum.Enumerants);
         return base.VisitEnum(@enum);
      }

      protected internal override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         _ValidateAnnotation(@struct.Annotation, AnnotationTypes.@struct);
         _ValidateHaveId(@struct);
         return base.VisitStruct(@struct);
      }

      protected internal override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         _ValidateAnnotation(@interface.Annotation, AnnotationTypes.@interface);
         _ValidateHaveId(@interface);
         return base.VisitInterface(@interface);
      }

      private static void _ValidateAnnotation(Annotation annotation, AnnotationTypes type)
      {
         if (annotation == null) return;
         var decl = (CapnpAnnotation)annotation.Declaration;
         if (!decl.Targets.Any(t => t == AnnotationTypes.any || t == type))
            throw new Exception("invalid annotation, cannot be applied to this declaration");
      }

      private static void _ValidateHaveId(CapnpIdType type)
      {
         if (type.Id == null)
            throw new InvalidOperationException("missing id on type " + type.Name);
      }

      private static void _ValidateNumbering(Member[] members)
      {
         if (members.Length == 0) return;
         var orderedMembers = new Member[members.Length];
         Array.Copy(members, orderedMembers, members.Length);
         Array.Sort(orderedMembers, new Comparison<Member>((m1, m2) => m1.Number - m2.Number));

         // todo: relate these errors to their contained type (or perhaps position info in member is sufficient)
         if (orderedMembers[0].Number != 0)
            throw new Exception("invalid numbering - should start at 0");
         for (var i = 1; i < orderedMembers.Length; i++)
            if (orderedMembers[i].Number != i)
               throw new Exception("invalid numbering, should not contain holes");
      }
   }
}