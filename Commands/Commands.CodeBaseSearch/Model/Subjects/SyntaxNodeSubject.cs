using System.Collections.Generic;
using BeaverSoft.Texo.Core.Actions;
using BeaverSoft.Texo.Core.Markdown.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Commands.CodeBaseSearch.Model.Subjects
{
    public abstract class SyntaxNodeSubject : Subject
    {
        private readonly CSharpSyntaxNode syntaxNode;

        public SyntaxNodeSubject(SubjectTypeEnum type, CSharpSyntaxNode syntaxNode, string name, IKeywordBuilder keywordBuilder)
            : base(type, name, keywordBuilder)
        {
            this.syntaxNode = syntaxNode;
        }

        public SyntaxNodeSubject(SubjectTypeEnum type, CSharpSyntaxNode syntaxNode, string name)
            : base(type, name)
        {
            this.syntaxNode = syntaxNode;
        }

        public override void WriteToMarkdown(MarkdownBuilder builder)
        {
            builder.Bullet();
            builder.Link(Name, GetLinkUrl());
        }

        protected string GetLinkUrl()
        {
            Location location = syntaxNode.GetLocation();
            string filePath = syntaxNode.SyntaxTree.FilePath;

            if (!location.IsInSource)
            {
                return ActionBuilder.PathOpenUri(filePath);
            }

            return ActionBuilder.BuildActionUri(
                    ActionNames.PATH_OPEN,
                    new Dictionary<string, string>
                    {
                        { ActionParameters.PATH, filePath },
                        { "line", location.GetLineSpan().StartLinePosition.Line.ToString() }
                    });
        }
    }
}
