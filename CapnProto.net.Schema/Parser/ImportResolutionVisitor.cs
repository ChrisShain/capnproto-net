using System;
using System.Diagnostics;

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
            return parsedSource.ResolveFullName(@ref.FullName);
         }
         else Debug.Assert(false);

         return importedType;
      }

      protected internal override CapnpUsing VisitUsing(CapnpUsing @using)
      {
         if (@using.Target is CapnpImport)
         {
            @using.Target = _ResolveImport((CapnpImport)@using.Target);
            if (@using.Target == null)
               throw new Exception("failed to resolve import"); // todo
         }

         return base.VisitUsing(@using);
      }

      protected internal override Field VisitField(Field fld)
      {
         if (fld.Type is CapnpImport)
         {
            fld.Type = _ResolveImport((CapnpImport)fld.Type);
            if (fld.Type == null)
               throw new Exception("failed to resolve field import, fld is " + fld.Name); // todo
         }

         return fld;
      }
   }
}