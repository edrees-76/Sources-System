using System;

namespace Sources.Helpers;

public class NavigationMessage
{
    public string ViewName { get; set; } = string.Empty;
    public object? Parameter { get; set; }
}
