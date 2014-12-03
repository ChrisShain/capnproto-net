using System;
using System.Security.Cryptography;
using System.Text;

namespace CapnProto.Schema.Parser
{
   class IdGeneratingVisitor : ScopeTrackingVisitor
   {
      private static UInt64 _GenerateId(CapnpAnnotatedType type, CapnpIdType scope)
      {
         if (type.Id != null) return type.Id.Value;

         if (scope.Id == null) throw new InvalidOperationException("scope has no id set");

         var byteCount = Encoding.UTF8.GetByteCount(type.Name);
         var buffer = new Byte[byteCount + 8];

         for (var i = 0; i < 8; i++)
            buffer[i] = (Byte)(scope.Id.Value >> (i * 8));

         Encoding.UTF8.GetBytes(type.Name, 0, type.Name.Length, buffer, 8);

         var md5 = MD5.Create();
         var hash = md5.ComputeHash(buffer);

         UInt64 result = 0;
         for (var i = 0; i < 8; i++)
            result = (result << (i * 8)) | hash[i];

         return result | CapnpParser.MIN_UID;
      }

      protected internal override CapnpStruct VisitStruct(CapnpStruct @struct)
      {
         if (@struct.Id == null)
            // Note that TopScope is the parent scope here (i.e. not yet the current struct)
            @struct.Id = _GenerateId(@struct, CurrentScope);

         return base.VisitStruct(@struct);
      }

      protected internal override CapnpInterface VisitInterface(CapnpInterface @interface)
      {
         if (@interface.Id == null)
            @interface.Id = _GenerateId(@interface, CurrentScope);

         return base.VisitInterface(@interface);
      }

      protected internal override CapnpEnum VisitEnum(CapnpEnum @enum)
      {
         if (@enum.Id == null)
            @enum.Id = _GenerateId(@enum, CurrentScope);

         return base.VisitEnum(@enum);
      }

      protected internal override CapnpAnnotation VisitAnnotationDecl(CapnpAnnotation annotation)
      {
         if (annotation.Id == null)
            annotation.Id = _GenerateId(annotation, CurrentScope);
         return base.VisitAnnotationDecl(annotation);
      }
   }
}
