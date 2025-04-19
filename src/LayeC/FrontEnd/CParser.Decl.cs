using System.Diagnostics;

using LayeC.FrontEnd.Semantics.Decls;
using LayeC.FrontEnd.Syntax.Decls;
using LayeC.FrontEnd.Syntax.Meta;
using LayeC.FrontEnd.Syntax.Types;

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
    public SemaDeclGroup ParseExternalDeclaration(List<SyntaxCAttribute> declAttrs, List<SyntaxCAttribute> declSpecAttrs)
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

    public SemaDeclGroup ParseDeclarationOrFunctionDefinition(List<SyntaxCAttribute> declAttrs, List<SyntaxCAttribute> declSpecAttrs)
    {
        var declSpec = ParseDeclarationSpecifiers(declSpecAttrs, DeclSpecContext.TopLevel);

        Context.Todo(nameof(ParseDeclarationOrFunctionDefinition));
        throw new UnreachableException();
    }

    /// ParseDeclarationSpecifiers
    /// 
    ///         declaration-specifiers: [C99 6.7]
    ///           storage-class-specifier declaration-specifiers[opt]
    ///           type-specifier declaration-specifiers[opt]
    /// [C99]     function-specifier declaration-specifiers[opt]
    /// [C11]     alignment-specifier declaration-specifiers[opt]
    /// [GNU]     attributes declaration-specifiers[opt]
    ///
    ///         storage-class-specifier: [C99 6.7.1]
    ///           'typedef'
    ///           'extern'
    ///           'static'
    ///           'auto'
    ///           'register'
    /// [C11]     '_Thread_local'
    /// [C23]     'thread_local'
    /// [GNU]     '__thread'
    /// 
    ///         type-specifier:
    ///           'void'
    ///           'char'
    ///           'short'
    ///           'int'
    ///           'long'
    ///           'float'
    ///           'double'
    ///           'signed'
    ///           'unsigned'
    ///           '_BitInt' '(' constant-expression ')'
    ///           'bool'
    ///           '_Complex'
    ///           '_Decimal32'
    ///           '_Decimal64'
    ///           '_Decimal128'
    /// [C23]     'auto'
    /// [GNU]     '__auto_type'
    ///           atomic-type-specifier
    ///           struct-or-union-specifier
    ///           enum-specifier
    ///           typedef-name
    /// [C23]     typeof-specifier
    ///
    ///         atomic-type-specifier
    ///           '_Atomic' '(' type-name ')'
    /// 
    ///         typeof-specifier:
    ///           'typeof' '(' typeof-specifier-argument ')'
    ///           'typeof_unqual' '(' typeof-specifier-argument ')'
    ///         
    ///         typeof-specifier-argument:
    ///           expression
    ///           type-name
    /// 
    ///         function-specifier: [C99 6.7.4]
    /// [C99]     'inline'
    /// 
    ///         type-qualifier:
    ///           const
    ///           restrict
    ///           volatile
    ///           _Atomic
    public SyntaxCDeclarationSpecifiers ParseDeclarationSpecifiers(List<SyntaxCAttribute> declSpecAttrs, DeclSpecContext declContext)
    {
        //var attrs = new SyntaxCAttributesBuilder();

        Context.Todo(nameof(ParseDeclarationSpecifiers));
        throw new UnreachableException();
    }

    public SyntaxCTypeSpecifier ParseTypeSpecifier()
    {
        var typeSpecTokens = new List<Token>();
        var typeSpec = CTypeSpecifierKind.Unspecified;

        bool continueChecking = true;
        while (continueChecking)
        {
            var ct = CurrentToken;
            switch (ct.Kind)
            {
                default: continueChecking = false; continue;

                case TokenKind.KWInt:
                {
                    typeSpecTokens.Add(Consume());
                    if (typeSpec.HasFlag(CTypeSpecifierKind.Int))
                        Context.ErrorDuplicateTypeSpecifier(ct);
                    typeSpec |= CTypeSpecifierKind.Int;
                } break;
            }
        }

        Context.Todo(nameof(ParseTypeSpecifier));
        throw new UnreachableException();
    }
}
