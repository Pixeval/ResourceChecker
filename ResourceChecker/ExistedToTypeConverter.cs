#region Copyright

// GPL v3 License
// 
// ResourceChecker/ResourceChecker
// Copyright (c) 2024 ResourceChecker/ExistedToTypeConverter.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ResourceChecker;

public class ExistedToTypeConverter : IValueConverter
{
    public object? Obj { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ExistedType.Missing => new SolidColorBrush(Colors.Red),
            ExistedType.Redundant => new(Colors.Blue),
            _ => null
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}