using Android.Views;
using Google.Android.Material.BottomNavigation;

namespace CyberForum.App.Platforms.Android;

/// <summary>
/// Красный кружок с числом на вкладке. Своих значков MAUI не умеет, а иконку с точкой
/// Android перекрашивает в цвет вкладки — поэтому берём родной бейдж Material прямо
/// у нижней панели.
/// </summary>
internal static class TabBadge
{
    public static void Show(int index, int count)
    {
        var view = Find(Platform.CurrentActivity?.Window?.DecorView);

        if (view is null || index < 0 || index >= view.Menu.Size())
        {
            return;
        }

        var item = view.Menu.GetItem(index);

        if (item is null)
        {
            return;
        }

        if (count <= 0)
        {
            view.RemoveBadge(item.ItemId);
            return;
        }

        var badge = view.GetOrCreateBadge(item.ItemId);

        badge.Number = count;
        badge.BackgroundColor = unchecked((int)0xFFE5342A);
        badge.BadgeTextColor = unchecked((int)0xFFFFFFFF);
    }

    private static BottomNavigationView? Find(global::Android.Views.View? root)
    {
        if (root is BottomNavigationView found)
        {
            return found;
        }

        if (root is not ViewGroup group)
        {
            return null;
        }

        for (var i = 0; i < group.ChildCount; i++)
        {
            var child = Find(group.GetChildAt(i));

            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }
}
