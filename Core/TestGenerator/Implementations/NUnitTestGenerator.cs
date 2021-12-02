using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Core.TestGenerator.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.TestGenerator.Implementations
{
    public class NUnitTestGenerator : ITestGenerator
    {

        private static readonly List<UsingDirectiveSyntax> DefaultLoadDirectiveList = new()
        {
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Text")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("NUnit.Framework"))
        };

        private const string Whitespace = "    ";

        private readonly int maxRead;

        private readonly int maxGenerate;

        private readonly int maxWrite;

        public NUnitTestGenerator(int maxRead, int maxGenerate, int maxWrite)
        {
            this.maxRead = maxRead;
            this.maxGenerate = maxGenerate;
            this.maxWrite = maxWrite;
        }

        public Task GenerateTestsAsync(string inputDirectory, string outputDirectory)
        {
            var directoryReader = CreateReadDirectoryBlock();
            var reader = CreateReadFileBlock(maxRead);
            var generator = CreateGenerateTestBlock(maxGenerate);
            var writer = CreateWriteFileBlock(maxWrite, outputDirectory);
            
            var opt = new DataflowLinkOptions { PropagateCompletion = true };
            directoryReader.LinkTo(reader, opt);
            reader.LinkTo(generator, opt);
            generator.LinkTo(writer, opt);

            directoryReader.Post(inputDirectory);
            directoryReader.Complete();
            return writer.Completion;
        }

        private static TransformManyBlock<string, string> CreateReadDirectoryBlock()
        {
            var opt = new ExecutionDataflowBlockOptions();
            return new TransformManyBlock<string, string>(ReadDirectory, opt);
        }

        private static string[] ReadDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new ArgumentException("Directory doesn't exist");
            }

            return Directory.EnumerateFiles(path).ToArray();
        }

        private static TransformBlock<string, string> CreateReadFileBlock(int maxFiles)
        {
            var opt = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxFiles };
            return new TransformBlock<string, string>(ReadFileAsync, opt);
        }

        private static Task<string> ReadFileAsync(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("File doesn't exist");
            }

            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096,
                FileOptions.Asynchronous);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            return reader.ReadToEndAsync();
        }

        private static TransformManyBlock<string, string> CreateGenerateTestBlock(int maxFiles)
        {
            var opt = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxFiles };
            return new TransformManyBlock<string, string>(CreateTests, opt);
        }

        private static string[] CreateTests(string code)
        {
            var classes = CSharpSyntaxTree.ParseText(code).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Where(@class => @class.Modifiers.Any(SyntaxKind.PublicKeyword))
                .Where(@class => !@class.Modifiers.Any(SyntaxKind.StaticKeyword)).ToArray();

            return classes.Select(CreateTest).ToArray();
        }

        private static string CreateTest(TypeDeclarationSyntax classDeclaration)
        {
            var unit = SyntaxFactory.CompilationUnit();
            unit = DefaultLoadDirectiveList.Aggregate(unit,
                (current, loadDirective) => current.AddUsings(loadDirective));

            var @namespace = SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName("GeneratedNamespace"),
                    SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(SyntaxFactory.TriviaList(), "Tests",
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))))
                .WithNamespaceKeyword(SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                        SyntaxFactory.CarriageReturnLineFeed), SyntaxKind.NamespaceKeyword,
                    SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                .WithOpenBraceToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.OpenBraceToken,
                    SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                .WithCloseBraceToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed),
                    SyntaxKind.CloseBraceToken, SyntaxFactory.TriviaList())).AddMembers(SyntaxFactory
                    .ClassDeclaration(SyntaxFactory.Identifier(SyntaxFactory.TriviaList(),
                        classDeclaration.Identifier.Text + "Test",
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))
                    .WithAttributeLists(SyntaxFactory.SingletonList(SyntaxFactory
                        .AttributeList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("TestFixture"))))
                        .WithOpenBracketToken(SyntaxFactory.Token(
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed,
                                SyntaxFactory.Whitespace(Whitespace)), SyntaxKind.OpenBracketToken,
                            SyntaxFactory.TriviaList()))
                        .WithCloseBracketToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(),
                            SyntaxKind.CloseBracketToken,
                            SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed)))))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace)), SyntaxKind.PublicKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                    .WithKeyword(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.ClassKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space)))
                    .WithOpenBraceToken(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace)), SyntaxKind.OpenBraceToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.CarriageReturnLineFeed))).WithCloseBraceToken(
                        SyntaxFactory.Token(SyntaxFactory.TriviaList(new[]
                        {
                            SyntaxFactory.Whitespace(Whitespace),
                        }), SyntaxKind.CloseBraceToken, SyntaxFactory.TriviaList()))
                    .AddMembers(AddTestMethods(classDeclaration)));

            return unit.NormalizeWhitespace().AddMembers(@namespace).ToFullString();
        }

        private static MemberDeclarationSyntax[] AddTestMethods(SyntaxNode classDeclaration)
        {
            var methods = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.Modifiers.Any(SyntaxKind.PublicKeyword));

            return methods.Select(method => SyntaxFactory
                .MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.VoidKeyword,
                        SyntaxFactory.TriviaList(SyntaxFactory.Space))),
                    SyntaxFactory.Identifier(method.Identifier.Text + "Test"))
                .WithAttributeLists(SyntaxFactory.SingletonList(SyntaxFactory
                    .AttributeList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Test"))))
                    .WithOpenBracketToken(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace + Whitespace)),
                        SyntaxKind.OpenBracketToken, SyntaxFactory.TriviaList()))
                    .WithCloseBracketToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.CloseBracketToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.LineFeed)))))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace + Whitespace)),
                    SyntaxKind.PublicKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space))))
                .WithParameterList(SyntaxFactory.ParameterList().WithCloseParenToken(
                    SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.CloseParenToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.LineFeed))))
                .WithBody(SyntaxFactory.Block()
                    .WithOpenBraceToken(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace + Whitespace)),
                        SyntaxKind.OpenBraceToken, SyntaxFactory.TriviaList(SyntaxFactory.LineFeed)))
                    .WithCloseBraceToken(SyntaxFactory.Token(
                        SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(Whitespace + Whitespace)),
                        SyntaxKind.CloseBraceToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace(""),
                            SyntaxFactory.LineFeed)))).AddBodyStatements(SyntaxFactory
                    .ExpressionStatement(SyntaxFactory
                        .InvocationExpression(SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(
                                SyntaxFactory.TriviaList(
                                    SyntaxFactory.Whitespace(Whitespace + Whitespace + Whitespace)), "Assert",
                                SyntaxFactory.TriviaList())), SyntaxFactory.IdentifierName("Fail"))).WithArgumentList(
                            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("autogenerated")))))))
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxFactory.TriviaList(), SyntaxKind.SemicolonToken,
                        SyntaxFactory.TriviaList(SyntaxFactory.LineFeed))))).Cast<MemberDeclarationSyntax>().ToArray();
        }

        private static ActionBlock<string> CreateWriteFileBlock(int maxFiles, string path)
        {
            var opt = new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxFiles };
            return new ActionBlock<string>(text => WriteFileAsync(path, text), opt);
        }

        private static Task WriteFileAsync(string path, string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var fileName = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First().Identifier.Text;
            var filePath = Path.Combine(path, fileName + ".cs");
            using var outputFile = new StreamWriter(filePath);
            return outputFile.WriteAsync(text);
        }
    }
}