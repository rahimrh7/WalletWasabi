using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace  WalletWasabi.Fluent.Generators
{
	[Generator]
    public class NavigationMetaDataGenerator : ISourceGenerator
    {
        private const string AttributeText = @"// <auto-generated />
using System;

namespace WalletWasabi.Fluent
{
	public enum NavBarPosition
	{
		None,
		Top,
		Bottom
	}

	public sealed class NavigationMetaData
	{
		public bool Searchable { get; init; }

		public string Title { get; init; }

		public string Caption { get; init; }

		public string IconName { get; init; }

		public int Order { get; init; }

		public string Category { get; init; }

		public string[] Keywords { get; init; }

		public NavBarPosition NavBarPosition {get; init; }
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class NavigationMetaDataAttribute : Attribute
	{
		public NavigationMetaDataAttribute()
		{
		}

		public bool Searchable { get; set; }

		public string Title { get; set; }

		public string Caption { get; set; }

		public string IconName { get; set; }

		public int Order { get; set; }

		public string Category { get; set; }

		public string[] Keywords { get; set; }

		public NavBarPosition NavBarPosition {get; set; }
	}
}";

        public void Initialize(GeneratorInitializationContext context)
        {
			// System.Diagnostics.Debugger.Launch();
			context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            context.AddSource("NavigationMetaDataAttribute", SourceText.From(AttributeText, Encoding.UTF8));

            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
			{
				return;
			}

			CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributeText, Encoding.UTF8), options));
            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("WalletWasabi.Fluent.NavigationMetaDataAttribute");
            INamedTypeSymbol metadataSymbol = compilation.GetTypeByMetadataName("WalletWasabi.Fluent.NavigationMetaData");

            List<INamedTypeSymbol> namedTypeSymbols = new();

            foreach (var candidateClass in receiver.CandidateClasses)
            {
                SemanticModel model = compilation.GetSemanticModel(candidateClass.SyntaxTree);
                var namedTypeSymbol = model.GetDeclaredSymbol(candidateClass);

                if (namedTypeSymbol.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                {
	                namedTypeSymbols.Add(namedTypeSymbol);
                }
            }

            foreach (var namedTypeSymbol in namedTypeSymbols)
            {
                string classSource = ProcessClass(namedTypeSymbol, attributeSymbol, metadataSymbol, context);
                context.AddSource($"{namedTypeSymbol.Name}_NavigationMetaData.cs", SourceText.From(classSource, Encoding.UTF8));
            }
        }

        private string ProcessClass(INamedTypeSymbol namedTypeSymbol, ISymbol attributeSymbol, ISymbol metadataSymbol, GeneratorExecutionContext context)
        {
            if (!namedTypeSymbol.ContainingSymbol.Equals(namedTypeSymbol.ContainingNamespace, SymbolEqualityComparer.Default))
            {
                return null;
            }

            string namespaceName = namedTypeSymbol.ContainingNamespace.ToDisplayString();

            var source = new StringBuilder($@"// <auto-generated />
using System;
using System.Threading.Tasks;
using WalletWasabi.Fluent.ViewModels.Navigation;

namespace {namespaceName}
{{
    public partial class {namedTypeSymbol.Name}
    {{
");

            AttributeData attributeData = namedTypeSymbol.GetAttributes().Single(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));

            source.Append($@"        public static {metadataSymbol.ToDisplayString()} MetaData {{ get; }} = new()
        {{
");
            var length = attributeData.NamedArguments.Length;
            for (int i = 0; i < length; i++)
            {
	            var namedArgument = attributeData.NamedArguments[i];

	            source.AppendLine($"            {namedArgument.Key} = " +
	                              $"{(namedArgument.Value.Kind == TypedConstantKind.Array ? "new [] " : "")}" +
	                              $"{namedArgument.Value.ToCSharpString()}{(i < length - 1 ? "," : "")}");
            }
			source.Append($@"        }};
");

			source.Append($@"        public static void Register(Func<Task<RoutableViewModel>> createInstance) => NavigationManager.RegisterRoutable<{namedTypeSymbol.Name}>(MetaData, createInstance);");

			source.Append($@"    }}
}}");

			return source.ToString();
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                    && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}