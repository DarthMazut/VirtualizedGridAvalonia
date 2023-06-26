using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace VirtualizedGridDemo.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<GridItemVM> _items = new();

    [ObservableProperty]
    private int _maxColumns = 100;

    public MainViewModel()
    {
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                _items.Add(new GridItemVM(x, y));
            }
        }
    }
}
