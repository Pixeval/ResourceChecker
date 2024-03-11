#region Copyright

// GPL v3 License
// 
// ResourceChecker/ResourceChecker
// Copyright (c) 2024 ResourceChecker/ResourceFileItem.cs
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

using System.Collections.Generic;
using System.Linq;

namespace ResourceChecker;

public class ResourceFileItem(LanguageItem parent, string fullName, string displayName, ExistedType existed, ResourceFileItem? bind = null) : ExistedTypeBase(existed), IFullName
{
    public string FullName { get; set; } = fullName;

    public string DisplayName { get; set; } = displayName;

    public Dictionary<string, ResourceItem> ResourceItems { get; } = [];

    public LanguageItem Parent { get; } = parent;

    public ResourceFileItem? Bind { get; } = bind;

    public ResourceFileItem? CloneNotExists(LanguageItem parent)
    {
        var clone = new ResourceFileItem(parent, FullName, DisplayName, Existed, this);
        foreach (var resourceItem in ResourceItems)
            if (resourceItem.Value.CloneNotExists(clone) is { } item)
                clone.ResourceItems[resourceItem.Key] = item;
        return clone.ResourceItems.Count is 0 ? null : clone;
    }

    public ResourceItem? GetResourceItem(string name)
    {
        return ResourceItems.TryGetValue(name, out var resourceItem) ? resourceItem : null;
    }

    public override string ToString() => ResourceItems.Aggregate($"    {DisplayName} ({FullName}){(IsExisted ? "" : $" ({Existed})")}\n", (current, resource) => current + resource.Value);
}