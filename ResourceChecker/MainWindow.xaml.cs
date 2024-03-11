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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using System.Xml.Schema;
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
            // ..\..\..\..\..\Pixeval\src\Pixeval\Strings

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

            var defaultLang = null as LanguageItem;

            // Get all language directories (zh-cn, ru-ru, etc)
            foreach (var enumerateDirectory in directory.EnumerateDirectories())
            {
                var langName = enumerateDirectory.Name;
                langDict[langName] = new LanguageItem(enumerateDirectory.FullName, langName);
                if (langName == DefaultLanguageTextBox.Text)
                    defaultLang = langDict[langName];
            }

            if (defaultLang is null)
            {
                _ = MessageBox.Show(
                    $"""
                     Default language not found:
                     {DefaultLanguageTextBox.Text}
                                                              
                     Please specify the default language.
                     """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var otherLang = langDict.Values.Where(t => t != defaultLang).ToArray();

            // scan all files in default language
            var defaultLangDirectory = new DirectoryInfo(defaultLang.FullName);
            foreach (var file in defaultLangDirectory.EnumerateFiles())
            {
                var extension = file.Extension;

                if (extension is not ".resw" and not ".resjson" || file.Length is 0)
                    continue;

                var fileName = file.Name;

                var currentFile = defaultLang.Resources[fileName] = new ResourceFileItem(file.FullName, fileName, ExistedType.Existed);

                var otherFiles = new List<ResourceFileItem>();

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var languageItem in otherLang)
                {
                    // ".\Strings\ru-ru" + "\" + fileName
                    var fullName = languageItem.FullName + "\\" + fileName;
                    otherFiles.Add(languageItem.Resources[fileName] = new ResourceFileItem(fullName, fileName, ExistedType.Missing));
                }

                await ParseReadAsync(file, AddResource);

                continue;

                void AddResource(string name, string value)
                {
                    currentFile.ResourceItems[name] = new(name, value, ExistedType.Existed);

                    foreach (var otherFile in otherFiles)
                        otherFile.ResourceItems[name] = new ResourceItem(name, null, ExistedType.Missing);
                }
            }

            // scan all files in other languages
            foreach (var langItem in otherLang)
            {
                var otherLangDirectory = new DirectoryInfo(langItem.FullName);
                foreach (var file in otherLangDirectory.EnumerateFiles())
                {
                    var extension = file.Extension;

                    if (extension is not ".resw" and not ".resjson" || file.Length is 0)
                        continue;

                    var fileName = file.Name;

                    if (langItem.Resources.TryGetValue(fileName, out var currentFile))
                        currentFile.Existed = ExistedType.Existed;
                    else
                        currentFile = langItem.Resources[fileName] = new ResourceFileItem(file.FullName, fileName, ExistedType.Redundant);

                    await ParseReadAsync(file, AddResource);

                    continue;

                    void AddResource(string name, string value)
                    {
                        if (currentFile.ResourceItems.TryGetValue(name, out var resourceItem))
                        {
                            resourceItem.Existed = ExistedType.Existed;
                            resourceItem.Value = value;
                        }
                        else
                            currentFile.ResourceItems[name] = new ResourceItem(name, value, ExistedType.Redundant);
                    }
                }
            }

            All = langDict.Values;
            NotExists = All.Select(t => t.CloneNotExists()).Where(t => t is not null)!;

            async Task ParseReadAsync(FileInfo file, Action<string, string> addResource)
            {
                switch (file.Extension)
                {
                    case ".resw":
                        {
                            var doc = XDocument.Load(file.OpenRead());

                            if (doc.XPathSelectElements("//data") is { } elements)
                                foreach (var node in elements)
                                    addResource(node.Attribute("name")!.Value, node.Element("value")!.Value);
                            break;
                        }
                    case ".resjson":
                        {
                            var doc = await JsonDocument.ParseAsync(file.OpenRead());

                            if (doc.RootElement.EnumerateObject() is var elements)
                                foreach (var node in elements)
                                    addResource(node.Name, node.Value.GetString()!);
                            break;
                        }
                }
            }
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
                Error occured when opening file / folder:
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

        if (sender is TreeViewItem { IsSelected: true, DataContext: ResourceItem item })
            Clipboard.SetText(item.Name);
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

public class ResourceFileItem(string fullName, string displayName, ExistedType existed) : ExistedTypeBase(existed), IFullName
{
    public string FullName { get; } = fullName;

    public string DisplayName { get; } = displayName;

    public Dictionary<string, ResourceItem> ResourceItems { get; } = [];

    public ResourceFileItem? CloneNotExists()
    {
        var clone = new ResourceFileItem(FullName, DisplayName, Existed);
        foreach (var resourceItem in ResourceItems)
            if (resourceItem.Value.CloneNotExists() is { } item)
                clone.ResourceItems[resourceItem.Key] = item;
        return clone.ResourceItems.Count is 0 ? null : clone;
    }

    public override string ToString() => ResourceItems.Aggregate($"    {DisplayName} ({FullName}){(IsExisted ? "" : $" ({Existed})")}\n", (current, resource) => current + resource.Value);
}

public class ResourceItem(string name, string? value, ExistedType existed) : ExistedTypeBase(existed)
{
    public string Name { get; } = name;

    public string? Value { get; set; } = value;

    public ResourceItem? CloneNotExists()
    {
        return IsExisted ? null : new ResourceItem(Name, Value, Existed);
    }

    public override string ToString() => $"        {Name}{(IsExisted ? "" : $" ({Existed})")}\n";
}

public interface IFullName
{
    string FullName { get; }
}

public enum ExistedType
{
    Existed,
    Redundant,
    Missing
}

public abstract class ExistedTypeBase(ExistedType existed)
{
    public ExistedType Existed { get; set; } = existed;

    public bool IsRedundant => Existed is ExistedType.Redundant;

    public bool IsMissing => Existed is ExistedType.Missing;

    public bool IsExisted => Existed is ExistedType.Existed;
}