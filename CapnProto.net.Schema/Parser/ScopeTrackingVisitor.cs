using System.Collections.Generic;

namespace CapnProto.Schema.Parser
{
   // Tracks current scope. May replace this by tracking a parent scope for each type, not sure.

   class ScopeTrackingVisitor : CapnpVisitor
   {
      // todo: must be capnpcomposite
      protected readonly List<CapnpComposite> _mScopes;

      protected ScopeTrackingVisitor()
      {
         _mScopes = new List<CapnpComposite>();
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