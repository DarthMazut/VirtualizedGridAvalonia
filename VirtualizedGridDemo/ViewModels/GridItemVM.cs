using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualizedGridDemo.ViewModels
{
    public partial class GridItemVM : ObservableObject
    {
        private readonly int _x;
        private readonly int _y;

        public string Text => $"[{_x},{_y}]";

        [ObservableProperty]
        private bool _isChecked;

        [ObservableProperty]
        private bool _visible = true;

        public GridItemVM(int x, int y)
        {
            _x = x;
            _y = y;
        }
    }
}
