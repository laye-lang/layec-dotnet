using System.Diagnostics;

using LayeC.FrontEnd.Semantics.Decls;
using LayeC.FrontEnd.Syntax.Decls;
using LayeC.FrontEnd.Syntax.Meta;

namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    public enum DeclSpecContext
    {
        /// <summary>
        /// Normal, default context.
        /// </summary>
        Normal,

        /// <summary>
        /// Top-level declaration context,
        /// </summary>
        TopLevel,

        /// <summary>
        /// a _Generic selection expression's type association.
        /// </summary>
        Association,
    }

    /// ParseExternalDeclaration
    ///
    ///       external-declaration: [C99 6.9], declaration: [C++ dcl.dcl]
    ///         function-definition
    ///         declaration
    /// [GNU]   asm-definition
    /// [GNU]   __extension__ external-declaration
    /// [GNU]   asm-definition:
    ///           simple-asm-expr ';'
    public SemaDeclGroup ParseExternalDeclaration(SyntaxCAttributesBuilder declAttrs, SyntaxCAttributesBuilder declSpecAttrs)
    {
        SemaDecl? singleDecl = null;
        switch (CurrentToken.Kind)
        {
            // TODO(local): source-level pragmas
            // TODO(local): empty declaration? I think C++ only
            // TODO(local): unexpected }
            // TODO(local): unexpected EOF
            // TODO(local): __extension__
            // TODO(local): GNU asm
            // TODO(local): typedef
            // TODO(local): static assert
            default: goto dont_know;
        }

    dont_know:;
        if (singleDecl is null)
            return ParseDeclarationOrFunctionDefinition(declAttrs, declSpecAttrs);

        Context.Assert(singleDecl is not null, "Should not have gotten here without building a declaraiton node.");
        return new SemaDeclGroup(singleDecl);
    }

    public SemaDeclGroup ParseDeclarationOrFunctionDefinition(SyntaxCAttributesBuilder declAttrs, SyntaxCAttributesBuilder declSpecAttrs)
    {
        var declSpec = new SyntaxCDeclSpecBuilder();

        declSpec.TakeAttributesFrom(declSpecAttrs);
        ParseDeclarationSpecifiers(declSpec, DeclSpecContext.TopLevel);

        Context.Todo(nameof(ParseDeclarationOrFunctionDefinition));
        throw new UnreachableException();
    }

    private bool IsStorageClassSpecifier(TokenKind kind) => kind switch
    {
        TokenKind.KWTypedef or TokenKind.KWExtern or TokenKind.KWStatic or
        TokenKind.KWRegister or TokenKind.KWThread_Local or TokenKind.KW__Thread => true,
        TokenKind.KWAuto when !LanguageOptions.CIsC23 => true,
        _ => false,
    };

    /// ParseDeclarationSpecifiers
    /// 
    ///       declaration-specifiers: [C99 6.7]
    ///         storage-class-specifier declaration-specifiers[opt]
    ///         type-specifier declaration-specifiers[opt]
    /// [C99]   function-specifier declaration-specifiers[opt]
    /// [C11]   alignment-specifier declaration-specifiers[opt]
    /// [GNU]   attributes declaration-specifiers[opt]
    ///
    ///       storage-class-specifier: [C99 6.7.1]
    ///         'typedef'
    ///         'extern'
    ///         'static'
    ///         'auto'
    ///         'register'
    /// [C11]   '_Thread_local'
    /// [C23]   'thread_local'
    /// [GNU]   '__thread'
    /// 
    ///       function-specifier: [C99 6.7.4]
    /// [C99]   'inline'
    public void ParseDeclarationSpecifiers(SyntaxCDeclSpecBuilder declSpec, DeclSpecContext declContext)
    {
        var attrs = new SyntaxCAttributesBuilder();
    }
}
