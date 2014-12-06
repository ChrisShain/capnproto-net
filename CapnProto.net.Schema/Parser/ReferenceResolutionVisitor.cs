using System;
using System.Diagnostics;

namespace CapnProto.Schema.Parser
{
   class ReferenceResolutionVisitor : ScopeTrackingVisitor
   {
      private readonly CapnpModule _mModule;

      private Int32 _mUnresolvedRefCount = 0;

      public ReferenceResolutionVisitor(CapnpModule module)
         : base()
      {
         _mModule = module;
      }

      public void ResolveReferences()
      {
         // Visit the module twice. The reason this works is that the first time we have an unresolved reference
         // it must be a forward reference, thus we must have seen its target after the first pass and only one more
         // pass is needed.
         _mUnresolvedRefCount = 0;
         VisitModule(_mModule);

         if (_mUnresolvedRefCount > 0)
         {
            _mUnresolvedRefCount = 0;
            VisitModule(_mModule);
         }

         Debug.Assert(_mUnresolvedRefCount == 0);
      }

      protected internal override CapnpType VisitReference(CapnpReference @ref)
      {
         _mUnresolvedRefCount += 1;
         var result = ResolveName(@ref.FullName);
         if (result != null && !(result is CapnpReference))
            _mUnresolvedRefCount -= 1;

         return result ?? @ref;
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

      private CapnpType ResolveName(FullName name)
      {
         Debug.Assert(name.Count > 0);

         Int32 level;
         CapnpType result = null;

         // Find the first scope that defines the first part of the name.
         for (level = _mScopes.Count - 1; level >= 0; level--)
         {
            result = _mScopes[level].ResolveName(name[0]);
            if (result != null)
               break;
         }

         if (result == null || name.Count == 1)
            return result;

         var container = result as CapnpComposite;
         if (container == null)
            return null;

         return container.ResolveFullName(name.From(1));
      }
   }
}