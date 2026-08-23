using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sources.Models;

public enum SearchCategory
{
    Sources,
    Locations,
    Users,
    Radioisotopes
}

public partial class GlobalSearchResultItem : ObservableObject
{
    public Guid Id { get; set; }
    public SearchCategory Category { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string? ExtraInfo { get; set; }
    public string IconKind { get; set; } = "Radioactive";
    public string TargetView { get; set; } = "Sources";

    [ObservableProperty] private bool _isSelected;
}

public class GlobalSearchResultGroup
{
    public SearchCategory Category { get; set; }
    public string GroupTitle { get; set; } = string.Empty;
    public string GroupIcon { get; set; } = "Folder";
    public int TotalCount { get; set; }
    public List<GlobalSearchResultItem> Items { get; set; } = new();
}
