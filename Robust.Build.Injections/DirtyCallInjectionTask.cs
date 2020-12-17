﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Robust.Build.Injections
{
    public partial class DirtyCallInjectionTask : ITask
    {
        [Required]
        public string AssemblyFile { get; set; }

        [Required]
        public string IntermediatePath { get; set; }

        [Required]
        public string AssemblyReferencePath { get; set; }

        public IBuildEngine BuildEngine { get; set; }
        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            var originalCopyPath = $"{IntermediatePath}dirty_call_injector_copy.dll";
            File.Copy(AssemblyFile, originalCopyPath, true);
            File.Delete(AssemblyFile);

            var inputPdb = GetPdbPath(AssemblyFile);
            var pdbExists = false;
            if (File.Exists(inputPdb))
            {
                var copyPdb = GetPdbPath(originalCopyPath);
                File.Copy(inputPdb, copyPdb, true);
                File.Delete(inputPdb);
                pdbExists = true;
            }

            BuildEngine.LogMessage($"DirtyCallInjection -> AssemblyFile:{AssemblyFile}", MessageImportance.Low);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(AssemblyReferencePath);
            var readerParameters = new ReaderParameters { AssemblyResolver = resolver };
            if (pdbExists)
            {
                readerParameters.ReadSymbols = true;
            }

            var asdef = AssemblyDefinition.ReadAssembly(originalCopyPath, readerParameters);

            var iCompType = asdef.MainModule.GetType("Robust.Shared.Interfaces.GameObjects.IComponent");
            if(iCompType == null)
            {
                if (!asdef.MainModule.TryGetTypeReference("Robust.Shared.Interfaces.GameObjects.IComponent",
                    out var iCompTypeRef))
                {
                    BuildEngine.LogError("DirtyMethodFinder","No IComponent-Type found!", "");
                    return false;
                }
                else
                {
                    iCompType = iCompTypeRef.Resolve();
                }
            }

            var dirtyMethod = iCompType.Methods.FirstOrDefault(m => m.Name == "Dirty");
            if (dirtyMethod == null)
            {
                BuildEngine.LogError("DirtyMethodFinder","No Dirty-Method found!", "");
                return false;
            }

            var internalDirtyMethod = asdef.MainModule.ImportReference(dirtyMethod);

            foreach (var typeDef in asdef.MainModule.Types)
            {
                if(!IsComponent(typeDef)) continue;

                foreach (var propDef in typeDef.Properties.Where(propDef => propDef.CustomAttributes.Any(a => a.AttributeType.FullName == "Robust.Shared.Injections.DirtyAttribute")))
                {
                    BuildEngine.LogMessage($"Found marked property {propDef} of type {typeDef}.", MessageImportance.Low);

                    if (!IsDefaultBody(propDef.SetMethod.Body))
                    {
                        BuildEngine.LogError("CustomSetterFound",$"Property {propDef} of type {typeDef} was marked [Dirty] but has a custom Setter.", typeDef.FullName);
                        return false;
                    }

                    //only needed for comparison
                    //var backingField = (FieldReference)propDef.SetMethod.Body.Instructions[2].Operand;
                    var ilProcessor = propDef.SetMethod.Body.GetILProcessor();
                    var instr = ilProcessor.Body.Instructions;
                    var first = instr[0];
                    var second = instr[1];
                    var third = instr[2];
                    var fourth = instr[3];
                    ilProcessor.Clear();
                    //TODO add comparison OPTION, have a bool field in the attribute to specify whether dirty should be called only on value change or not
                    //ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
                    //ilProcessor.Append(ilProcessor.Create(OpCodes.Ldfld, backingField));
                    //ilProcessor.Append( ilProcessor.Create(OpCodes.Ldarg_1));
                    //ilProcessor.Append(ilProcessor.Create(OpCodes.Beq, fourth));
                    ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
                    ilProcessor.Append(ilProcessor.Create(OpCodes.Call, internalDirtyMethod));
                    ilProcessor.Append(first);
                    ilProcessor.Append(second);
                    ilProcessor.Append(third);
                    ilProcessor.Append(fourth);
                }
            }

            if (pdbExists)
            {
                var writerParameters = new WriterParameters {WriteSymbols = true};
                asdef.Write(AssemblyFile, writerParameters);
            }else
            {
                asdef.Write(AssemblyFile);
            }

            asdef.Dispose();
            return true;
        }
    }
}
