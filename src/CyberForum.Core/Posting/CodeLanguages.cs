namespace CyberForum.Core.Posting;

/// <summary>
/// Язык блока кода. На форуме под каждый заведён свой bb-код: [csharp]…[/csharp],
/// и от него зависит подсветка. Список собран по кнопкам редактора форума.
/// </summary>
public sealed record CodeLanguage(string Tag, string Title)
{
    public override string ToString() => Title;
}

public static class CodeLanguages
{
    /// <summary>Ходовые языки идут первыми — их выбирают в девяти случаях из десяти.</summary>
    public static readonly IReadOnlyList<CodeLanguage> All =
    [
        new("code", "Просто код"),
        new("csharp", "C#"),
        new("cpp", "C++"),
        new("clang", "C"),
        new("python", "Python"),
        new("java", "Java"),
        new("js", "JavaScript"),
        new("ts", "TypeScript"),
        new("html", "HTML"),
        new("css", "CSS"),
        new("php", "PHP"),
        new("sql", "SQL"),
        new("mysql", "MySQL"),
        new("tsql", "T-SQL"),
        new("plsql", "PL/SQL"),
        new("pascal", "Pascal"),
        new("delphi", "Delphi"),
        new("asm", "Ассемблер"),
        new("bash", "Bash"),
        new("pshell", "PowerShell"),
        new("winbatch", "Batch"),
        new("kotlin", "Kotlin"),
        new("swift", "Swift"),
        new("go", "Go"),
        new("rust", "Rust"),
        new("ruby", "Ruby"),
        new("rails", "Ruby on Rails"),
        new("perl", "Perl"),
        new("lua", "Lua"),
        new("haskell", "Haskell"),
        new("lisp", "Lisp"),
        new("fsharp", "F#"),
        new("vb", "Visual Basic"),
        new("vbnet", "VB.NET"),
        new("qbasic", "QBasic"),
        new("basic", "Basic"),
        new("objc", "Objective-C"),
        new("cppqt", "C++ Qt"),
        new("1c", "1С"),
        new("json", "JSON"),
        new("xml", "XML"),
        new("matlab", "Matlab"),
        new("fortran", "Fortran"),
        new("prolog", "Prolog"),
        new("actionscript", "ActionScript"),
        new("latex", "LaTeX"),
        new("phphtml", "PHP и HTML"),
        new("noparse", "Без обработки"),
    ];

    public static CodeLanguage Default => All[0];

    /// <summary>Оборачивает текст в bb-код языка; пустое место — просто заготовка.</summary>
    public static string Wrap(CodeLanguage language, string? text) =>
        $"[{language.Tag}]\n{text?.Trim() ?? string.Empty}\n[/{language.Tag}]\n";
}
