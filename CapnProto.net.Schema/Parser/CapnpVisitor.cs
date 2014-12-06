using System;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   class CapnpVisitor
   {
      protected Boolean mEnableNestedType = true;

      protected void EnableNestedType()
      {
         mEnableNestedType = true;
      }

      protected void DisableNestedType()
      {
         mEnableNestedType = false;
      }

      public virtual CapnpType Visit(CapnpType target)
      {
         if (target == null) return null;
         return target.Accept(this);
      }

      protected internal virtual CapnpType VisitPrimitive(CapnpPrimitive primitive)
      {
         return primitive;
      }

      protected internal virtual CapnpType VisitList(CapnpList list)
      {
         list.Parameter = Visit(list.Parameter);
         return list;
      }

      protected internal virtual Value VisitValue(Value value)
      {
         return value;
      }

      protected internal virtual CapnpModule VisitModule(CapnpModule module)
      {
         module.Structs = module.Structs.Select(s => VisitStruct(s)).ToArray();
         module.Interfaces = module.Interfaces.Select(i => VisitInterface(i)).ToArray();
         module.Constants = module.Constants.Select(c => VisitConst(c)).ToArray();
         module.Enumerations = module.Enumerations.Select(e => VisitEnum(e)).ToArray();
         module.AnnotationDefs = module.AnnotationDefs.Select(a => VisitAnnotationDecl(a)).ToArray();
         module.Usings = module.Usings.Select(u => VisitUsing(u)).ToArray();
         module.Annotations = module.Annotations.Select(a => VisitAnnotation(a)).ToArray();
         return module;
      }

      protected internal virtual Annotation VisitAnnotation(Annotation annotation)
      {
         if (annotation == null) return null;

         annotation.Declaration = Visit(annotation.Declaration);
         annotation.Argument = VisitValue(annotation.Argument);
         return annotation;
      }

      protected internal virtual CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         if (!mEnableNestedType) return @struct;

         @struct.Structs = @struct.Structs.Select(s => VisitStruct(s)).ToArray();
         @struct.Interfaces = @struct.Interfaces.Select(i => VisitInterface(i)).ToArray();
         @struct.Enumerations = @struct.Enumerations.Select(e => VisitEnum(e)).ToArray();

         DisableNestedType();

         @struct.Fields = @struct.Fields.Select(f => VisitField(f)).ToArray();
         @struct.Annotations = @struct.Annotations.Select(a => VisitAnnotation(a)).ToArray();
         @struct.Usings = @struct.Usings.Select(u => VisitUsing(u)).ToArray();

         EnableNestedType();

         return @struct;
      }

      protected internal virtual CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         if (!mEnableNestedType) return @interface;

         @interface.Structs = @interface.Structs.Select(s => VisitStruct(s)).ToArray();
         @interface.Interfaces = @interface.Interfaces.Select(i => VisitInterface(i)).ToArray();
         @interface.Enumerations = @interface.Enumerations.Select(e => VisitEnum(e)).ToArray();

         DisableNestedType();

         @interface.BaseInterfaces = @interface.BaseInterfaces.Select(i => Visit(i)).ToArray();
         @interface.Annotations = @interface.Annotations.Select(a => VisitAnnotation(a)).ToArray();
         @interface.Methods = @interface.Methods.Select(m => VisitMethod(m)).ToArray();
         @interface.Usings = @interface.Usings.Select(u => VisitUsing(u)).ToArray();

         EnableNestedType();

         return @interface;
      }

      protected internal virtual CapnpEnum VisitEnum(CapnpEnum @enum)
      {
         @enum.Annotations = @enum.Annotations.Select(a => VisitAnnotation(a)).ToArray();
         @enum.Enumerants = @enum.Enumerants.Select(e => VisitEnumerant(e)).ToArray();
         return @enum;
      }

      protected internal virtual Enumerant VisitEnumerant(Enumerant e)
      {
         e.Annotation = VisitAnnotation(e.Annotation);
         return e;
      }

      protected internal virtual Field VisitField(Field fld)
      {
         fld.Type = Visit(fld.Type);
         fld.Value = VisitValue(fld.Value);
         fld.Annotation = VisitAnnotation(fld.Annotation);
         return fld;
      }

      protected internal virtual Method VisitMethod(Method method)
      {
         method.Arguments = method.Arguments.Select(p => VisitParameter(p)).ToArray();
         method.ReturnType = VisitParameter(method.ReturnType);
         method.Annotation = VisitAnnotation(method.Annotation);
         return method;
      }

      protected internal virtual Parameter VisitParameter(Parameter p)
      {
         p.Type = Visit(p.Type);
         p.Annotation = VisitAnnotation(p.Annotation);
         p.DefaultValue = VisitValue(p.DefaultValue);
         return p;
      }

      protected internal virtual CapnpGroup VisitGroup(CapnpGroup grp)
      {
         grp.Fields = grp.Fields.Select(f => VisitField(f)).ToArray();
         return grp;
      }

      protected internal virtual CapnpUnion VisitUnion(CapnpUnion union)
      {
         union.Fields = union.Fields.Select(u => VisitField(u)).ToArray();
         return union;
      }

      protected internal virtual CapnpType VisitReference(CapnpReference @ref)
      {
         return @ref;
      }

      protected internal virtual CapnpConst VisitConst(CapnpConst @const)
      {
         @const.Value = VisitValue(@const.Value);
         return @const;
      }

      protected internal virtual CapnpType VisitImport(CapnpImport import)
      {
         import.Type = Visit(import.Type);
         return import;
      }

      protected internal virtual CapnpUsing VisitUsing(CapnpUsing @using)
      {
         @using.Target = Visit(@using.Target);
         return @using;
      }

      protected internal virtual CapnpAnnotation VisitAnnotationDecl(CapnpAnnotation annotation)
      {
         annotation.ArgumentType = Visit(annotation.ArgumentType);
         return annotation;
      }
   }
}