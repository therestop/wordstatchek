using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using WordstatCheck.Core;

namespace WordstatCheck.Desktop;

public partial class RegionPickerWindow : Window
{
    private readonly List<SelectableRegion> items;
    private readonly ICollectionView view;

    public IReadOnlyList<RegionOption> SelectedRegions { get; private set; } = [];

    public RegionPickerWindow(IReadOnlyList<RegionOption> regions, IEnumerable<string> selectedIds)
    {
        InitializeComponent();
        var selected = selectedIds.ToHashSet(StringComparer.Ordinal);
        items = regions.Select(region => new SelectableRegion(region, selected.Contains(region.Id))).ToList();
        view = CollectionViewSource.GetDefaultView(items);
        view.Filter = MatchesSearch;
        RegionsList.ItemsSource = view;
        UpdateSelectedCount();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private bool MatchesSearch(object value)
    {
        if (value is not SelectableRegion item) return false;
        var query = SearchBox.Text.Trim();
        return query.Length == 0
            || item.Region.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Region.Path.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || item.Region.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => view?.Refresh();

    private void RegionCheck_Changed(object sender, RoutedEventArgs e) => UpdateSelectedCount();

    private void UpdateSelectedCount()
    {
        if (SelectedCountText is null) return;
        var count = items.Count(item => item.IsSelected);
        SelectedCountText.Text = count == 0 ? "Все регионы" : $"Выбрано: {count}";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in items) item.IsSelected = false;
        RegionsList.Items.Refresh();
        UpdateSelectedCount();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        SelectedRegions = items.Where(item => item.IsSelected).Select(item => item.Region).ToList();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class SelectableRegion(RegionOption region, bool isSelected)
    {
        public RegionOption Region { get; } = region;
        public bool IsSelected { get; set; } = isSelected;
    }
}
