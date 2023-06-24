using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualizedGrid
{
    internal class SimpleGrid : Panel
    {
        public static readonly StyledProperty<double> ItemHeightProperty =
            AvaloniaProperty.Register<SimpleGrid, double>(nameof(ItemHeight));

        public static readonly StyledProperty<double> ItemWidthProperty =
            AvaloniaProperty.Register<SimpleGrid, double>(nameof(ItemWidth));

        public static readonly StyledProperty<int> MaxElementsInRowProperty =
            AvaloniaProperty.Register<SimpleGrid, int>(nameof(MaxElementsInRow));

        public double ItemHeight
        {
            get => GetValue(ItemHeightProperty);
            set => SetValue(ItemHeightProperty, value);
        }

        public double ItemWidth
        {
            get => GetValue(ItemWidthProperty);
            set => SetValue(ItemWidthProperty, value);
        }

        public int MaxElementsInRow
        {
            get => GetValue(MaxElementsInRowProperty);
            set => SetValue(MaxElementsInRowProperty, value);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(
                GetElementsX() * ItemWidth,
                GetElementsY() * ItemHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int x = 0;
            int y = 0;

            foreach (Control child in Children)
            {
                if (!child.IsVisible)
                {
                    continue;
                }

                child.Arrange(new Rect(x * ItemWidth, y * ItemHeight, ItemWidth, ItemHeight));

                x++;

                if (x >= MaxElementsInRow)
                {
                    x = 0;
                    y++;
                }
            }

            return finalSize;
        }

        private int GetElementsX() => Children.Count > MaxElementsInRow ? MaxElementsInRow : Children.Count;


        private int GetElementsY() => (int)Math.Ceiling((double)Children.Count / MaxElementsInRow);
    }
}
