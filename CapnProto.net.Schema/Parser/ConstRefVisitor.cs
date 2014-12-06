using System;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   // And finally, resolve references to constants.
   class ConstRefVisitor : CapnpVisitor
   {
      private CapnpModule _mModule;

      protected internal override CapnpModule VisitModule(CapnpModule module)
      {
         if (_mModule != null) return module; // the case e.g. in using T = import "x"
         _mModule = module;
         return base.VisitModule(module);
      }

      // todo: check types match
      protected internal override Value VisitValue(Value value)
      {
         var constRef = value as ConstRefValue;
         if (constRef == null) return value;

         // A const ref must always be fully qualified, so is resolved from top-level scope (module).
         // todo: can we have const ref to const?
         var resolvedRef = _mModule.ResolveFullName(constRef.FullConstName) as CapnpConst;

         if (resolvedRef == null)
            throw new Exception("const ref does not refer to a const");

         return resolvedRef.Value;
      }
   }
}