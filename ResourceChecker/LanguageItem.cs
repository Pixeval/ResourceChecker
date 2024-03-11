#region Copyright

// GPL v3 License
// 
// ResourceChecker/ResourceChecker
// Copyright (c) 2024 ResourceChecker/LanguageItem.cs
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

public class LanguageItem(string fullName, string displayName, LanguageItem? bind = null) : IFullName
{
    public string FullName { get; } = fullName;

    public string DisplayName { get; } = displayName;

    public Dictionary<string, ResourceFileItem> Resources { get; } = [];

    public LanguageItem? Bind { get; } = bind;

    public LanguageItem? CloneNotExists()
    {
        var clone = new LanguageItem(FullName, DisplayName, this);
        foreach (var resourceFileItem in Resources)
            if (resourceFileItem.Value.CloneNotExists(clone) is { } fileItem)
                clone.Resources[resourceFileItem.Key] = fileItem;
        return clone.Resources.Count is 0 ? null : clone;
    }

    public ResourceFileItem? GetResourceFileItem(string displayName)
    {
        return Resources.TryGetValue(displayName, out var resourceFileItem) ? resourceFileItem : null;
    }

    public override string ToString() => Resources.Aggregate($"{DisplayName} ({FullName})\n", (current, resource) => current + resource.Value);
}