namespace DepotUploader;

internal sealed class KeyValuesNode
{
    public KeyValuesNode(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public KeyValuesNode(string key)
    {
        Key = key;
        Children = new List<KeyValuesNode>();
    }

    public string Key { get; }

    public string? Value { get; set; }

    public List<KeyValuesNode>? Children { get; }

    public KeyValuesNode? FindChild(string key)
    {
        if (Children is null)
        {
            return null;
        }

        foreach (KeyValuesNode child in Children)
        {
            if (string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    public string GetValue(string key)
    {
        return FindChild(key)?.Value ?? string.Empty;
    }

    public void SetValue(string key, string value)
    {
        if (Children is null)
        {
            throw new InvalidOperationException($"\"{Key}\" holds a value, not a block.");
        }

        KeyValuesNode? child = FindChild(key);
        if (child is null)
        {
            Children.Add(new KeyValuesNode(key, value));

            return;
        }

        child.Value = value;
    }

    public void SetValueWhenDifferent(string key, string value)
    {
        if (GetValue(key) != value)
        {
            SetValue(key, value);
        }
    }
}
