using System.Collections.Generic;

namespace CapnProto.Schema.Parser
{
   class ScopeTrackingVisitor : CapnpVisitor
   {
      protected readonly List<CapnpType> _mScopes;

      protected ScopeTrackingVisitor()
      {
         _mScopes = new List<CapnpType>();
      }

      protected internal sealed override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         _mScopes.Add(@struct);
         var result = base.VisitStruct(@struct);
         _mScopes.RemoveAt(_mScopes.Count - 1);
         return result;
      }

      protected internal sealed override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         _mScopes.Add(@interface);
         var result = base.VisitInterface(@interface);
         _mScopes.RemoveAt(_mScopes.Count - 1);
         return result;
      }
   }
}