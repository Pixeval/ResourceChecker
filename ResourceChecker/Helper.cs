#region Copyright

// GPL v3 License
// 
// ResourceChecker/ResourceChecker
// Copyright (c) 2024 ResourceChecker/Helper.cs
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
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ResourceChecker;

public static class Helper
{
    public static void OpenFile(string filepath)
    {
        try
        {
            if (File.Exists(filepath))
                Process.Start(filepath);
            else if (Directory.Exists(filepath))
                Process.Start("explorer.exe", filepath);
            else
            {
                if (ShowError(
                        $"""
                         File / folder not existed:
                         {filepath}

                         Copy path?
                         """))
                    Clipboard.SetText(filepath);
            }
        }
        catch (Exception)
        {
            if (ShowError(
                    $"""
                     Error occured when opening file / folder:
                     {filepath}

                     Copy path?
                     """))
                Clipboard.SetText(filepath);
        }
    }

    public static bool ShowError(string message)
    {
        var messageBoxResult = MessageBox.Show(message, "Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
        return messageBoxResult is MessageBoxResult.Yes;
    }

    public static void ShowMessage(string message)
    {
        _ = MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}