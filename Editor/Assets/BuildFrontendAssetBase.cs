using UnityEngine;

public abstract class BuildFrontendAssetBase : ScriptableObject
{
    public string MenuEntry => $"{Category}{(string.IsNullOrEmpty(Category) ? string.Empty : "/")}{Name}";

    [Header("Categorization")]
    public string Name = "";
    public string Category = "";

    protected virtual void Awake()
    {
        Name ??= name;
    }
}