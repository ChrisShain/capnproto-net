using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapnProto.Schema.Parser
{
   // Look at edge cases const lookup vs annotation lookup (TODO).
   class ReferenceResolutionVisitor : ScopeTrackingVisitor
   {
      private readonly CapnpModule _mModule;

      private Int32 _mUnresolvedRefCount = 0;
      private List<CapnpReference> _mRefs = new List<CapnpReference>();

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
         // Todo: I'll keep this for now as it's easier to debug, however, we could fold the first pass into our parser
         // because it already resolve and parses some values directly. That would mean we only require one pass here.
         _mUnresolvedRefCount = 0;
         _mRefs.Clear();
         VisitModule(_mModule);

         if (_mUnresolvedRefCount > 0)
         {
            _mUnresolvedRefCount = 0;
            _mRefs.Clear();
            VisitModule(_mModule);
         }

         Debug.Assert(_mUnresolvedRefCount == 0);
      }

      protected internal override CapnpType VisitReference(CapnpReference @ref)
      {
         _mUnresolvedRefCount += 1;
         _mRefs.Add(@ref);
         var result = mCurrentScope.ResolveFullName(@ref.FullName);

         if (result != null && !(result is CapnpReference))
         {
            _mUnresolvedRefCount -= 1;
            _mRefs.Remove(@ref);
            return result;
         }

         // No change, nothing resolved.
         return @ref;
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
   }
}