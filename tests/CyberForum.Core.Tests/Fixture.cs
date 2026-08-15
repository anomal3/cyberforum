namespace CyberForum.Core.Tests;

/// <summary>Доступ к эталонным страницам форума, снятым с живого сайта.</summary>
internal static class Fixture
{
    private static readonly string Directory =
        Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static string Read(string name)
    {
        var path = Path.Combine(Directory, name);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Нет эталонной страницы «{name}». Положи её в tests/Fixtures.", path);
        }

        return File.ReadAllText(path);
    }

    public static bool Exists(string name) => File.Exists(Path.Combine(Directory, name));
}
