using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   class ReferenceResolutionVisitor : ScopeTrackingVisitor
   {
      private readonly CapnpModule _mModule;

      private HashSet<CapnpReference> _mUnresolvedReferences;

      public ReferenceResolutionVisitor(CapnpModule module)
         : base()
      {
         _mModule = module;
         _mScopes.Add(module);
         _mUnresolvedReferences = new HashSet<CapnpReference>();
      }

      public void ResolveReferences()
      {
         Visit(_mModule);

         if (_mUnresolvedReferences.Count > 0)
         {
            Visit(_mModule);
         }

         if (_mUnresolvedReferences.Count > 0)
            throw new Exception(); // todo
      }

      protected internal override CapnpType VisitReference(CapnpReference @ref)
      {
         var result = ResolveName(@ref.FullName);
         if (!(result is CapnpReference))
            _mUnresolvedReferences.Remove(@ref);
         return result;
      }

      protected internal override Value VisitValue(Value value)
      {
         if (value == null) return value;
         if (value.Type == null) return value; // nothing to resolve here (used by annotations)

         var unresolved = value as UnresolvedValue;
         if (unresolved == null)
         {
            Debug.Assert(!(value.Type is CapnpReference));
            return value;
         }

         var type = Visit(unresolved.Type);
         if (type != unresolved.Type)
            return new UnresolvedValue(type)
            {
               Position = unresolved.Position,
               RawData = unresolved.RawData
            };

         return value;
      }

      #region Resolution Logic

      // todo: nameable types?
      private String GetName(CapnpType type)
      {
         var namedType = type as CapnpNamedType;
         if (namedType != null) return namedType.Name;

         if (type is CapnpModule) return null;

         throw new InvalidOperationException();
      }

      private CapnpType ResolveName(String fullName)
      {
         if (fullName.StartsWith(".")) throw new Exception("const references: to do");

         // todo: more validation (e.g. a..b etc)
         var parts = fullName.Split('.');

         Int32 level;
         CapnpType result = null;

         for (level = _mScopes.Count - 1; level >= 0; level--)
         {
            result = ResolveNameAgainstScope(parts[0], _mScopes[level]);
            if (result != null)
               break;
         }

         if (result == null) throw new Exception("todo: resolve " + fullName);

         // now resolve the rest of the chain
         for (var i = 1; i < parts.Length; i++)
         {
            result = ResolveNameAgainstScope(parts[i], result);
         }

         if (result == null)
            throw new Exception("failed to resolve");

         return result;
      }

      private CapnpType ResolveNameAgainstScope(String name, CapnpType scope)
      {
         if (scope is CapnpReference) return scope;

         CapnpType result;

         // todo: we probably need to introduce a "scoped" type that holds usings and so on

         var @struct = scope as CapnpStruct;
         if (@struct != null)
         {
            // todo todo todo
            result = @struct.NestedTypes.Where(n => GetName(n) == name).SingleOrDefault();

            if (result == null)
               result = @struct.Usings.Where(u => u.Name == name).Select(u => u.Target).SingleOrDefault();

            goto HANDLE_RESULT;
         }

         var @interface = scope as CapnpStruct;
         if (@interface != null)
         {
            // todo
            result = @interface.NestedTypes.Where(n => GetName(n) == name).SingleOrDefault();

            if (result == null)
               result = @interface.Usings.Where(u => u.Name == name).Select(u => u.Target).SingleOrDefault();

            goto HANDLE_RESULT;
         }

         // todo: names can clash, e.g. annotation decl of same name as struct -> find out what's allowed, precedence or simply illegal?
         var module = scope as CapnpModule;
         if (module != null)
         {
            result = module.Structs.Where(s => GetName(s) == name).SingleOrDefault();

            if (result == null)
               result = module.Usings.Where(u => u.Name == name).Select(u => u.Target).SingleOrDefault();

            if (result == null)
               result = module.AnnotationDefs.Where(d => d.Name == name).SingleOrDefault();

            if (result == null)
               result = module.Enumerations.Where(e => e.Name == name).SingleOrDefault();

            if (result == null)
               result = module.Interfaces.Where(e => e.Name == name).SingleOrDefault();

            goto HANDLE_RESULT;
         }

         return null;

      HANDLE_RESULT:
         if (result is CapnpReference)
         {
            var unresolved = (CapnpReference)result;
            _mUnresolvedReferences.Add(unresolved);
         }

         return result;
      }

      #endregion
   }
}