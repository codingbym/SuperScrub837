using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SuperScrub837.CustomExtensions
{
    public class TextBoxVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type typeTarget, object param, CultureInfo culture)
        {
            bool currVisFlag = (bool)value;

            if (currVisFlag)
            {
                return Visibility.Visible;
            }

            return Visibility.Hidden;
        }
        public object ConvertBack(object value, Type typeTarget, object param, CultureInfo culture)
        {
            throw new NotImplementedException();

        }
    }
}
