﻿using LayeC.Source;

namespace LayeC.FrontEnd.SemaTree.Decls;

public abstract class SemaDecl(SourceText source, SourceRange range)
    : SemaNode(source, range)
{
    public SemaDecl(Token token)
        : this(token.Source, token.Range)
    {
    }

    public SemaDecl(SemaNode child)
        : this(child.Source, child.Range)
    {
    }
}
