using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Внутренности одного узла дерева: разделы категории или подразделы раздела.
/// Раньше они раскрывались прямо в общем списке, но список внутри списка MAUI строит
/// целиком и на телефоне это подвешивало экран — теперь каждый уровень своей страницей.
/// </summary>
public sealed partial class CategoryViewModel(ForumsViewModel forums) : BaseViewModel, IQueryAttributable
{
    public ObservableCollection<ForumNode> Sections { get; } = [];

    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    // у самого раздела тоже есть темы, а у категории — нет
    [ObservableProperty]
    public partial bool HasOwnThreads { get; set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Slug = Read(query, "slug");
        Title = Read(query, "title");

        Sections.Clear();

        // дерево уже разобрано на вкладке «Разделы», второй раз тянуть его незачем
        var node = Find(forums.Nodes, Slug);

        if (node is null)
        {
            ErrorMessage = "Не нашёл этот раздел. Вернись на «Разделы» и попробуй ещё раз.";
            return;
        }

        // у категории верхнего уровня своих тем нет, у раздела — есть
        HasOwnThreads = !forums.IsCategory(Slug);

        foreach (var section in node.Children)
        {
            Sections.Add(section);
        }
    }

    [RelayCommand]
    private Task OpenAsync(ForumNode? node)
    {
        if (node is null)
        {
            return Task.CompletedTask;
        }

        // у раздела есть свои подразделы — сперва показываем их
        return node.Children.Count > 0
            ? Shell.Current.GoToAsync(Routes.ToCategory(node.Slug, node.Title))
            : Shell.Current.GoToAsync(Routes.ToThreadList(node.Slug, node.Title));
    }

    [RelayCommand]
    private Task OpenOwnAsync() =>
        string.IsNullOrEmpty(Slug)
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThreadList(Slug, Title));

    private static ForumNode? Find(IEnumerable<ForumNode> nodes, string slug)
    {
        foreach (var node in nodes)
        {
            if (string.Equals(node.Slug, slug, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            var found = Find(node.Children, slug);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string Read(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty)
            : string.Empty;
}
