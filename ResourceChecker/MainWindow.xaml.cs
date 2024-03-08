using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.Win32;

namespace ResourceChecker;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        try
        {
            // ..\..\..\..\..\Pixeval\src

            var directory = new DirectoryInfo(PathTextBox.Text);
            if (!directory.Exists)
            {
                _ = MessageBox.Show(
                    $"""
                     Directory not existed:
                     {directory.FullName}

                     Please specify the deepest directory that contains all the resources.
                     """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var langDict = new Dictionary<string, LanguageItem>();

            foreach (var stringsDirectory in directory.EnumerateDirectories("Strings", SearchOption.AllDirectories))
                foreach (var enumerateDirectory in stringsDirectory.EnumerateDirectories())
                {
                    var langName = enumerateDirectory.Name;

                    var currentLang = langDict.TryGetValue(langName, out LanguageItem? value1)
                        ? value1
                        : langDict[langName]
                            = langDict.Count is 0
                                ? new(enumerateDirectory.FullName, langName)
                                : langDict.First().Value.Clone(enumerateDirectory.FullName, langName);

                    foreach (var file in enumerateDirectory.EnumerateFiles("*", SearchOption.AllDirectories))
                    {
                        var extension = file.Extension;

                        if (extension is not ".resw" and not ".resjson")
                            continue;

                        var fileName = file.Name;

                        List<ResourceFileItem> allFiles;

                        if (currentLang.Resources.TryGetValue(fileName, out var currentFile))
                        {
                            currentFile.Existed = true;
                            allFiles = langDict.Values.Select(x => x.Resources[fileName]).ToList();
                        }
                        else
                        {
                            allFiles = [];
                            foreach (var item in langDict)
                            {
                                bool curr = Equals(item.Value, currentLang);
                                var newFile = item.Value.Resources[fileName] = new(file.FullName, fileName, curr);
                                allFiles.Add(newFile);
                                if (curr)
                                    currentFile = newFile;
                            }
                        }

                        switch (extension)
                        {
                            case ".resw":
                                {
                                    var doc = XDocument.Load(file.OpenRead());

                                    if (doc.XPathSelectElements("//data") is { } elements)
                                        foreach (var node in elements)
                                            AddResource(node.Attribute("name")!.Value, node.Element("value")!.Value);
                                    break;
                                }
                            case ".resjson":
                                {
                                    var doc = await JsonDocument.ParseAsync(file.OpenRead());

                                    if (doc.RootElement.EnumerateObject() is var elements)
                                        foreach (var node in elements)
                                            AddResource(node.Name, node.Value.GetString()!);
                                    break;
                                }
                        }

                        continue;

                        void AddResource(string name, string value)
                        {
                            if (currentFile!.ResourceItems.TryGetValue(name, out var resourceItem))
                            {
                                resourceItem.Existed = true;
                                resourceItem.Value = value;
                            }
                            else
                            {
                                foreach (var resourceFileItem in allFiles)
                                {
                                    resourceFileItem.ResourceItems[name] = Equals(resourceFileItem, currentFile)
                                        ? new(name, value, true)
                                        : new ResourceItem(name, null, false);
                                }
                            }
                        }
                    }
                }

            All = langDict.Values;
            NotExists = All.Select(t => t.CloneNotExists()).Where(t => t is not null)!;
        }
        finally
        {
            button.IsEnabled = true;
        }

        TreeView.ItemsSource = CheckBox.IsChecked is true ? All : NotExists;
    }

    private IEnumerable<LanguageItem> All { get; set; } = [];

    private IEnumerable<LanguageItem> NotExists { get; set; } = [];

    private static void OpenFile(string filepath)
    {
        try
        {
            var pro = new Process
            {
                StartInfo = new(filepath),
            };
            pro.Start();
        }
        catch (Exception)
        {
            var messageBoxResult = MessageBox.Show(
                $"""
                Error occured when opening file/folder:
                {filepath}
                
                Copy path?
                """, "Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
            if (messageBoxResult is MessageBoxResult.Yes)
                Clipboard.SetText(filepath);
        }
    }

    private void OnTreeViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem { IsSelected: true, DataContext: IFullName obj })
            OpenFile(obj.FullName);
    }

    private void CheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        TreeView.ItemsSource = CheckBox.IsChecked is true ? All : NotExists;
    }

    private void ExpandAll_OnClick(object sender, RoutedEventArgs e)
    {
        ExpandAll = true;
    }

    private void CollapseAll_OnClick(object sender, RoutedEventArgs e)
    {
        ExpandAll = false;
    }

    private void OutputToFile_OnClick(object sender, RoutedEventArgs e)
    {
        var saveFileDialog = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt" };
        if (saveFileDialog.ShowDialog() is not true)
            return;
        var outputFile = saveFileDialog.FileName;

        File.WriteAllText(outputFile, NotExists.Aggregate("", (current, languageItem) => current + languageItem));
    }

    private bool _expandAll;

    public bool ExpandAll
    {
        get => _expandAll;
        set => SetField(ref _expandAll, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

public class RedWhenFalseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is false ? new SolidColorBrush(Colors.Red) : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return false;
    }
}

