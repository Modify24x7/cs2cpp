﻿namespace Il2Native.Logic
{
    using System.CodeDom;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using DOM;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Symbols;

    public class CCodeUnitsBuilder
    {
        private readonly IList<CCodeUnit> _cunits = new List<CCodeUnit>();

        internal CCodeUnitsBuilder(IAssemblySymbol assembly, IDictionary<string, BoundStatement> boundBodyByMethodSymbol, IDictionary<string, SourceMethodSymbol> sourceMethodByMethodSymbol)
        {
            this.Assembly = assembly;
            this.BoundBodyByMethodSymbol = boundBodyByMethodSymbol;
            this.SourceMethodByMethodSymbol = sourceMethodByMethodSymbol;
        }

        internal IDictionary<string, BoundStatement> BoundBodyByMethodSymbol { get; set; }

        internal IDictionary<string, SourceMethodSymbol> SourceMethodByMethodSymbol { get; set; }

        protected IAssemblySymbol Assembly { get; set; }
       
        public IList<CCodeUnit> Build()
        {
            foreach (var type in this.Assembly.Modules.SelectMany(module => module.EnumAllTypes()).Where(t => t.ContainingType == null || !t.ContainingType.Name.Contains("<PrivateImplementationDetails>")))
            {
                this._cunits.Add(BuildUnit(type));
            }

            return this._cunits;
        }

        private CCodeUnit BuildUnit(ITypeSymbol type)
        {
            var unit = new CCodeUnit(type);
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
            {
                var key = method.ToDisplayString(SymbolDisplayFormat.TestFormat);
                SourceMethodSymbol sourceMethod;
                var sourceMethodFound = this.SourceMethodByMethodSymbol.TryGetValue(key, out sourceMethod);
                BoundStatement boundStatement;
                var boundStatementFound = this.BoundBodyByMethodSymbol.TryGetValue(key, out boundStatement);

                Debug.Assert(sourceMethodFound || boundStatementFound, "Method information can't be found");

                unit.Declarations.Add(new CCodeMethodDeclaration(sourceMethodFound ? sourceMethod : method));
                if (boundStatement != null)
                {
                    unit.Definitions.Add(new CCodeMethodDefinition(method, boundStatement));
                }
            }

            return unit;
        }
    }
}
