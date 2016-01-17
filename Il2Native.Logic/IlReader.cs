﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IlReader.cs" company="">
// </copyright>
// <summary>
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace Il2Native.Logic
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Text;
    using Il2Native.Logic.CodeParts;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Symbols;
    using Microsoft.CodeAnalysis.CSharp.Symbols.Metadata.PE;

    using PEAssemblyReader;

    using OpCodesEmit = System.Reflection.Emit.OpCodes;

    /// <summary>
    /// </summary>
    public class IlReader : IIlReader
    {
        /// <summary>
        /// </summary>
        private static readonly OpCode[] OpCodesMap = new OpCode[256];

        /// <summary>
        /// </summary>
        private readonly IDictionary<string, IType> ResolvedTypes = new SortedDictionary<string, IType>();

        /// <summary>
        /// </summary>
        private readonly IDictionary<AssemblyIdentity, AssemblySymbol> cache = new Dictionary<AssemblyIdentity, AssemblySymbol>();

        /// <summary>
        /// </summary>
        private readonly bool isDll;

        /// <summary>
        /// </summary>
        private readonly Lazy<IEnumerable<IType>> lazyAllTypes;

        /// <summary>
        /// </summary>
        private readonly Lazy<IEnumerable<IType>> lazyTypes;

        /// <summary>
        /// </summary>
        private readonly IList<UnifiedAssembly<AssemblySymbol>> unifiedAssemblies =
            new List<UnifiedAssembly<AssemblySymbol>>();

        /// <summary>
        /// </summary>
        private Lazy<IModule> lasyModule;

        /// <summary>
        /// </summary>
        private Lazy<SystemTypes> lasySystem;

        /// <summary>
        /// </summary>
        static IlReader()
        {
            OpCodesMap[(int)Code.Nop] = OpCodesEmit.Nop;
            OpCodesMap[(int)Code.Break] = OpCodesEmit.Break;
            OpCodesMap[(int)Code.Ldarg_0] = OpCodesEmit.Ldarg_0;
            OpCodesMap[(int)Code.Ldarg_1] = OpCodesEmit.Ldarg_1;
            OpCodesMap[(int)Code.Ldarg_2] = OpCodesEmit.Ldarg_2;
            OpCodesMap[(int)Code.Ldarg_3] = OpCodesEmit.Ldarg_3;
            OpCodesMap[(int)Code.Ldloc_0] = OpCodesEmit.Ldloc_0;
            OpCodesMap[(int)Code.Ldloc_1] = OpCodesEmit.Ldloc_1;
            OpCodesMap[(int)Code.Ldloc_2] = OpCodesEmit.Ldloc_2;
            OpCodesMap[(int)Code.Ldloc_3] = OpCodesEmit.Ldloc_3;
            OpCodesMap[(int)Code.Stloc_0] = OpCodesEmit.Stloc_0;
            OpCodesMap[(int)Code.Stloc_1] = OpCodesEmit.Stloc_1;
            OpCodesMap[(int)Code.Stloc_2] = OpCodesEmit.Stloc_2;
            OpCodesMap[(int)Code.Stloc_3] = OpCodesEmit.Stloc_3;
            OpCodesMap[(int)Code.Ldarg_S] = OpCodesEmit.Ldarg_S;
            OpCodesMap[(int)Code.Ldarga_S] = OpCodesEmit.Ldarga_S;
            OpCodesMap[(int)Code.Starg_S] = OpCodesEmit.Starg_S;
            OpCodesMap[(int)Code.Ldloc_S] = OpCodesEmit.Ldloc_S;
            OpCodesMap[(int)Code.Ldloca_S] = OpCodesEmit.Ldloca_S;
            OpCodesMap[(int)Code.Stloc_S] = OpCodesEmit.Stloc_S;
            OpCodesMap[(int)Code.Ldnull] = OpCodesEmit.Ldnull;
            OpCodesMap[(int)Code.Ldc_I4_M1] = OpCodesEmit.Ldc_I4_M1;
            OpCodesMap[(int)Code.Ldc_I4_0] = OpCodesEmit.Ldc_I4_0;
            OpCodesMap[(int)Code.Ldc_I4_1] = OpCodesEmit.Ldc_I4_1;
            OpCodesMap[(int)Code.Ldc_I4_2] = OpCodesEmit.Ldc_I4_2;
            OpCodesMap[(int)Code.Ldc_I4_3] = OpCodesEmit.Ldc_I4_3;
            OpCodesMap[(int)Code.Ldc_I4_4] = OpCodesEmit.Ldc_I4_4;
            OpCodesMap[(int)Code.Ldc_I4_5] = OpCodesEmit.Ldc_I4_5;
            OpCodesMap[(int)Code.Ldc_I4_6] = OpCodesEmit.Ldc_I4_6;
            OpCodesMap[(int)Code.Ldc_I4_7] = OpCodesEmit.Ldc_I4_7;
            OpCodesMap[(int)Code.Ldc_I4_8] = OpCodesEmit.Ldc_I4_8;
            OpCodesMap[(int)Code.Ldc_I4_S] = OpCodesEmit.Ldc_I4_S;
            OpCodesMap[(int)Code.Ldc_I4] = OpCodesEmit.Ldc_I4;
            OpCodesMap[(int)Code.Ldc_I8] = OpCodesEmit.Ldc_I8;
            OpCodesMap[(int)Code.Ldc_R4] = OpCodesEmit.Ldc_R4;
            OpCodesMap[(int)Code.Ldc_R8] = OpCodesEmit.Ldc_R8;
            OpCodesMap[(int)Code.Dup] = OpCodesEmit.Dup;
            OpCodesMap[(int)Code.Pop] = OpCodesEmit.Pop;
            OpCodesMap[(int)Code.Jmp] = OpCodesEmit.Jmp;
            OpCodesMap[(int)Code.Call] = OpCodesEmit.Call;
            OpCodesMap[(int)Code.Calli] = OpCodesEmit.Calli;
            OpCodesMap[(int)Code.Ret] = OpCodesEmit.Ret;
            OpCodesMap[(int)Code.Br_S] = OpCodesEmit.Br_S;
            OpCodesMap[(int)Code.Brfalse_S] = OpCodesEmit.Brfalse_S;
            OpCodesMap[(int)Code.Brtrue_S] = OpCodesEmit.Brtrue_S;
            OpCodesMap[(int)Code.Beq_S] = OpCodesEmit.Beq_S;
            OpCodesMap[(int)Code.Bge_S] = OpCodesEmit.Bge_S;
            OpCodesMap[(int)Code.Bgt_S] = OpCodesEmit.Bgt_S;
            OpCodesMap[(int)Code.Ble_S] = OpCodesEmit.Ble_S;
            OpCodesMap[(int)Code.Blt_S] = OpCodesEmit.Blt_S;
            OpCodesMap[(int)Code.Bne_Un_S] = OpCodesEmit.Bne_Un_S;
            OpCodesMap[(int)Code.Bge_Un_S] = OpCodesEmit.Bge_Un_S;
            OpCodesMap[(int)Code.Bgt_Un_S] = OpCodesEmit.Bgt_Un_S;
            OpCodesMap[(int)Code.Ble_Un_S] = OpCodesEmit.Ble_Un_S;
            OpCodesMap[(int)Code.Blt_Un_S] = OpCodesEmit.Blt_Un_S;
            OpCodesMap[(int)Code.Br] = OpCodesEmit.Br;
            OpCodesMap[(int)Code.Brfalse] = OpCodesEmit.Brfalse;
            OpCodesMap[(int)Code.Brtrue] = OpCodesEmit.Brtrue;
            OpCodesMap[(int)Code.Beq] = OpCodesEmit.Beq;
            OpCodesMap[(int)Code.Bge] = OpCodesEmit.Bge;
            OpCodesMap[(int)Code.Bgt] = OpCodesEmit.Bgt;
            OpCodesMap[(int)Code.Ble] = OpCodesEmit.Ble;
            OpCodesMap[(int)Code.Blt] = OpCodesEmit.Blt;
            OpCodesMap[(int)Code.Bne_Un] = OpCodesEmit.Bne_Un;
            OpCodesMap[(int)Code.Bge_Un] = OpCodesEmit.Bge_Un;
            OpCodesMap[(int)Code.Bgt_Un] = OpCodesEmit.Bgt_Un;
            OpCodesMap[(int)Code.Ble_Un] = OpCodesEmit.Ble_Un;
            OpCodesMap[(int)Code.Blt_Un] = OpCodesEmit.Blt_Un;
            OpCodesMap[(int)Code.Switch] = OpCodesEmit.Switch;
            OpCodesMap[(int)Code.Ldind_I1] = OpCodesEmit.Ldind_I1;
            OpCodesMap[(int)Code.Ldind_U1] = OpCodesEmit.Ldind_U1;
            OpCodesMap[(int)Code.Ldind_I2] = OpCodesEmit.Ldind_I2;
            OpCodesMap[(int)Code.Ldind_U2] = OpCodesEmit.Ldind_U2;
            OpCodesMap[(int)Code.Ldind_I4] = OpCodesEmit.Ldind_I4;
            OpCodesMap[(int)Code.Ldind_U4] = OpCodesEmit.Ldind_U4;
            OpCodesMap[(int)Code.Ldind_I8] = OpCodesEmit.Ldind_I8;
            OpCodesMap[(int)Code.Ldind_I] = OpCodesEmit.Ldind_I;
            OpCodesMap[(int)Code.Ldind_R4] = OpCodesEmit.Ldind_R4;
            OpCodesMap[(int)Code.Ldind_R8] = OpCodesEmit.Ldind_R8;
            OpCodesMap[(int)Code.Ldind_Ref] = OpCodesEmit.Ldind_Ref;
            OpCodesMap[(int)Code.Stind_Ref] = OpCodesEmit.Stind_Ref;
            OpCodesMap[(int)Code.Stind_I1] = OpCodesEmit.Stind_I1;
            OpCodesMap[(int)Code.Stind_I2] = OpCodesEmit.Stind_I2;
            OpCodesMap[(int)Code.Stind_I4] = OpCodesEmit.Stind_I4;
            OpCodesMap[(int)Code.Stind_I8] = OpCodesEmit.Stind_I8;
            OpCodesMap[(int)Code.Stind_R4] = OpCodesEmit.Stind_R4;
            OpCodesMap[(int)Code.Stind_R8] = OpCodesEmit.Stind_R8;
            OpCodesMap[(int)Code.Add] = OpCodesEmit.Add;
            OpCodesMap[(int)Code.Sub] = OpCodesEmit.Sub;
            OpCodesMap[(int)Code.Mul] = OpCodesEmit.Mul;
            OpCodesMap[(int)Code.Div] = OpCodesEmit.Div;
            OpCodesMap[(int)Code.Div_Un] = OpCodesEmit.Div_Un;
            OpCodesMap[(int)Code.Rem] = OpCodesEmit.Rem;
            OpCodesMap[(int)Code.Rem_Un] = OpCodesEmit.Rem_Un;
            OpCodesMap[(int)Code.And] = OpCodesEmit.And;
            OpCodesMap[(int)Code.Or] = OpCodesEmit.Or;
            OpCodesMap[(int)Code.Xor] = OpCodesEmit.Xor;
            OpCodesMap[(int)Code.Shl] = OpCodesEmit.Shl;
            OpCodesMap[(int)Code.Shr] = OpCodesEmit.Shr;
            OpCodesMap[(int)Code.Shr_Un] = OpCodesEmit.Shr_Un;
            OpCodesMap[(int)Code.Neg] = OpCodesEmit.Neg;
            OpCodesMap[(int)Code.Not] = OpCodesEmit.Not;
            OpCodesMap[(int)Code.Conv_I1] = OpCodesEmit.Conv_I1;
            OpCodesMap[(int)Code.Conv_I2] = OpCodesEmit.Conv_I2;
            OpCodesMap[(int)Code.Conv_I4] = OpCodesEmit.Conv_I4;
            OpCodesMap[(int)Code.Conv_I8] = OpCodesEmit.Conv_I8;
            OpCodesMap[(int)Code.Conv_R4] = OpCodesEmit.Conv_R4;
            OpCodesMap[(int)Code.Conv_R8] = OpCodesEmit.Conv_R8;
            OpCodesMap[(int)Code.Conv_U4] = OpCodesEmit.Conv_U4;
            OpCodesMap[(int)Code.Conv_U8] = OpCodesEmit.Conv_U8;
            OpCodesMap[(int)Code.Callvirt] = OpCodesEmit.Callvirt;
            OpCodesMap[(int)Code.Cpobj] = OpCodesEmit.Cpobj;
            OpCodesMap[(int)Code.Ldobj] = OpCodesEmit.Ldobj;
            OpCodesMap[(int)Code.Ldstr] = OpCodesEmit.Ldstr;
            OpCodesMap[(int)Code.Newobj] = OpCodesEmit.Newobj;
            OpCodesMap[(int)Code.Castclass] = OpCodesEmit.Castclass;
            OpCodesMap[(int)Code.Isinst] = OpCodesEmit.Isinst;
            OpCodesMap[(int)Code.Conv_R_Un] = OpCodesEmit.Conv_R_Un;
            OpCodesMap[(int)Code.Unbox] = OpCodesEmit.Unbox;
            OpCodesMap[(int)Code.Throw] = OpCodesEmit.Throw;
            OpCodesMap[(int)Code.Ldfld] = OpCodesEmit.Ldfld;
            OpCodesMap[(int)Code.Ldflda] = OpCodesEmit.Ldflda;
            OpCodesMap[(int)Code.Stfld] = OpCodesEmit.Stfld;
            OpCodesMap[(int)Code.Ldsfld] = OpCodesEmit.Ldsfld;
            OpCodesMap[(int)Code.Ldsflda] = OpCodesEmit.Ldsflda;
            OpCodesMap[(int)Code.Stsfld] = OpCodesEmit.Stsfld;
            OpCodesMap[(int)Code.Stobj] = OpCodesEmit.Stobj;
            OpCodesMap[(int)Code.Conv_Ovf_I1_Un] = OpCodesEmit.Conv_Ovf_I1_Un;
            OpCodesMap[(int)Code.Conv_Ovf_I2_Un] = OpCodesEmit.Conv_Ovf_I2_Un;
            OpCodesMap[(int)Code.Conv_Ovf_I4_Un] = OpCodesEmit.Conv_Ovf_I4_Un;
            OpCodesMap[(int)Code.Conv_Ovf_I8_Un] = OpCodesEmit.Conv_Ovf_I8_Un;
            OpCodesMap[(int)Code.Conv_Ovf_U1_Un] = OpCodesEmit.Conv_Ovf_U1_Un;
            OpCodesMap[(int)Code.Conv_Ovf_U2_Un] = OpCodesEmit.Conv_Ovf_U2_Un;
            OpCodesMap[(int)Code.Conv_Ovf_U4_Un] = OpCodesEmit.Conv_Ovf_U4_Un;
            OpCodesMap[(int)Code.Conv_Ovf_U8_Un] = OpCodesEmit.Conv_Ovf_U8_Un;
            OpCodesMap[(int)Code.Conv_Ovf_I_Un] = OpCodesEmit.Conv_Ovf_I_Un;
            OpCodesMap[(int)Code.Conv_Ovf_U_Un] = OpCodesEmit.Conv_Ovf_U_Un;
            OpCodesMap[(int)Code.Box] = OpCodesEmit.Box;
            OpCodesMap[(int)Code.Newarr] = OpCodesEmit.Newarr;
            OpCodesMap[(int)Code.Ldlen] = OpCodesEmit.Ldlen;
            OpCodesMap[(int)Code.Ldelema] = OpCodesEmit.Ldelema;
            OpCodesMap[(int)Code.Ldelem_I1] = OpCodesEmit.Ldelem_I1;
            OpCodesMap[(int)Code.Ldelem_U1] = OpCodesEmit.Ldelem_U1;
            OpCodesMap[(int)Code.Ldelem_I2] = OpCodesEmit.Ldelem_I2;
            OpCodesMap[(int)Code.Ldelem_U2] = OpCodesEmit.Ldelem_U2;
            OpCodesMap[(int)Code.Ldelem_I4] = OpCodesEmit.Ldelem_I4;
            OpCodesMap[(int)Code.Ldelem_U4] = OpCodesEmit.Ldelem_U4;
            OpCodesMap[(int)Code.Ldelem_I8] = OpCodesEmit.Ldelem_I8;
            OpCodesMap[(int)Code.Ldelem_I] = OpCodesEmit.Ldelem_I;
            OpCodesMap[(int)Code.Ldelem_R4] = OpCodesEmit.Ldelem_R4;
            OpCodesMap[(int)Code.Ldelem_R8] = OpCodesEmit.Ldelem_R8;
            OpCodesMap[(int)Code.Ldelem_Ref] = OpCodesEmit.Ldelem_Ref;
            OpCodesMap[(int)Code.Stelem_I] = OpCodesEmit.Stelem_I;
            OpCodesMap[(int)Code.Stelem_I1] = OpCodesEmit.Stelem_I1;
            OpCodesMap[(int)Code.Stelem_I2] = OpCodesEmit.Stelem_I2;
            OpCodesMap[(int)Code.Stelem_I4] = OpCodesEmit.Stelem_I4;
            OpCodesMap[(int)Code.Stelem_I8] = OpCodesEmit.Stelem_I8;
            OpCodesMap[(int)Code.Stelem_R4] = OpCodesEmit.Stelem_R4;
            OpCodesMap[(int)Code.Stelem_R8] = OpCodesEmit.Stelem_R8;
            OpCodesMap[(int)Code.Stelem_Ref] = OpCodesEmit.Stelem_Ref;
            OpCodesMap[(int)Code.Ldelem] = OpCodesEmit.Ldelem;
            OpCodesMap[(int)Code.Stelem] = OpCodesEmit.Stelem;
            OpCodesMap[(int)Code.Unbox_Any] = OpCodesEmit.Unbox_Any;
            OpCodesMap[(int)Code.Conv_Ovf_I1] = OpCodesEmit.Conv_Ovf_I1;
            OpCodesMap[(int)Code.Conv_Ovf_U1] = OpCodesEmit.Conv_Ovf_U1;
            OpCodesMap[(int)Code.Conv_Ovf_I2] = OpCodesEmit.Conv_Ovf_I2;
            OpCodesMap[(int)Code.Conv_Ovf_U2] = OpCodesEmit.Conv_Ovf_U2;
            OpCodesMap[(int)Code.Conv_Ovf_I4] = OpCodesEmit.Conv_Ovf_I4;
            OpCodesMap[(int)Code.Conv_Ovf_U4] = OpCodesEmit.Conv_Ovf_U4;
            OpCodesMap[(int)Code.Conv_Ovf_I8] = OpCodesEmit.Conv_Ovf_I8;
            OpCodesMap[(int)Code.Conv_Ovf_U8] = OpCodesEmit.Conv_Ovf_U8;
            OpCodesMap[(int)Code.Refanyval] = OpCodesEmit.Refanyval;
            OpCodesMap[(int)Code.Ckfinite] = OpCodesEmit.Ckfinite;
            OpCodesMap[(int)Code.Mkrefany] = OpCodesEmit.Mkrefany;
            OpCodesMap[(int)Code.Ldtoken] = OpCodesEmit.Ldtoken;
            OpCodesMap[(int)Code.Conv_U2] = OpCodesEmit.Conv_U2;
            OpCodesMap[(int)Code.Conv_U1] = OpCodesEmit.Conv_U1;
            OpCodesMap[(int)Code.Conv_I] = OpCodesEmit.Conv_I;
            OpCodesMap[(int)Code.Conv_Ovf_I] = OpCodesEmit.Conv_Ovf_I;
            OpCodesMap[(int)Code.Conv_Ovf_U] = OpCodesEmit.Conv_Ovf_U;
            OpCodesMap[(int)Code.Add_Ovf] = OpCodesEmit.Add_Ovf;
            OpCodesMap[(int)Code.Add_Ovf_Un] = OpCodesEmit.Add_Ovf_Un;
            OpCodesMap[(int)Code.Mul_Ovf] = OpCodesEmit.Mul_Ovf;
            OpCodesMap[(int)Code.Mul_Ovf_Un] = OpCodesEmit.Mul_Ovf_Un;
            OpCodesMap[(int)Code.Sub_Ovf] = OpCodesEmit.Sub_Ovf;
            OpCodesMap[(int)Code.Sub_Ovf_Un] = OpCodesEmit.Sub_Ovf_Un;
            OpCodesMap[(int)Code.Endfinally] = OpCodesEmit.Endfinally;
            OpCodesMap[(int)Code.Leave] = OpCodesEmit.Leave;
            OpCodesMap[(int)Code.Leave_S] = OpCodesEmit.Leave_S;
            OpCodesMap[(int)Code.Stind_I] = OpCodesEmit.Stind_I;
            OpCodesMap[(int)Code.Conv_U] = OpCodesEmit.Conv_U;
            OpCodesMap[(int)Code.Arglist] = OpCodesEmit.Arglist;
            OpCodesMap[(int)Code.Ceq] = OpCodesEmit.Ceq;
            OpCodesMap[(int)Code.Cgt] = OpCodesEmit.Cgt;
            OpCodesMap[(int)Code.Cgt_Un] = OpCodesEmit.Cgt_Un;
            OpCodesMap[(int)Code.Clt] = OpCodesEmit.Clt;
            OpCodesMap[(int)Code.Clt_Un] = OpCodesEmit.Clt_Un;
            OpCodesMap[(int)Code.Ldftn] = OpCodesEmit.Ldftn;
            OpCodesMap[(int)Code.Ldvirtftn] = OpCodesEmit.Ldvirtftn;
            OpCodesMap[(int)Code.Ldarg] = OpCodesEmit.Ldarg;
            OpCodesMap[(int)Code.Ldarga] = OpCodesEmit.Ldarga;
            OpCodesMap[(int)Code.Starg] = OpCodesEmit.Starg;
            OpCodesMap[(int)Code.Ldloc] = OpCodesEmit.Ldloc;
            OpCodesMap[(int)Code.Ldloca] = OpCodesEmit.Ldloca;
            OpCodesMap[(int)Code.Stloc] = OpCodesEmit.Stloc;
            OpCodesMap[(int)Code.Localloc] = OpCodesEmit.Localloc;
            OpCodesMap[(int)Code.Endfilter] = OpCodesEmit.Endfilter;
            OpCodesMap[(int)Code.Unaligned] = OpCodesEmit.Unaligned;
            OpCodesMap[(int)Code.Volatile] = OpCodesEmit.Volatile;
            OpCodesMap[(int)Code.Tail] = OpCodesEmit.Tailcall;
            OpCodesMap[(int)Code.Initobj] = OpCodesEmit.Initobj;
            OpCodesMap[(int)Code.Constrained] = OpCodesEmit.Constrained;
            OpCodesMap[(int)Code.Cpblk] = OpCodesEmit.Cpblk;
            OpCodesMap[(int)Code.Initblk] = OpCodesEmit.Initblk;

            // OpCodesMap[(int)Code.No] = OpCodesEmit.No;
            OpCodesMap[(int)Code.Rethrow] = OpCodesEmit.Rethrow;
            OpCodesMap[(int)Code.Sizeof] = OpCodesEmit.Sizeof;
            OpCodesMap[(int)Code.Refanytype] = OpCodesEmit.Refanytype;
            OpCodesMap[(int)Code.Readonly] = OpCodesEmit.Readonly;
        }

        /// <summary>
        /// </summary>
        public IlReader()
        {
            this.CompileInMemory = true;

            this.lazyTypes = new Lazy<IEnumerable<IType>>(() => this.ReadTypes().ToList());
            this.lazyAllTypes = new Lazy<IEnumerable<IType>>(() => this.ReadTypes(true).ToList());
            this.lasyModule = new Lazy<IModule>(() => this.Types().First(t => t.IsModule).Module);
            this.lasySystem = new Lazy<SystemTypes>(() => new SystemTypes(this.Module));
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <param name="args">
        /// </param>
        public IlReader(string[] source, string[] args)
            : this()
        {
            this.Sources = source;
            this.FirstSource = this.Sources.First();

            var coreLibPathArg = args != null ? args.FirstOrDefault(a => a.StartsWith("corelib:")) : null;
            this.CoreLibPath = coreLibPathArg != null ? coreLibPathArg.Substring("corelib:".Length) : null;
            this.UsingRoslyn = args == null || !args.Any(a => a == "roslyn-");
            this.isDll = this.FirstSource.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase);
            this.DefaultDllLocations = this.isDll ? Path.GetDirectoryName(Path.GetFullPath(this.FirstSource)) : null;
            this.DebugInfo = args != null && args.Contains("debug");
            if (!this.isDll)
            {
                this.SourceFilePath = Path.GetFullPath(this.FirstSource);
                if (this.DefaultDllLocations == null && !string.IsNullOrWhiteSpace(this.CoreLibPath))
                {
                    this.DefaultDllLocations = Path.GetDirectoryName(this.CoreLibPath);
                }
            }

            var refs = args != null ? args.FirstOrDefault(a => a.StartsWith("ref:")) : null;
            var refsValue = refs != null ? refs.Substring("ref:".Length) : null;
            this.ReferencesList = (refsValue ?? string.Empty).Split(
                new[] { ';' },
                StringSplitOptions.RemoveEmptyEntries);

            this.CompactMode = args != null && args.Contains("compact");
        }

        public bool DoNotAddInternalArrays { get; set; }

        /// <summary>
        /// </summary>
        public IModule Module
        {
            get
            {
                return this.lasyModule.Value;
            }
        }

        /// <summary>
        /// </summary>
        public SystemTypes System 
        {
            get
            {
                return this.lasySystem.Value;
            }
        }

        /// <summary>
        /// </summary>
        public string AssemblyQualifiedName
        {
            get { return this.Assembly.Assembly.Identity.Name; }
        }

        /// <summary>
        /// </summary>
        public string CoreLibPath { get; set; }

        public bool DebugInfo { get; private set; }

        /// <summary>
        /// </summary>
        public string[] ReferencesList { get; set; }

        /// <summary>
        /// </summary>
        public string DefaultDllLocations { get; private set; }

        /// <summary>
        /// </summary>
        public string DllFilePath { get; private set; }

        /// <summary>
        /// </summary>
        public bool CompactMode { get; private set; }

        /// <summary>
        /// </summary>
        public bool IsCoreLib
        {
            get { return !this.Assembly.Assembly.AssemblyReferences.Any(); }
        }

        /// <summary>
        /// </summary>
        public string ModuleName
        {
            get { return this.Assembly.ManifestModule.Name; }
        }

        /// <summary>
        /// </summary>
        public string PdbFilePath { get; private set; }

        /// <summary>
        /// </summary>
        public string SourceFilePath { get; private set; }

        /// <summary>
        /// </summary>
        public ICodeWriter CodeWriter { get; set; }

        /// <summary>
        /// </summary>
        protected bool UsingRoslyn { get; set; }

        /// <summary>
        /// </summary>
        protected bool CompileInMemory { get; set; }

        /// <summary>
        /// </summary>
        protected AssemblyMetadata Assembly { get; private set; }

        /// <summary>
        /// </summary>
        protected string FirstSource { get; private set; }

        /// <summary>
        /// </summary>
        protected string[] Sources { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IConstructor> Constructors(IType type, ICodeWriter codeWriter)
        {
            return Constructors(type, DefaultFlags, codeWriter);
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <param name="flags">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IConstructor> Constructors(IType type, BindingFlags flags, ICodeWriter codeWriter)
        {
            Debug.Assert(type != null);

            foreach (var constructor in type.GetConstructors(flags).Where(m => !m.IsGenericMethodDefinition))
            {
                yield return constructor;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IField> Fields(IType type, ICodeWriter codeWriter)
        {
            return Fields(type, DefaultFlags, codeWriter);
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <param name="flags">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IField> Fields(IType type, BindingFlags flags, ICodeWriter codeWriter)
        {
            Debug.Assert(type != null);

            foreach (var field in type.GetFields(flags))
            {
                yield return field;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IMethod> Methods(IType type, ICodeWriter codeWriter, bool excludeSpecializations = false, bool structObjectAdaptersOnly = false)
        {
            return Methods(type, DefaultFlags, codeWriter, excludeSpecializations, structObjectAdaptersOnly);
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <param name="flags">
        /// </param>
        /// <returns>
        /// </returns>
        public static IEnumerable<IMethod> Methods(IType type, BindingFlags flags, ICodeWriter codeWriter, bool excludeSpecializations = false, bool structObjectAdaptersOnly = false)
        {
            Debug.Assert(type != null);

            var normal = type.ToNormal();
            if (!structObjectAdaptersOnly)
            {
                if (!excludeSpecializations)
                {
                    foreach (var method in type.GetMethods(flags).Where(m => !m.IsGenericMethodDefinition))
                    {
                        yield return method;
                    }
                }
                else
                {
                    foreach (var method in type.GetMethods(flags))
                    {
                        yield return method;
                    }
                }

                if (type.IsPrivateImplementationDetails)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="fullTypeName">
        /// </param>
        /// <returns>
        /// </returns>
        public IType ResolveType(string fullTypeName, IGenericContext genericContext = null)
        {
            if (genericContext != null && !genericContext.IsEmpty)
            {
                return this.Module.ResolveType(fullTypeName, genericContext);
            }

            IType result;
            if (this.ResolvedTypes.TryGetValue(fullTypeName, out result))
            {
                return result;
            }

            result = this.Module.ResolveType(fullTypeName, null);
            this.ResolvedTypes[result.FullName] = result;
            return result;
        }

        public IEnumerable<string> References()
        {
            var added = new HashSet<AssemblyIdentity>();
            return this.AllReferencesHelper(this.Assembly, added, true).Distinct();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public IEnumerable<string> AllReferences()
        {
            var added = new HashSet<AssemblyIdentity>();
            return this.AllReferencesHelper(this.Assembly, added).Distinct();
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public IEnumerable<IType> AllTypes()
        {
            return this.lazyAllTypes.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public IEnumerable<IType> CompileSourceWithRoslyn(string assemblyName, params string[] source)
        {
            var nameDll = assemblyName ?? "__unrestricted__.dll";

            var syntaxTrees =
                source.Select(s => CSharpSyntaxTree.ParseText(s, new CSharpParseOptions(LanguageVersion.Experimental)));

            var coreLibRefAssembly = string.IsNullOrWhiteSpace(this.CoreLibPath)
                                         ? new MetadataImageReference(new FileStream(typeof(int).Assembly.Location, FileMode.Open, FileAccess.Read))
                                         : new MetadataImageReference(new FileStream(this.CoreLibPath, FileMode.Open, FileAccess.Read));

            var options =
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true)
                                                                                 .WithOptimizations(!this.DebugInfo)
                                                                                 .WithRuntimeMetadataVersion("4.5");

            var compilation = CSharpCompilation.Create(nameDll, syntaxTrees, new[] { coreLibRefAssembly }, options);

            var dllStream = new MemoryStream();
            var result = compilation.Emit(peStream: dllStream);
            if (result.Diagnostics.Length > 0)
            {
                foreach (var diagnostic in result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
                {
                    throw new InvalidOperationException(diagnostic.GetMessage());
                }
            }

            dllStream.Flush();
            dllStream.Position = 0;

            // Successful Compile
            var fromImageStream = AssemblyMetadata.CreateFromImageStream(dllStream);
            var compileSourceWithRoslyn = this.LoadAssemblySymbol(fromImageStream, true);
            return this.ReadTypes(compileSourceWithRoslyn);
        }

        /// <summary>
        /// </summary>
        public void Load()
        {
            this.Assembly = !this.isDll
                                ? this.UsingRoslyn
                                    ? this.CompileInMemory
                                        ? this.CompileWithRoslynInMemory(this.Sources)
                                        : this.CompileWithRoslyn(this.Sources)
                                    : this.Compile(this.Sources)
                                : AssemblyMetadata.CreateFromImageStream(new FileStream(this.FirstSource, FileMode.Open, FileAccess.Read));

            if (this.isDll)
            {
                this.DllFilePath = this.FirstSource;
                this.PdbFilePath = Path.ChangeExtension(this.FirstSource, "pdb");
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="method">
        /// </param>
        /// <param name="genericContext">
        /// </param>
        /// <param name="stackCall">
        /// </param>
        /// <returns>
        /// </returns>
        public IEnumerable<OpCodePart> OpCodes(IMethod method, IGenericContext genericContext, Queue<IMethod> stackCall = null)
        {
            if (method == null)
            {
                yield break;
            }

            foreach (var opCode in this.OpCodes(method.GetMethodBody(genericContext), method.Module, genericContext, stackCall))
            {
                yield return opCode;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="methodBody">
        /// </param>
        /// <param name="module">
        /// </param>
        /// <param name="genericContext">
        /// </param>
        /// <param name="stackCall">
        /// </param>
        /// <returns>
        /// </returns>
        public IEnumerable<OpCodePart> OpCodes(IMethodBody methodBody, IModule module, IGenericContext genericContext, Queue<IMethod> stackCall)
        {
            // TODO: remove all calls to disover used types into BaseWriter function Discover all forward declarations

            if (!methodBody.HasBody)
            {
                yield break;
            }

            var extended = false;
            var startAddress = 0;
            var currentAddress = 0;
            var ilAsByteArray = methodBody.GetILAsByteArray();
            var enumerator = ilAsByteArray.GetEnumerator();
            while (enumerator.MoveNext())
            {
                var @byte = (byte)enumerator.Current;
                if (@byte == 0xFE)
                {
                    extended = true;
                    continue;
                }

                var code = (Code)(extended ? (@byte + 0xE1) : @byte);
                extended = false;

                var opCode = OpCodesMap[(int)code];

                startAddress = currentAddress;
                currentAddress += opCode.Size;

                switch (code)
                {
                    case Code.Br_S:
                    case Code.Beq_S:
                    case Code.Brtrue_S:
                    case Code.Brfalse_S:
                    case Code.Blt_S:
                    case Code.Blt_Un_S:
                    case Code.Bgt_S:
                    case Code.Bgt_Un_S:
                    case Code.Bge_S:
                    case Code.Bge_Un_S:
                    case Code.Ble_S:
                    case Code.Ble_Un_S:
                    case Code.Bne_Un_S:
                    case Code.Ldc_I4_S:
                    case Code.Ldloc_S:
                    case Code.Ldloca_S:
                    case Code.Stloc_S:
                    case Code.Leave_S:
                    case Code.Ldarg_S:
                    case Code.Starg_S:
                    case Code.Ldarga_S:

                        // read token, next 
                        var token = ReadInt32ShortForm(enumerator, ref currentAddress);
                        var @int32 = token;
                        yield return new OpCodeInt32Part(opCode, startAddress, currentAddress, @int32);
                        continue;
                    case Code.Br:
                    case Code.Beq:
                    case Code.Brtrue:
                    case Code.Brfalse:
                    case Code.Blt:
                    case Code.Blt_Un:
                    case Code.Bgt:
                    case Code.Bgt_Un:
                    case Code.Bge:
                    case Code.Bge_Un:
                    case Code.Ble:
                    case Code.Ble_Un:
                    case Code.Bne_Un:
                    case Code.Ldc_I4:
                    case Code.Ldloc:
                    case Code.Stloc:
                    case Code.Leave:
                    case Code.Ldarg:
                    case Code.Starg:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        @int32 = token;
                        yield return new OpCodeInt32Part(opCode, startAddress, currentAddress, @int32);
                        continue;
                    case Code.Ldc_I8:

                        // read token, next 
                        var bytes = ReadBytes(enumerator, 8, ref currentAddress);
                        var @int64 = BitConverter.ToInt64(bytes, 0);
                        yield return new OpCodeInt64Part(opCode, startAddress, currentAddress, @int64);
                        continue;
                    case Code.Ldc_R4:

                        // read token, next 
                        bytes = ReadBytes(enumerator, 4, ref currentAddress);
                        var @single = BitConverter.ToSingle(bytes, 0);
                        yield return new OpCodeSinglePart(opCode, startAddress, currentAddress, @single);
                        continue;
                    case Code.Ldc_R8:

                        // read token, next 
                        bytes = ReadBytes(enumerator, 8, ref currentAddress);
                        var @double = BitConverter.ToDouble(bytes, 0);
                        yield return new OpCodeDoublePart(opCode, startAddress, currentAddress, @double);
                        continue;
                    case Code.Ldstr:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        var @string = module.ResolveString(token);
                        yield return new OpCodeStringPart(opCode, startAddress, currentAddress, new KeyValuePair<int, string>(token, @string));
                        continue;
                    case Code.Newobj:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        var resolveMember = module.ResolveMember(token, genericContext);
                        var constructor = resolveMember as IConstructor;
                        Debug.Assert(constructor != null, "Not Supported Newobj");
                        if (constructor != null)
                        {
                            yield return new OpCodeConstructorInfoPart(opCode, startAddress, currentAddress, constructor);
                        }

                        continue;
                    case Code.Call:
                    case Code.Callvirt:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        var method = module.ResolveMethod(token, genericContext);
                        yield return new OpCodeMethodInfoPart(opCode, startAddress, currentAddress, method);
                        continue;

                    case Code.Ldftn:
                    case Code.Ldvirtftn:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        method = module.ResolveMethod(token, genericContext);
                        yield return new OpCodeMethodInfoPart(opCode, startAddress, currentAddress, method);
                        continue;
                    case Code.Stfld:
                    case Code.Stsfld:
                    case Code.Ldfld:
                    case Code.Ldflda:
                    case Code.Ldsfld:
                    case Code.Ldsflda:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        var field = module.ResolveField(token, genericContext);
                        Debug.Assert(field != null);
                        yield return new OpCodeFieldInfoPart(opCode, startAddress, currentAddress, field);
                        continue;
                    case Code.Ldtoken: // can it be anything?

                        token = ReadInt32(enumerator, ref currentAddress);

                        var resolvedToken = module.ResolveToken(token, genericContext);

                        var typeToken = resolvedToken as IType;
                        if (typeToken != null)
                        {
                            yield return new OpCodeTypePart(opCode, startAddress, currentAddress, typeToken);
                            continue;
                        }

                        var fieldMember = resolvedToken as IField;
                        if (fieldMember != null)
                        {
                            // special case
                            yield return new OpCodeFieldInfoPart(opCode, startAddress, currentAddress, fieldMember);
                            continue;
                        }

                        var methodMember = resolvedToken as IMethod;
                        if (methodMember != null)
                        {
                            yield return new OpCodeMethodInfoPart(opCode, startAddress, currentAddress, methodMember);
                            continue;
                        }

                        var fullyDefinedReference = resolvedToken as FullyDefinedReference;
                        if (fullyDefinedReference != null)
                        {
                            yield return new OpCodeFullyDefinedReferencePart(opCode, startAddress, currentAddress, fullyDefinedReference);
                            continue;
                        }

                        yield return new OpCodeInt32Part(opCode, startAddress, currentAddress, token);
                        continue;
                    case Code.Newarr:
                    case Code.Ldelem:
                    case Code.Stelem:
                    case Code.Ldelema:
                    case Code.Box:
                    case Code.Unbox:
                    case Code.Unbox_Any:
                    case Code.Castclass:
                    case Code.Initobj:
                    case Code.Isinst:
                    case Code.Ldobj:
                    case Code.Stobj:
                    case Code.Cpobj:
                    case Code.Constrained:
                    case Code.Sizeof:
                    case Code.Mkrefany:
                    case Code.Refanyval:

                        // read token, next 
                        token = ReadInt32(enumerator, ref currentAddress);
                        var type = module.ResolveType(token, genericContext);
                        yield return new OpCodeTypePart(opCode, startAddress, currentAddress, type);
                        continue;
                    case Code.Switch:

                        var ints = new List<int>();
                        var count = ReadInt32(enumerator, ref currentAddress);
                        for (var i = 0; i < count; i++)
                        {
                            ints.Add(ReadInt32(enumerator, ref currentAddress));
                        }

                        yield return new OpCodeLabelsPart(opCode, startAddress, currentAddress, ints.ToArray());
                        continue;
                    case Code.Ldlen:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_I:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_I1:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_I2:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_I4:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_U1:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_U2:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_U4:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_R4:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Ldelem_R8:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_I:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_I1:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_I2:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_I4:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_R4:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    case Code.Stelem_R8:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;

                    default:
                        yield return new OpCodePart(opCode, startAddress, currentAddress);
                        continue;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <returns>
        /// </returns>
        public IEnumerable<IType> Types()
        {
            return this.lazyTypes.Value;
        }

        /// <summary>
        /// </summary>
        /// <param name="type">
        /// </param>
        /// <returns>
        /// </returns>
        private static IEnumerable<IType> EnumAllNestedTypes(IType type)
        {
            foreach (var nestedType in type.GetNestedTypes())
            {
                yield return nestedType;
                foreach (var subNestedType in EnumAllNestedTypes(nestedType))
                {
                    yield return subNestedType;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <returns>
        /// </returns>
        private static IEnumerable<NamespaceSymbol> GetAllNamespaces(NamespaceSymbol source)
        {
            yield return source;
            foreach (var namespaceSymbolSub in
                source.GetNamespaceMembers().SelectMany(namespaceSymbolSub => GetAllNamespaces(namespaceSymbolSub)))
            {
                yield return namespaceSymbolSub;
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyMetadata">
        /// </param>
        /// <returns>
        /// </returns>
        private static PEAssemblySymbol GetAssemblySymbol(AssemblyMetadata assemblyMetadata)
        {
            return new PEAssemblySymbol(assemblyMetadata.Assembly, DocumentationProvider.Default, isLinked: false, importOptions: MetadataImportOptions.All);
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <param name="size">
        /// </param>
        /// <param name="shift">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// </exception>
        private static byte[] ReadBytes(IEnumerator source, int size, ref int shift)
        {
            var b = new byte[size];
            for (var i = 0; i < size; i++)
            {
                if (source.MoveNext())
                {
                    b[i] = (byte)source.Current;
                }
                else
                {
                    throw new InvalidOperationException("Could not read bytes");
                }
            }

            shift += size;

            return b;
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <param name="shift">
        /// </param>
        /// <returns>
        /// </returns>
        private static int ReadInt32(IEnumerator source, ref int shift)
        {
            return BitConverter.ToInt32(ReadBytes(source, 4, ref shift), 0);
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <param name="shift">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// </exception>
        private static int ReadInt32ShortForm(IEnumerator source, ref int shift)
        {
            if (source.MoveNext())
            {
                shift++;
                return (byte)source.Current;
            }

            throw new InvalidOperationException("Could not read a short for of int32");
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyMetadata">
        /// </param>
        /// <returns>
        /// </returns>
        private IEnumerable<string> AllReferencesHelper(AssemblyMetadata assemblyMetadata, HashSet<AssemblyIdentity> processed, bool excludingCurrent = false)
        {
            if (!processed.Add(assemblyMetadata.Assembly.Identity))
            {
                yield break;
            }

            if (!excludingCurrent)
            {
                yield return assemblyMetadata.Assembly.Identity.Name;
            }

            foreach (var reference in this.LoadReferences(assemblyMetadata).Names)
            {
                foreach (var referenceName in this.AllReferencesHelper(this.GetAssemblyMetadata(reference), processed))
                {
                    yield return referenceName;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="sources">
        /// </param>
        /// <returns>
        /// </returns>
        /// <exception cref="Exception">
        /// </exception>
        private AssemblyMetadata Compile(string[] sources)
        {
            var language = "CSharp";
            var codeProvider = CodeDomProvider.IsDefinedLanguage(language) ? CodeDomProvider.CreateProvider(language) : null;
            if (codeProvider == null)
            {
                throw new NotSupportedException(string.Format("language '{0}' is not supported", language));
            }

            var path = Path.GetTempPath();
            var filename = Path.GetRandomFileName();

            var outDll = Path.Combine(path, string.Concat(filename, ".dll"));
            var outPdb = Path.Combine(path, string.Concat(filename, ".pdb"));

            var parameters = CodeDomProvider.GetCompilerInfo(language).CreateDefaultCompilerParameters();
            parameters.GenerateExecutable = false;
            parameters.GenerateInMemory = false;
            parameters.CompilerOptions =
                string.Concat(
                    string.Format("/optimize{0} /unsafe+{1}", this.DebugInfo ? "-" : "+", this.DebugInfo ? " /debug:full" : string.Empty),
                    string.IsNullOrWhiteSpace(this.CoreLibPath) ? string.Empty : string.Format(" /nostdlib+ /r:\"{0}\"", this.CoreLibPath));
            parameters.OutputAssembly = outDll;

            var results = codeProvider.CompileAssemblyFromFile(parameters, sources);

            if (results.Errors.Count > 0)
            {
                Console.WriteLine(@"Errors/Warnings:");
                foreach (CompilerError compilerError in results.Errors)
                {
                    Console.WriteLine(compilerError);
                }
            }

            this.DllFilePath = outDll;
            this.PdbFilePath = outPdb;

            // Successful Compile
            return AssemblyMetadata.CreateFromImageStream(new FileStream(results.PathToAssembly, FileMode.Open, FileAccess.Read));
        }

        /// <summary>
        /// </summary>
        /// <param name="source">
        /// </param>
        /// <returns>
        /// </returns>
        private AssemblyMetadata CompileWithRoslyn(string[] source)
        {
            var srcFileName = Path.GetFileNameWithoutExtension(this.FirstSource);
            var baseName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var nameDll = srcFileName + "_" + baseName + ".dll";
            var namePdb = srcFileName + "_" + baseName + ".pdb";
            var outDll = Path.Combine(Path.GetTempPath(), nameDll);
            var outPdb = Path.Combine(Path.GetTempPath(), namePdb);

            var syntaxTrees =
                source.Select(s => CSharpSyntaxTree.ParseText(new StreamReader(s).ReadToEnd(), new CSharpParseOptions(LanguageVersion.Experimental)));

            var assemblies = new List<MetadataImageReference>();

            this.LoadReferencesForCompiling(assemblies);

            var options =
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true)
                                                                                 .WithOptimizations(!this.DebugInfo)
                                                                                 .WithRuntimeMetadataVersion("4.5");

            var compilation = CSharpCompilation.Create(nameDll, syntaxTrees, assemblies.ToArray(), options);

            using (var dllStream = new FileStream(outDll, FileMode.OpenOrCreate))
            {
                using (var pdbStream = new FileStream(outPdb, FileMode.OpenOrCreate))
                {
                    var result = compilation.Emit(peStream: dllStream, pdbFilePath: outPdb, pdbStream: pdbStream);

                    if (result.Diagnostics.Length > 0)
                    {
                        Console.WriteLine(@"Errors/Warnings:");
                        foreach (var diagnostic in result.Diagnostics)
                        {
                            Console.WriteLine(diagnostic);
                        }
                    }
                }
            }

            this.DllFilePath = outDll;
            this.PdbFilePath = outPdb;

            // Successful Compile
            return AssemblyMetadata.CreateFromImageStream(new FileStream(outDll, FileMode.Open, FileAccess.Read));
        }

        private AssemblyMetadata CompileWithRoslynInMemory(string[] source)
        {
            var srcFileName = Path.GetFileNameWithoutExtension(this.FirstSource);
            var baseName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
            var nameDll = srcFileName + "_" + baseName + ".dll";
            var namePdb = srcFileName + "_" + baseName + ".pdb";
            var outDll = Path.Combine(Path.GetTempPath(), nameDll);
            var outPdb = Path.Combine(Path.GetTempPath(), namePdb);

            var syntaxTrees =
                source.Select(s => CSharpSyntaxTree.ParseText(new StreamReader(s).ReadToEnd(), new CSharpParseOptions(LanguageVersion.Experimental)));

            var assemblies = new List<MetadataImageReference>();

            this.LoadReferencesForCompiling(assemblies);

            var options =
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true)
                                                                                 .WithOptimizations(!this.DebugInfo)
                                                                                 .WithRuntimeMetadataVersion("4.5");

            var compilation = CSharpCompilation.Create(nameDll, syntaxTrees, assemblies.ToArray(), options);

            var dllStream = new MemoryStream();
            var pdbStream = new MemoryStream();

            var result = compilation.Emit(peStream: dllStream, pdbFilePath: outPdb, pdbStream: pdbStream);

            if (result.Diagnostics.Length > 0)
            {
                Console.WriteLine(@"Errors/Warnings:");
                foreach (var diagnostic in result.Diagnostics)
                {
                    Console.WriteLine(diagnostic);
                }
            }

            dllStream.Flush();
            dllStream.Position = 0;

            pdbStream.Flush();
            pdbStream.Position = 0;

            // Successful Compile
            return AssemblyMetadata.CreateFromImageStream(dllStream);
        }

        private void LoadReferencesForCompiling(List<MetadataImageReference> assemblies)
        {
            var added = new HashSet<AssemblyIdentity>();

            if (!string.IsNullOrWhiteSpace(this.CoreLibPath))
            {
                AddAsseblyReference(assemblies, added, this.CoreLibPath);
            }
            else
            {
                AddAsseblyReference(assemblies, added, typeof(int).Assembly.Location);
            }

            if (this.ReferencesList != null)
            {
                foreach (var refItem in this.ReferencesList)
                {
                    if (File.Exists(refItem))
                    {
                        AddAsseblyReference(assemblies, added, refItem);
                    }
                    else
                    {
                        this.AddAsseblyReference(assemblies, added, new AssemblyIdentity(refItem));
                    }
                }
            }
        }

        private void AddAsseblyReference(List<MetadataImageReference> assemblies, HashSet<AssemblyIdentity> added, AssemblyIdentity assemblyIdentity)
        {
            var resolvedFilePath = this.ResolveReferencePath(assemblyIdentity);
            if (resolvedFilePath == null)
            {
                return;
            }

            var metadata = AssemblyMetadata.CreateFromImageStream(new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read));
            if (added.Add(metadata.Assembly.Identity))
            {
                var metadataImageReference = new MetadataImageReference(new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read));
                assemblies.Add(metadataImageReference);

                // process nested
                foreach (var refAssemblyIdentity in metadata.Assembly.AssemblyReferences)
                {
                    AddAsseblyReference(assemblies, added, refAssemblyIdentity);
                }
            }
        }

        private void AddAsseblyReference(List<MetadataImageReference> assemblies, HashSet<AssemblyIdentity> added, string resolvedFilePath)
        {
            var metadata = AssemblyMetadata.CreateFromImageStream(new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read));
            var metadataImageReference = new MetadataImageReference(new FileStream(resolvedFilePath, FileMode.Open, FileAccess.Read));

            if (added.Add(metadata.Assembly.Identity))
            {
                assemblies.Add(metadataImageReference);

                // process nested
                foreach (var refAssemblyIdentity in metadata.Assembly.AssemblyReferences)
                {
                    AddAsseblyReference(assemblies, added, refAssemblyIdentity);
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblySymbol">
        /// </param>
        /// <returns>
        /// </returns>
        private IEnumerable<IType> EnumAllTypes(PEAssemblySymbol assemblySymbol)
        {
            Debug.Assert(assemblySymbol != null, "missing assembly");

            foreach (var module in assemblySymbol.Modules)
            {
                var peModuleSymbol = module as PEModuleSymbol;
                foreach (var metadataTypeAdapter in
                    from symbol in GetAllNamespaces(peModuleSymbol.GlobalNamespace).SelectMany(n => n.GetTypeMembers()) select new MetadataTypeAdapter(symbol))
                {
                    yield return metadataTypeAdapter;
                    foreach (var nestedType in EnumAllNestedTypes(metadataTypeAdapter))
                    {
                        yield return nestedType;
                    }
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyIdentity">
        /// </param>
        /// <returns>
        /// </returns>
        private AssemblyMetadata GetAssemblyMetadata(AssemblyIdentity assemblyIdentity)
        {
            var resolveReferencePath = this.ResolveReferencePath(assemblyIdentity);
            if (string.IsNullOrWhiteSpace(resolveReferencePath))
            {
                return null;
            }

            return AssemblyMetadata.CreateFromImageStream(new FileStream(resolveReferencePath, FileMode.Open, FileAccess.Read));
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <returns>
        /// </returns>
        private AssemblySymbol LoadAssemblySymbol(AssemblyIdentity identity)
        {
            AssemblySymbol symbol;
            if (this.cache.TryGetValue(identity, out symbol))
            {
                return symbol;
            }

            var assemblyMetadata = this.GetAssemblyMetadata(identity);
            if (assemblyMetadata != null)
            {
                return this.LoadAssemblySymbol(assemblyMetadata);
            }

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyMetadata">
        /// </param>
        /// <returns>
        /// </returns>
        private AssemblySymbol LoadAssemblySymbol(AssemblyMetadata assemblyMetadata, bool noCache = false)
        {
            AssemblySymbol symbol;
            if (!noCache && this.cache.TryGetValue(assemblyMetadata.Assembly.Identity, out symbol))
            {
                return symbol;
            }

            var assemblySymbol = GetAssemblySymbol(assemblyMetadata);

            this.cache[assemblyMetadata.Assembly.Identity] = assemblySymbol;
            this.unifiedAssemblies.Add(new UnifiedAssembly<AssemblySymbol>(assemblySymbol, assemblyMetadata.Assembly.Identity));

            var moduleReferences = this.LoadReferences(assemblyMetadata);
            foreach (var module in assemblySymbol.Modules)
            {
                module.SetReferences(moduleReferences);
            }

            this.SetCorLib(assemblySymbol);

            return assemblySymbol;
        }

        /// <summary>
        /// </summary>
        /// <param name="identity">
        /// </param>
        /// <returns>
        /// </returns>
        private AssemblySymbol LoadAssemblySymbolOrMissingAssemblySymbol(AssemblyIdentity identity)
        {
            AssemblySymbol symbol;
            if (this.cache.TryGetValue(identity, out symbol))
            {
                return symbol;
            }

            var peAssemblySymbol = this.LoadAssemblySymbol(identity);
            if (peAssemblySymbol != null)
            {
                return peAssemblySymbol;
            }

            return new MissingAssemblySymbol(identity);
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyMetadata">
        /// </param>
        /// <returns>
        /// </returns>
        private ModuleReferences<AssemblySymbol> LoadReferences(AssemblyMetadata assemblyMetadata)
        {
            var peReferences = ImmutableArray.CreateRange(assemblyMetadata.Assembly.AssemblyReferences.Select(this.LoadAssemblySymbolOrMissingAssemblySymbol));

            var moduleReferences = new ModuleReferences<AssemblySymbol>(
                assemblyMetadata.Assembly.AssemblyReferences, peReferences, ImmutableArray.CreateRange(this.unifiedAssemblies));

            return moduleReferences;
        }

        /// <summary>
        /// </summary>
        /// <param name="readAll">
        /// </param>
        /// <param name="ignoreCurrent">
        /// </param>
        /// <returns>
        /// </returns>
        private IEnumerable<IType> ReadTypes(bool readAll = false, bool ignoreCurrent = false)
        {
            var assemblySymbol = this.LoadAssemblySymbol(this.Assembly);
            return this.ReadTypes(assemblySymbol, readAll, ignoreCurrent);
        }

        private IEnumerable<IType> ReadTypes(AssemblySymbol assemblySymbol, bool readAll = false, bool ignoreCurrent = false)
        {
            if (readAll)
            {
                var moduleReferences = this.LoadReferences(this.Assembly);
                foreach (var moduleAssemblySymbol in moduleReferences.Symbols)
                {
                    if (moduleAssemblySymbol is MissingAssemblySymbol)
                    {
                        throw new Exception(string.Format("Assembly '{0}' is missing", moduleAssemblySymbol));
                    }

                    foreach (var metadataTypeAdapter in this.EnumAllTypes(moduleAssemblySymbol as PEAssemblySymbol))
                    {
                        yield return metadataTypeAdapter;
                    }
                }
            }

            if (!ignoreCurrent)
            {
                // 3) Load Types
                foreach (var metadataTypeAdapter in this.EnumAllTypes(assemblySymbol as PEAssemblySymbol))
                {
                    yield return metadataTypeAdapter;
                }
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblyIdentity">
        /// </param>
        /// <returns>
        /// </returns>
        private string ResolveReferencePath(AssemblyIdentity assemblyIdentity)
        {
            var dllFileName = string.Concat(assemblyIdentity.Name, ".dll");
            if (File.Exists(dllFileName))
            {
                return Path.GetFullPath(dllFileName);
            }

            if (!string.IsNullOrWhiteSpace(this.DefaultDllLocations))
            {
                var dllFullName = Path.Combine(this.DefaultDllLocations, dllFileName);
                if (File.Exists(dllFullName))
                {
                    return dllFullName;
                }
            }

            var windir = Environment.GetEnvironmentVariable("windir");

            var dllFullNameGAC = Path.Combine(windir, string.Format(@"Microsoft.NET\assembly\GAC_MSIL\{0}\{1}_{2}_{3}_{4}", assemblyIdentity.Name, "4.0", assemblyIdentity.Version, assemblyIdentity.CultureName, GetAssemblyHashString(assemblyIdentity)));
            if (File.Exists(dllFullNameGAC))
            {
                return dllFullNameGAC;
            }

            // find first possible
            var dllFullNameGACSearch = Path.Combine(windir, string.Format(@"Microsoft.NET\assembly\GAC_MSIL\{0}", assemblyIdentity.Name));
            try
            {
                foreach (var dll in Directory.EnumerateFiles(dllFullNameGACSearch, "*.dll", SearchOption.AllDirectories))
                {
                    // TODO: filter it here
                    return dll;
                }
            }
            catch (Exception)
            {
            }

            if (assemblyIdentity.Name == "CoreLib")
            {
                return this.CoreLibPath;
            }

            if (assemblyIdentity.Name == "mscorlib")
            {
                if (!string.IsNullOrWhiteSpace(this.CoreLibPath))
                {
                    return this.CoreLibPath;
                }

                Debug.Assert(false, "you are using mscorlib from .NET");

                return typeof(int).Assembly.Location;
            }

            Debug.Fail("Not implemented yet");

            return null;
        }

        private static object GetAssemblyHashString(AssemblyIdentity assemblyIdentity)
        {
            if (!assemblyIdentity.HasPublicKey)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var @byte in assemblyIdentity.PublicKey)
            {
                sb.AppendFormat("{0:N}", @byte);
            }

            return sb.ToString();
        }

        /// <summary>
        /// </summary>
        /// <param name="assemblySymbol">
        /// </param>
        private void SetCorLib(PEAssemblySymbol assemblySymbol)
        {
            if (!assemblySymbol.Assembly.AssemblyReferences.Any())
            {
                // this is the core lib
                assemblySymbol.SetCorLibrary(assemblySymbol);
                return;
            }

            if (SetCorLib(assemblySymbol, assemblySymbol))
            {
                return;
            }

            Debug.Fail("CoreLib not set");
        }

        private bool SetCorLib(PEAssemblySymbol assemblySymbol, PEAssemblySymbol fromAssemblySymbol)
        {
            var loadedRefAssemblies = from assemblyIdentity in fromAssemblySymbol.Assembly.AssemblyReferences select this.LoadAssemblySymbol(assemblyIdentity);
            foreach (var loadedRefAssemblySymbol in loadedRefAssemblies)
            {
                var peRefAssembly = loadedRefAssemblySymbol as PEAssemblySymbol;
                if (peRefAssembly != null && !peRefAssembly.Assembly.AssemblyReferences.Any())
                {
                    assemblySymbol.SetCorLibrary(loadedRefAssemblySymbol);
                    return true;
                }
            }

            foreach (var loadedRefAssemblySymbol in loadedRefAssemblies)
            {
                if (this.SetCorLib(assemblySymbol, loadedRefAssemblySymbol as PEAssemblySymbol))
                {
                    return true;
                }
            }

            return false;
        }

        public const BindingFlags DefaultFlags =
            BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    }
}