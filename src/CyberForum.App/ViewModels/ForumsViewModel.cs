using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

// Дерево разделов. Тянем его целиком один раз — оно большое, но меняется раз в год.
public sealed partial class ForumsViewModel(ForumClient client) : BaseViewModel
{
    public ObservableCollection<ForumCategory> Categories { get; } = [];

    // то же дерево, но как есть — по нему ищут внутренние страницы
    public IReadOnlyList<ForumNode> Nodes { get; private set; } = [];

    public bool IsCategory(string slug) =>
        Nodes.Any(node => string.Equals(node.Slug, slug, StringComparison.OrdinalIgnoreCase));

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        if (Categories.Count > 0)
        {
            return;
        }

        Title = "Разделы";
        await FillAsync(token);
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(FillAsync);

    [RelayCommand]
    private static Task OpenAsync(ForumNode? node) =>
        node is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThreadList(node.Slug, node.Title));

    [RelayCommand]
    private static Task OpenCategoryAsync(ForumCategory? category) =>
        category is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToCategory(category.Node.Slug, category.Name));

    private async Task FillAsync(CancellationToken token)
    {
        var tree = await client.GetForumTreeAsync(token);

        Nodes = tree;
        Categories.Clear();

        foreach (var category in tree)
        {
            Categories.Add(new ForumCategory(category));
        }
    }
}

// Категория верхнего уровня в общем списке
public sealed partial class ForumCategory(ForumNode node) : ObservableObject
{
    public ForumNode Node { get; } = node;

    public string Name => Node.Title;

    public string? Description => Node.Description;

    public IReadOnlyList<ForumNode> Sections => Node.Children;

    public bool HasSections => Node.Children.Count > 0;

    public string Counter => Word(Node.Children.Count);

    // склонение по-человечески: 21 раздел, 22 раздела, 25 разделов
    private static string Word(int count)
    {
        if (count == 0)
        {
            return "нет разделов";
        }

        var hundred = count % 100;
        var last = count % 10;

        if (hundred is >= 11 and <= 14)
        {
            return $"{count} разделов";
        }

        return last switch
        {
            1 => $"{count} раздел",
            >= 2 and <= 4 => $"{count} раздела",
            _ => $"{count} разделов",
        };
    }
}
