﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FlubuCore.IO.Wrappers;
using FlubuCore.Scripting.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;

namespace FlubuCore.Scripting
{
    public class ScriptLoader : IScriptLoader
    {
        private readonly IFileWrapper _file;
        private readonly IScriptAnalyser _analyser;
        private readonly ILogger<ScriptLoader> _log;

        public ScriptLoader(IFileWrapper file, IScriptAnalyser analyser, ILogger<ScriptLoader> log)
        {
            _file = file;
            _analyser = analyser;
            _log = log;
        }

        public async Task<IBuildScript> FindAndCreateBuildScriptInstanceAsync(string fileName)
        {
            var coreDir = Path.GetDirectoryName(typeof(object).GetTypeInfo().Assembly.Location);
            var flubuPath = typeof(DefaultBuildScript).GetTypeInfo().Assembly.Location;
            List<MetadataReference> references = new List<MetadataReference>
            {
                // Here we get the path to the mscorlib and private mscorlib
                // libraries that are required for compilation to succeed.
                MetadataReference.CreateFromFile(Path.Combine(coreDir, "mscorlib.dll")),
                MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location),
                MetadataReference.CreateFromFile(flubuPath),
                MetadataReference.CreateFromFile(typeof(File).GetTypeInfo().Assembly.Location)
            };

            // Enumerate all assemblies referenced by this executing assembly
            // and provide them as references to the build script we're about to
            // compile.
            AssemblyName[] referencedAssemblies = Assembly.GetEntryAssembly().GetReferencedAssemblies();
            foreach (var referencedAssembly in referencedAssemblies)
            {
                Assembly loadedAssembly = Assembly.Load(referencedAssembly);
                if (string.IsNullOrEmpty(loadedAssembly.Location))
                    continue;

                references.Add(MetadataReference.CreateFromFile(loadedAssembly.Location));
            }

            List<string> code = _file.ReadAllLines(fileName);

            AnalyserResult analyserResult = _analyser.Analyze(code);
            references.AddRange(analyserResult.References.Select(i=> MetadataReference.CreateFromFile(i)));

            foreach (var csFile in analyserResult.CsFiles)
            {
                if (_file.Exists(csFile))
                {
                    _log.LogInformation($"File found: {csFile}");
                    List<string> additionalCode = _file.ReadAllLines(csFile);

                    AnalyserResult additionalCodeAnalyserResult = _analyser.Analyze(additionalCode);
                    if (additionalCodeAnalyserResult.CsFiles.Count > 0)
                    {
                        throw new NotSupportedException("//#imp is only supported in main buildscript .cs file.");
                    }

                   var usings = additionalCode.Where(x => x.StartsWith("using"));

                    references.AddRange(additionalCodeAnalyserResult.References.Select(i => MetadataReference.CreateFromFile(i)));
                    code.InsertRange(0, usings);
                    code.AddRange(additionalCode.Where(x => !x.StartsWith("using")));
                }
                else
                {
                    _log.LogInformation($"File was not found: {csFile}");
                }
            }

            var opts = ScriptOptions.Default
                .WithReferences(references);

            Script script = CSharpScript
                .Create(string.Join("\r\n", code), opts)
                .ContinueWith(string.Format("var sc = new {0}();", analyserResult.ClassName));

            ScriptState result = await script.RunAsync();

            return result.Variables[0].Value as IBuildScript;
        }
    }
}