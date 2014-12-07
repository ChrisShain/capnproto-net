
namespace CapnProto.Schema.Parser
{
   class ScopeTrackingVisitor : CapnpVisitor
   {
      protected CapnpComposite mCurrentScope;

      protected internal override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         if (!mEnableNestedType) return @struct;

         mCurrentScope = @struct;
         var s = base.VisitStruct(@struct);
         mCurrentScope = s.Scope;
         return s;
      }

      protected internal override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         if (!mEnableNestedType) return @interface;

         mCurrentScope = @interface;
         var i = base.VisitInterface(@interface);
         mCurrentScope = i.Scope;
         return i;
      }

      protected internal override CapnpModule VisitModule(CapnpModule module)
      {
         if (mActiveModule != null && mActiveModule != module) return module;

         mCurrentScope = module;
         var m = base.VisitModule(module);
         mCurrentScope = m;
         return m;
      }
   }
}
