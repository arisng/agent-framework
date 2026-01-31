// Copyright (c) Microsoft. All rights reserved.

namespace AGUIWebChat.Client.Services;

public interface IComponentRegistry
{
    void Register(string mediaType, Type componentType);

    bool TryGetComponentType(string mediaType, out Type? componentType);

    IReadOnlyDictionary<string, Type> RegisteredComponents { get; }
}

public sealed class ComponentRegistry : IComponentRegistry
{
    private readonly Dictionary<string, Type> _components = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, Type> RegisteredComponents => this._components;

    public void Register(string mediaType, Type componentType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException("Media type must be provided.", nameof(mediaType));
        }

        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        this._components[mediaType] = componentType;
    }

    public bool TryGetComponentType(string mediaType, out Type? componentType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            componentType = null;
            return false;
        }

        return this._components.TryGetValue(mediaType, out componentType);
    }
}
