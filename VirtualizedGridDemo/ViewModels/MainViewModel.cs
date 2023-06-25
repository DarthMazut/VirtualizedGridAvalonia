using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace VirtualizedGridDemo.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<GridItemVM> _items = new();

    [ObservableProperty]
    private int _maxColumns = 10;

    public MainViewModel()
    {
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                _items.Add(new GridItemVM(x, y));
            }
        }
    }
}
