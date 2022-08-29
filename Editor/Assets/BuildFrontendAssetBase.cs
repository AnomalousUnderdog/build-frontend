using UnityEngine;

public abstract class BuildFrontendAssetBase : ScriptableObject
{
    public string MenuEntry { get { return Category + (Category == "" ? "" : "/") + Name; } }

    [Header("Categorization")]
    public string Name = "";
    public string Category = "";

    protected virtual void Awake()
    {
        if (Name == null)
            Name = name;
    }

}
