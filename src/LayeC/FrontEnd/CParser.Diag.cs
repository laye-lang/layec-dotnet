namespace LayeC.FrontEnd;

public sealed partial class CParser
{
    private void DiagnoseUseOfC11Keyword(Token token)
    {
        if (LanguageOptions.CIsC11)
            Context.WarnC11CompatKeyword(token);
        else Context.ExtC11Keyword(token);
    }
}
