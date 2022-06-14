using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SuperScrub837.OtherComponents
{
    public class DraggablePopup : Popup
    {
        Point _initialMousePosition;
        bool _isDragging;

        protected override void OnInitialized(EventArgs e)
        {
            var contents = Child as FrameworkElement;
            Debug.Assert(contents != null, "DraggablePopup either has no content if content that " +
             "does not derive from FrameworkElement. Must be fixed for dragging to work.");
            contents.MouseLeftButtonDown += Child_MouseLeftButtonDown;
            contents.MouseLeftButtonUp += Child_MouseLeftButtonUp;
            contents.MouseMove += Child_MouseMove;
        }

        private void Child_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            _initialMousePosition = e.GetPosition(null);
            element.CaptureMouse();
            _isDragging = true;
            e.Handled = true;
        }

        private void Child_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(null);
                HorizontalOffset = HorizontalOffset + (currentPoint.X - _initialMousePosition.X);
                VerticalOffset = VerticalOffset + (currentPoint.Y - _initialMousePosition.Y);
            }
        }

        private void Child_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                var element = sender as FrameworkElement;
                element.ReleaseMouseCapture();
                _isDragging = false;
                e.Handled = true;
            }
        }
    }
}
