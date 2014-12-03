using System.Collections.Generic;

namespace CapnProto.Schema.Parser
{
   class ScopeTrackingVisitor : CapnpVisitor
   {
      protected readonly List<CapnpIdType> _mScopes;

      protected ScopeTrackingVisitor()
      {
         _mScopes = new List<CapnpIdType>();
      }

      protected CapnpIdType CurrentScope { get { return _mScopes[_mScopes.Count - 1]; } }

      protected internal override CapnpModule VisitModule(CapnpModule module)
      {
         _mScopes.Add(module);
         var m = base.VisitModule(module);
         _mScopes.RemoveAt(_mScopes.Count - 1);
         return m;
      }

      protected internal override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         _mScopes.Add(@struct);
         var result = base.VisitStruct(@struct);
         _mScopes.RemoveAt(_mScopes.Count - 1);
         return result;
      }

      protected internal override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         _mScopes.Add(@interface);
         var result = base.VisitInterface(@interface);
         _mScopes.RemoveAt(_mScopes.Count - 1);
         return result;
      }
   }
}