using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using CapnProtonet.CapnProto_VSPackage;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextTemplating.VSHost;

namespace CapnProtonet.CapnProto_net_VSPackage
{
    // Note: the class name is used as the name of the Custom Tool from the end-user's perspective.
    [ComVisible(true)]
    [Guid("18786B62-E81D-45CD-9E91-DCCF75E5E4FB")]
    [ProvideObject(typeof(CapnProtoSingleFileGenerator))]
    [ProvideCodeGeneratorExtension("CapnProtoSingleFileGenerator", ".capnp")]
    [CodeGeneratorRegistration(typeof(CapnProtoSingleFileGenerator), "CapnProtoSingleFileGenerator", VsContextGuids.VsContextGuidVcsProject, GeneratesDesignTimeSource = true)]
    public class CapnProtoSingleFileGenerator : CustomToolBase
    {
        protected override string DefaultExtension()
        {
            return ".cs";
        }

        protected override byte[] Generate(string inputFilePath, string inputFileContents, string defaultNamespace, IVsGeneratorProgress progressCallback)
        {
            var localDir = Path.GetDirectoryName(typeof (CapnProtoSingleFileGenerator).Assembly.Location);

            var capnpProcess = new Process
                {
                    StartInfo =
                        {
                            FileName = Path.Combine(localDir, "capnp.exe"),
                            Arguments = String.Format("compile -ocsharp \"{0}\"", inputFilePath),
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = localDir,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        }, 
                };

            capnpProcess.Start();
            capnpProcess.StandardInput.Write(inputFileContents);

            var stdOut = capnpProcess.StandardOutput.ReadToEnd();
            var stdErr = capnpProcess.StandardError.ReadToEnd();
            if (!String.IsNullOrEmpty(stdErr))
            {
                return new UTF8Encoding(true).GetBytes(stdErr); 
            }

            capnpProcess.Close();

            return new UTF8Encoding(true).GetBytes(stdOut);
        }
    }
}
