using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtualizedGridDemo.ViewModels;

namespace VirtualizedGridDemo.Controls
{
    public class CustomRenderItem : Control
    {
        protected override void OnDataContextEndUpdate()
        {
            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            double xCoord = 5;
            double yCoord = 5;
            double width = 25;
            double height = 25;

            string textToRender = (DataContext as GridItemVM)?.Text ?? string.Empty;
            
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Red)), new Rect(xCoord, yCoord, width, height));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Green)), new Rect(xCoord + 1, yCoord + 1, width - 2, height - 2));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Blue)), new Rect(xCoord + 2, yCoord + 2, width - 4, height - 4));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Yellow)), new Rect(xCoord + 3, yCoord + 3, width - 6, height - 6));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Purple)), new Rect(xCoord + 4, yCoord + 4, width - 8, height - 8));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Black)), new Rect(xCoord + 5, yCoord + 5, width - 10, height - 10));
            context.DrawRectangle(new Pen(new SolidColorBrush(Colors.Pink)), new Rect(xCoord + 6, yCoord + 6, width - 12, height - 12));
            context.DrawText(
                new FormattedText(
                    textToRender,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Arial")),
                    12,
                    new SolidColorBrush(Colors.Black)), new Point(5,25));
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            if (e.GetPosition(this).X > 10 && e.GetPosition(this).X < 25)
            {
                if (e.GetPosition(this).Y > 10 && e.GetPosition(this).Y < 25)
                {

                }
            }
        }
    }
}
