using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ResourceChecker;

public partial class ResourceItemPresenter
{
    public string? HeaderText
    {
        get => HeaderRun.Text;
        set
        {
            HeaderRun.Text = value;
            HeaderRun.FontWeight = value is "Current" or "Default" ? FontWeights.Bold : FontWeights.Normal;
        }
    }

    public ResourceItemPresenter()
    {
        InitializeComponent();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Helper.OpenFile(e.Uri.OriginalString);
    }

    private async void Save_OnClick(object sender, RoutedEventArgs e)
    {
        ((Button)sender).IsEnabled = false;
        try
        {
            var vm = (ResourceItem)DataContext;
            if (vm.Name == NameTextBox.Text && vm.Value == ValueTextBox.Text)
            {
                Helper.ShowMessage("Same name and value.");
                return;
            }
            var file = new FileInfo(vm.Parent.FullName);
            var oldName = vm.IsExisted ? vm.Name : null;
            if (!await SetAndSaveAsync(file, oldName, NameTextBox.Text, ValueTextBox.Text))
                return;

            SetViewModel(vm);
            if (vm.Bind is not null)
                SetViewModel(vm.Bind);

            void SetViewModel(ResourceItem item)
            {
                item.Name = NameTextBox.Text;
                item.Value = ValueTextBox.Text;
                if (item.IsMissing)
                {
                    item.Existed = ExistedType.Existed;
                    if (item.Parent.IsMissing)
                        item.Parent.Existed = ExistedType.Existed;
                }
            }
        }
        finally
        {
            ((Button)sender).IsEnabled = true;
        }
    }

    private static async Task<bool> SetAndSaveAsync(FileInfo file, string? oldName, string? newName, string newValue)
    {
        if (file.Extension is ".resjson")
        {
            IDictionary<string, string>? dict;
            if (file is { Exists: true, Length: > 0 })
            {
                var read = file.OpenRead();
                dict = await JsonSerializer.DeserializeAsync<IDictionary<string, string>>(read);
                read.Dispose();
            }
            else
                dict = new Dictionary<string, string>();
            if (dict is null)
            {
                if (Helper.ShowError(
                        $"""
                         Json file is not valid.
                         {file.FullName}

                         Copy path?
                         """))
                    Clipboard.SetText(file.FullName);
                return false;
            }

            Debug.Assert(!(newName is null && oldName is null));
            if (newName is null)
            {
                dict.Remove(oldName!);
            }
            else if (oldName is null || oldName == newName)
            {
                dict[newName] = newValue;
            }
            else if (oldName != newName)
            {
                dict.Remove(oldName);
                dict[newName] = newValue;
            }

            using var writeStream = new FileStream(file.FullName, FileMode.Create, FileAccess.Write);
            await JsonSerializer.SerializeAsync(writeStream, dict, Options);
        }

        return true;
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        ((Button)sender).IsEnabled = false;
        try
        {
            var vm = (ResourceItem)DataContext;
            var file = new FileInfo(vm.Parent.FullName);
            Debug.Assert(!vm.IsMissing);
            if (!await SetAndSaveAsync(file, vm.Name, null, null!))
                return;

            SetViewModel(vm);
            if (vm.Bind is not null)
                SetViewModel(vm.Bind);

            static void SetViewModel(ResourceItem item)
            {
                item.Value = null;
                if (item.IsRedundant)
                    item.Existed = ExistedType.NormalNotExists;
                else if (item.IsExisted)
                    item.Existed = ExistedType.Missing;
            }
        }
        finally
        {
            ((Button)sender).IsEnabled = true;
        }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
}