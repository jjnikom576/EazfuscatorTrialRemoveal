using System;
using System.Collections.Generic;
using System.IO;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace ETR
{
    public enum PatchResult
    {
        Success,
        NoTypeFound,
        MultipleTypesUsedFirst,
        WriteFailed
    }

    /// <summary>
    /// Headless Eazfuscator.NET trial-restriction patcher.
    /// Extracted from MainWindow.xaml.cs so both GUI and CLI can share it.
    /// </summary>
    public static class Patcher
    {
        /// <summary>
        /// Load <paramref name="inputPath"/>, strip trial restriction, write result to
        /// <paramref name="outputPath"/>. Progress callback receives (percent, message)
        /// snapshots suitable for GUI or console reporting; pass null in CLI mode if not needed.
        /// </summary>
        public static PatchResult Run(string inputPath, string outputPath, Action<int, string> progress)
        {
            if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("inputPath required");
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("outputPath required");
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file not found", inputPath);

            Report(progress, 0, "Processing Start!");
            var module = ModuleDefMD.Load(inputPath);
            Report(progress, 25, "Finding EvaluationTypes");

            IList<TypeDef> evalTypes = FindTypes(module);
            if (evalTypes.Count == 0)
            {
                return PatchResult.NoTypeFound;
            }

            var resultKind = evalTypes.Count > 1
                ? PatchResult.MultipleTypesUsedFirst
                : PatchResult.Success;

            Patching(evalTypes[0]);
            Report(progress, 55, "Patching");

            try
            {
                Write(module, outputPath);
            }
            catch (Exception)
            {
                return PatchResult.WriteFailed;
            }

            Report(progress, 100, "Done Processing.");
            return resultKind;
        }

        static void Report(Action<int, string> progress, int percent, string message)
        {
            if (progress != null) progress(percent, message);
        }

        static void Patching(TypeDef evalType)
        {
            var badMethods = GetStaticMethods(evalType, "System.Boolean", "System.Boolean");
            foreach (var method in badMethods)
            {
                var instructions = method.Body.Instructions;
                instructions.Clear();
                instructions.Add(OpCodes.Ldc_I4_1.ToInstruction());
                instructions.Add(OpCodes.Ret.ToInstruction());
                method.Body.ExceptionHandlers.Clear();
            }
        }

        static IList<TypeDef> FindTypes(ModuleDefMD module)
        {
            var evalTypes = new List<TypeDef>();
            foreach (var typeDef in module.GetTypes())
            {
                // Signatures for Eazfuscator 4.9 through 2023.3 — do not simplify without
                // testing against every supported version.
                if (typeDef.Methods.Count == 7
                    && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2
                    && CountStaticMethods(typeDef, "System.Void") == 3
                    && CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1
                    && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 6
                    && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 2
                    && CountStaticMethods(typeDef, "System.Void") == 2
                    && CountStaticMethods(typeDef, "System.Void", "System.Threading.ThreadStart") == 1
                    && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 4
                    && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                    && CountStaticMethods(typeDef, "System.Void") == 2
                    && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
                else if (typeDef.Methods.Count == 3
                    && CountStaticMethods(typeDef, "System.Boolean", "System.Boolean") == 1
                    && CountStaticMethods(typeDef, "System.Void") == 1
                    && CountStaticMethods(typeDef, "System.Boolean") == 1)
                {
                    evalTypes.Add(typeDef);
                }
            }
            return evalTypes;
        }

        static int CountStaticMethods(TypeDef def, string retType, params string[] paramTypes)
        {
            return GetStaticMethods(def, retType, paramTypes).Count;
        }

        static IList<MethodDef> GetStaticMethods(TypeDef def, string retType, params string[] paramTypes)
        {
            var methods = new List<MethodDef>();
            if (!def.HasMethods) return methods;

            foreach (var method in def.Methods)
            {
                if (!method.IsStatic) continue;
                if (!method.ReturnType.FullName.Equals(retType)) continue;
                if (paramTypes.Length != method.Parameters.Count) continue;

                bool paramsMatch = true;
                for (int i = 0; i < paramTypes.Length && i < method.Parameters.Count; i++)
                {
                    if (!method.Parameters[i].Type.FullName.Equals(paramTypes[i]))
                    {
                        paramsMatch = false;
                        break;
                    }
                }
                if (!paramsMatch) continue;
                methods.Add(method);
            }
            return methods;
        }

        static void Write(ModuleDefMD module, string outputPath)
        {
            var options = new ModuleWriterOptions(module);
            options.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            options.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;
            module.Write(outputPath, options);
        }
    }
}
