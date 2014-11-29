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
         if (_mModule != null) throw new InvalidOperationException();
         _mModule = module;
         return base.VisitModule(module);
      }

      // todo: check types match
      protected internal override Value VisitValue(Value value)
      {
         var constRef = value as ConstRefValue;
         if (constRef == null) return value;

         var parts = constRef.FullConstName.Split('.');

         if (parts[0] == "")
            return _mModule.Constants.Where(c => c.Name == parts[1]).Single().Value;

         // todo: other types, of course. clean up this junk
         CapnpType declaringType = _mModule.Structs.Where(s => s.Name == parts[0]).Single();
         for (var i = 1; i < parts.Length - 2; i++)
            declaringType = ((CapnpStruct)declaringType).NestedTypes.Where(t => t is CapnpStruct).Cast<CapnpStruct>().Where(s => s.Name == parts[i]).Single();

         return ((CapnpStruct)declaringType).NestedTypes.Where(t => t is CapnpConst).Cast<CapnpConst>().Where(c => c.Name == parts[parts.Length - 1]).Single().Value;
      }
   }
}