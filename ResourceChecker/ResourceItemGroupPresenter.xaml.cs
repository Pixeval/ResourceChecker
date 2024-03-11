using System.Collections.Generic;
using System.Windows.Controls;

namespace ResourceChecker;

public partial class ResourceItemGroupPresenter : UserControl
{
    public ResourceItemGroupPresenter()
    {
        InitializeComponent();
    }
}

public class ResourceItemGroup(ResourceItem currentItem, ResourceItem? defaultItem, IEnumerable<ResourceItem> otherItems)
{
    public ResourceItem CurrentItem { get; } = currentItem;

    public ResourceItem? DefaultItem { get; } = defaultItem;

    public IEnumerable<ResourceItem> OtherItems { get; } = otherItems;
}