using System;
using System.Diagnostics;
using System.Linq;

namespace CapnProto.Schema.Parser
{
   // todo: caching, but that should probably be done through a caching decorator
   // todo: recursion detection
   class ImportResolutionVisitor : CapnpVisitor
   {
      private readonly Func<String, String> _GetImportContents;

      public ImportResolutionVisitor(Func<String, String> getImportContents)
      {
         _GetImportContents = getImportContents;
      }

      private CapnpType _ResolveImport(CapnpImport import)
      {
         var source = _GetImportContents(import.File);

         var importedType = import.Type;

         var importParser = new CapnpParser(source);
         var parsedSource = importParser.Parse();
         parsedSource = importParser.ProcessParsedSource(parsedSource, _GetImportContents);

         // No type specified, so return the module for later perusal.
         if (importedType == null)
            return parsedSource;

         if (importedType is CapnpReference)
         {
            var @ref = (CapnpReference)importedType;

            // todo: refactor, this needs to use proper resolution logic
            if (@ref.FullName.Contains(".")) throw new Exception("todo: imports with scoped names");

            // todo: of course this is not confined to structs only
            importedType = parsedSource.Structs.Where(s => s.Name == @ref.FullName).Single();
         }
         else Debug.Assert(false);

         return importedType;
      }

      protected internal override CapnpUsing VisitUsing(CapnpUsing @using)
      {
         if (@using.Target is CapnpImport)
            @using.Target = _ResolveImport((CapnpImport)@using.Target);

         return base.VisitUsing(@using);
      }

      protected internal override Field VisitField(Field fld)
      {
         if (fld.Type is CapnpImport)
            fld.Type = _ResolveImport((CapnpImport)fld.Type);

         return fld;
      }
   }
}