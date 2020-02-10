﻿using System;
using System.Globalization;
using System.Windows.Data;
using TeraDataLite;

namespace TCC.Converters
{
    public class ReadyToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is ReadyStatus r)) r = ReadyStatus.None;
            //TODO: use triggers and visibility
            return r switch
            {
                ReadyStatus.NotReady => .9,
                ReadyStatus.Ready=> .9,
                ReadyStatus.Undefined => .9,
                _ => 0
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
