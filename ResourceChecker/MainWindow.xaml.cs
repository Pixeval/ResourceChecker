using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Microsoft.Win32;

namespace ResourceChecker;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private LanguageItem? _defaultLang;

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

                     Please specify the directory that contains all the resources (usually named "Strings").
                     """, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var langDict = new Dictionary<string, LanguageItem>();

            var defaultLang = _defaultLang = null;

            // Get all language directories (zh-cn, ru-ru, etc)
            foreach (var enumerateDirectory in directory.EnumerateDirectories())
            {
                var langName = enumerateDirectory.Name;
                langDict[langName] = new(enumerateDirectory.FullName, langName);
                if (langName == DefaultLanguageTextBox.Text)
                    defaultLang = _defaultLang = langDict[langName];
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

                var currentFile = defaultLang.Resources[fileName] = new ResourceFileItem(defaultLang, file.FullName, fileName, ExistedType.Existed);

                var otherFiles = new List<ResourceFileItem>();

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var languageItem in otherLang)
                {
                    // ".\Strings\ru-ru" + "\" + fileName
                    var fullName = languageItem.FullName + "\\" + fileName;
                    otherFiles.Add(languageItem.Resources[fileName] = new ResourceFileItem(languageItem, fullName, fileName, ExistedType.Missing));
                }

                await ParseReadAsync(file, AddResource);

                continue;

                void AddResource(string name, string value)
                {
                    currentFile.ResourceItems[name] = new(currentFile, name, value, ExistedType.Existed);

                    foreach (var otherFile in otherFiles)
                        otherFile.ResourceItems[name] = new(otherFile, name, null, ExistedType.Missing);
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
                        currentFile = langItem.Resources[fileName] = new(langItem, file.FullName, fileName, ExistedType.Redundant);

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
                            currentFile.ResourceItems[name] = new(currentFile, name, value, ExistedType.Redundant);
                    }
                }
            }

            All = langDict.Values;
            NotExists = All.Select(t => t.CloneNotExists()).Where(t => t is not null)!;

            async Task ParseReadAsync(FileInfo file, Action<string, string> addResource)
            {
                if (file.Extension is ".resjson")
                {
                    var fileStream = file.OpenRead();
                    var doc = await JsonDocument.ParseAsync(fileStream);
                    fileStream.Dispose();

                    if (doc.RootElement.EnumerateObject() is var elements)
                        foreach (var node in elements)
                            addResource(node.Name, node.Value.GetString()!);
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

    private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (sender is TreeView { SelectedItem: ResourceItem resourceItem })
            ScrollViewer.Content = new ResourceItemGroupPresenter
            {
                DataContext = new ResourceItemGroup(
                    resourceItem,
                    _defaultLang?.GetResourceFileItem(resourceItem.Parent.DisplayName)
                        ?.GetResourceItem(resourceItem.Name),
                    All.Where(t => t.DisplayName != _defaultLang?.DisplayName && t.DisplayName != resourceItem.Parent.Parent.DisplayName)
                        .Select(t => t.GetResourceFileItem(resourceItem.Parent.DisplayName))
                        .Select(t => t?.GetResourceItem(resourceItem.Name))
                        .Where(t => t is not null)!
                    )
            };
    }

    private void OnTreeViewItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        return;
        if (sender is TreeViewItem { IsSelected: true, DataContext: IFullName obj })
            Helper.OpenFile(obj.FullName);

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