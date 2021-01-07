﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Robust.Generators
{
    [Generator]
    public class DataClassGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new AutoDataClassRegistrationReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if(!(context.SyntaxReceiver is AutoDataClassRegistrationReceiver receiver)) return;

            Debugger.Launch();

            var comp = (CSharpCompilation)context.Compilation;
            var iCompType = comp.GetTypeByMetadataName("Robust.Shared.Interfaces.GameObjects.IComponent");

            //resolve autodata registrations (we need the to validate the customdataclasses)
            var resolvedAutoDataRegistrations =
                receiver.Registrations.Select(cl => comp.GetSemanticModel(cl.SyntaxTree).GetDeclaredSymbol(cl)).ToImmutableHashSet();

            //resolve all custom dataclasses
            var resolvedCustomDataClasses = new Dictionary<ITypeSymbol, ITypeSymbol>();
            foreach (var classDeclarationSyntax in receiver.CustomDataClassRegistrations)
            {
                var symbol = comp.GetSemanticModel(classDeclarationSyntax.SyntaxTree)
                    .GetDeclaredSymbol(classDeclarationSyntax);

                var arg = symbol?.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "CustomDataClassAttribute")?.ConstructorArguments.FirstOrDefault();
                if (arg == null)
                {
                    var msg = $"Could not resolve argument of CustomDataClassAttribute for class {classDeclarationSyntax.Identifier.Text}";
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RADC0002", msg, msg, "Usage", DiagnosticSeverity.Error, true),
                        classDeclarationSyntax.GetLocation()));
                    return;
                }

                var customDataClass = (ITypeSymbol) arg.Value.Value;
                if (customDataClass == null)
                {
                    var msg = $"Could not resolve CustomDataClassAttribute for class {classDeclarationSyntax.Identifier.Text}";
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RADC0001", msg, msg, "Usage", DiagnosticSeverity.Error, true),
                        classDeclarationSyntax.GetLocation()));
                    continue;
                }

                string shouldInherit;
                if (resolvedAutoDataRegistrations.Any(r => SymbolEqualityComparer.Default.Equals(symbol, r)))
                {
                    shouldInherit = $"{symbol}_AUTODATA";
                }
                else
                {
                    shouldInherit = ResolveParentDataClass(symbol);
                }
                context.AddSource($"{customDataClass.Name}_INHERIT.g.cs", SourceText.From(GenerateCustomDataClassInheritanceCode(customDataClass.Name, customDataClass.ContainingNamespace.ToString(), shouldInherit), Encoding.UTF8));


                resolvedCustomDataClasses.Add(symbol, (ITypeSymbol)arg.Value.Value);
            }

            string ResolveParentDataClass(ITypeSymbol typeS, bool print = false)
            {
                var typeSymbol = typeS;
                if (typeSymbol is INamedTypeSymbol tSym)
                {
                    if(print) context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("AAA", $"{tSym.ConstructedFrom},{tSym.IsGenericType}", $"{tSym.ConstructedFrom},{tSym.IsGenericType}", "usage", DiagnosticSeverity.Warning, true),Location.None));
                    typeSymbol = tSym.ConstructedFrom;
                }


                if (resolvedCustomDataClasses.TryGetValue(typeSymbol, out var customDataClass))
                    return $"{customDataClass.ContainingNamespace}.{customDataClass.Name}";

                var metaName = $"{typeSymbol.ContainingNamespace}.{typeSymbol.Name}_AUTODATA";
                var dataClass = comp.GetTypeByMetadataName(metaName);
                if (dataClass != null || resolvedAutoDataRegistrations.Any(r => SymbolEqualityComparer.Default.Equals(r, typeSymbol))) return metaName;

                if(typeSymbol.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iCompType)) || typeSymbol.BaseType == null)
                    return "Robust.Shared.Prototypes.ComponentData";

                return ResolveParentDataClass(typeSymbol.BaseType, print);
            }

            T GetCtorArg<T>(ImmutableArray<TypedConstant> ctorArgs, int i)
            {
                try
                {
                    return (T) ctorArgs[i].Value;

                }
                catch
                {
                    return default;
                }
            }

            //generate all autodata classes
            foreach (var symbol in resolvedAutoDataRegistrations)
            {
                var fields = new List<FieldTemplate>();
                foreach (var member in symbol.GetMembers())
                {
                    var attribute = member.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name == "YamlFieldAttribute");
                    if(attribute == null) continue;
                    var fieldName = GetCtorArg<string>(attribute.ConstructorArguments, 0);
                    if (fieldName == null || !SyntaxFacts.IsValidIdentifier(GetFieldName(fieldName)))
                    {
                        var msg =
                            $"YamlFieldAttribute for Member {member} of type {symbol} has an invalid tag {fieldName}.";
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("RADC0003", msg, msg, "Usage", DiagnosticSeverity.Error, true),
                            member.Locations.First()));
                        continue;
                    }

                    var @readonly = GetCtorArg<bool>(attribute.ConstructorArguments, 1);
                    var flagType = GetCtorArg<ITypeSymbol>(attribute.ConstructorArguments, 2);

                    string type;
                    switch (member)
                    {
                        case IFieldSymbol fieldSymbol:
                            type = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            break;
                        case IPropertySymbol propertySymbol:
                            type = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                            break;
                        default:
                            var msg =
                                $"YamlFieldAttribute assigned for Member {member} of type {symbol} which is neither Field or Property! It will be ignored.";
                            context.ReportDiagnostic(Diagnostic.Create(
                                new DiagnosticDescriptor("RADC0000", msg, msg, "Usage", DiagnosticSeverity.Warning, true),
                                member.Locations.First()));
                            continue;
                    }
                    fields.Add(new FieldTemplate(fieldName, type, @readonly, flagType));
                }

                var name = $"{symbol.Name}_AUTODATA";
                var @namespace = symbol.ContainingNamespace.ToString();

                bool b = name == "PowerSupplierComponent_AUTODATA";
                var inheriting = ResolveParentDataClass(symbol.BaseType, b);

                context.AddSource($"{name}.g.cs",
                    SourceText.From(GenerateCode(name, @namespace, inheriting, fields), Encoding.UTF8));
            }
        }

        private string GenerateCustomDataClassInheritanceCode(string name, string @namespace, string inheriting)
        {
            return $@"namespace {@namespace} {{
    public partial class {name} : {inheriting} {{}}
}}
";
        }

        private string GetFieldName(string fieldname) => $"{fieldname}_field";

        private string GenerateCode(string name, string @namespace, string inheriting, List<FieldTemplate> fields)
        {
            var code = $@"#nullable enable
using System;
using System.Linq;
using Robust.Shared.Serialization;
namespace {@namespace} {{
    public class {name} : {inheriting} {{

";

            //generate fields
            foreach (var field in fields)
            {
                code += $@"
        public {field.Type} {GetFieldName(field.Name)};";
            }

            //generate exposedata
            code += @"

        /// <inheritdoc />
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);";

            foreach (var field in fields)
            {
                code += $@"
            {(field.ReadOnly ? "if(serializer.Reading) " : "")}serializer.NullableDataField(ref {GetFieldName(field.Name)}, ""{field.Name}"", null{(field.FlagType != default ? $", withFormat: WithFormat.Flags<{field.FlagType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()" : "")});";
            }
            code += @"
        }";

            //generate getvalue
            code += @"

        /// <inheritdoc />
        public override object? GetValue(string tag)
        {
            return tag switch
            {";

            foreach (var field in fields)
            {
                code += $@"
                ""{field.Name}"" => {GetFieldName(field.Name)},";
            }

            code += @"
                _ => base.GetValue(tag)
            };
        }";

            //generate setvalue
            code += @"

        /// <inheritdoc />
        public override void SetValue(string tag, object? value)
        {
            switch (tag)
            {";

            foreach (var field in fields)
            {
                code += $@"
                case ""{field.Name}"":
                    {GetFieldName(field.Name)} = ({field.Type})value;
                    break;";
            }
            code += @"
                default:
                    base.SetValue(tag, value);
                    break;
            }
        }";

            code += @"
    }
}";
            return code;
        }

        private struct FieldTemplate
        {
            public readonly string Name;
            public readonly string Type;
            public readonly bool ReadOnly;
            public readonly ITypeSymbol FlagType;

            public FieldTemplate(string name, string type, bool readOnly, ITypeSymbol flagType)
            {
                Name = name;
                ReadOnly = readOnly;
                FlagType = flagType;
                Type = type.EndsWith("?") ? type : $"{type}?";
            }
        }
    }
}