public class LanguageItem(string fullName, string displayName) : IFullName
{
    public string FullName { get; } = fullName;

    public string DisplayName { get; } = displayName;

    public Dictionary<string, ResourceFileItem> Resources { get; } = [];

    public LanguageItem Clone(string fullName, string displayName)
    {
        var clone = new LanguageItem(fullName, displayName);
        foreach (var resourceFileItem in Resources)
        {
            clone.Resources[resourceFileItem.Key] = resourceFileItem.Value.Clone();
        }
        return clone;
    }

    public LanguageItem? CloneNotExists()
    {
        var clone = new LanguageItem(FullName, DisplayName);
        foreach (var resourceFileItem in Resources)
            if (resourceFileItem.Value.CloneNotExists() is { } fileItem)
                clone.Resources[resourceFileItem.Key] = fileItem;
        return clone.Resources.Count is 0 ? null : clone;
    }

    public override string ToString() => Resources.Aggregate($"{DisplayName} ({FullName})\n", (current, resource) => current + resource.Value);
}

public class ResourceFileItem(string fullName, string displayName, bool existed) : IFullName
{
    public string FullName { get; } = fullName;

    public string DisplayName { get; } = displayName;

    public bool Existed { get; set; } = existed;

    public Dictionary<string, ResourceItem> ResourceItems { get; } = [];

    public ResourceFileItem Clone()
    {
        var clone = new ResourceFileItem(FullName, DisplayName, false);
        foreach (var resourceItem in ResourceItems)
        {
            clone.ResourceItems[resourceItem.Key] = resourceItem.Value.Clone();
        }

        return clone;
    }

    public ResourceFileItem? CloneNotExists()
    {
        var clone = new ResourceFileItem(FullName, DisplayName, Existed);
        foreach (var resourceItem in ResourceItems)
            if (resourceItem.Value.CloneNotExists() is { } item)
                clone.ResourceItems[resourceItem.Key] = item;
        return clone.ResourceItems.Count is 0 ? null : clone;
    }

    public override string ToString() => ResourceItems.Aggregate($"    {DisplayName} ({FullName}){(Existed ? "" : " (not existed)")}\n", (current, resource) => current + resource.Value);
}

public class ResourceItem(string name, string? value, bool existed)
{
    public string Name { get; } = name;

    public string? Value { get; set; } = value;

    public bool Existed { get; set; } = existed;

    public ResourceItem Clone()
    {
        return new(Name, null, false);
    }

    public ResourceItem? CloneNotExists()
    {
        return Existed ? null : new ResourceItem(Name, Value, Existed);
    }

    public override string ToString() => $"        {Name}{(Existed ? "" : " (not existed)")}\n";
}

public interface IFullName
{
    string FullName { get; }
}