//  ----------------------------------------------------------------------------------
//  Copyright Microsoft Corporation
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ----------------------------------------------------------------------------------

namespace Orleans.DurableTask.Core.Entities;


/// <summary>
/// A unique identifier for an entity, consisting of entity name and entity key.
/// </summary>
[DataContract]
[GenerateSerializer]
[Alias("EntityId")]
public readonly struct EntityId : IEquatable<EntityId>, IComparable
{
    /// <summary>
    /// Create an entity id for an entity.
    /// </summary>
    /// <param name="name">The name of this class of entities.</param>
    /// <param name="key">The entity key.</param>
    public EntityId(string name, string key)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name), "Invalid entity id: entity name must not be a null or empty string.");
        }

        this.Name = name;
        this.Key = key ?? throw new ArgumentNullException(nameof(key), "Invalid entity id: entity key must not be null.");
    }

    /// <summary>
    /// The name for this class of entities.
    /// </summary>
    [DataMember(Name = "name", IsRequired = true)]
    [Id(0)]
    public readonly string Name { get; }

    /// <summary>
    /// The entity key. Uniquely identifies an entity among all entities of the same name.
    /// </summary>
    [DataMember(Name = "key", IsRequired = true)]
    [Id(1)]
    public readonly string Key { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"@{this.Name}@{this.Key}";
    }

    /// <summary>
    /// Returns the entity ID for a given instance ID.
    /// </summary>
    /// <param name="instanceId">The instance ID.</param>
    /// <returns>the corresponding entity ID.</returns>
    public static EntityId FromString(string? instanceId)
    {
        if (string.IsNullOrEmpty(instanceId))
        {
            throw new ArgumentException(nameof(instanceId));
        }
        var pos = instanceId.IndexOf('@', 1);
        if (pos <= 0 || instanceId[0] != '@')
        {
            throw new ArgumentException($"Instance ID '{instanceId}' is not a valid entity ID.", nameof(instanceId));
        }
        var entityName = instanceId.Substring(1, pos - 1);
        var entityKey = instanceId.Substring(pos + 1);
        return new EntityId(entityName, entityKey);
    }

  
    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return (obj is EntityId other) && this.Equals(other);
    }

    /// <inheritdoc/>
    public bool Equals(EntityId other)
    {
        return (this.Name, this.Key).Equals((other.Name, other.Key));
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return (this.Name, this.Key).GetHashCode();
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
#if WEICHEI
        var other = (EntityId)obj;
        return (this.Name, this.Key).CompareTo((other.Name, other.Key));
#endif
        if (obj is EntityId other) {
            return (this.Name, this.Key).CompareTo((other.Name, other.Key));
        } else {
#warning TODO: what is the correct value?
            return -1;
        }
    }
}
