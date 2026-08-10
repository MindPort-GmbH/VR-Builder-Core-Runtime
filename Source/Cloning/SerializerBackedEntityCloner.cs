// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using VRBuilder.Core.EntityOwners;
using VRBuilder.Core.Serialization;

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Creates entity copies by round-tripping them through an <see cref="IProcessSerializer"/>.
    /// </summary>
    public sealed class SerializerBackedEntityCloner : IEntityCloner
    {
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static ReferenceComparer<T> Instance { get; } = new ReferenceComparer<T>();

            public bool Equals(T left, T right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(T value)
            {
                return RuntimeHelpers.GetHashCode(value);
            }
        }

        private sealed class CloneContext : IEntityCloneContext
        {
            private readonly IReadOnlyDictionary<Guid, IEntity> copiesBySourceId;
            private readonly IReadOnlyDictionary<Guid, IEntity> objectReferenceTargetsBySourceId;

            public CloneContext(IReadOnlyDictionary<Guid, IEntity> copiesBySourceId,
                IReadOnlyDictionary<Guid, IEntity> objectReferenceTargetsBySourceId)
            {
                this.copiesBySourceId = copiesBySourceId;
                this.objectReferenceTargetsBySourceId = objectReferenceTargetsBySourceId;
            }

            public Guid RemapId(Guid sourceId)
            {
                return copiesBySourceId.TryGetValue(sourceId, out IEntity copy) ? copy.Id : sourceId;
            }

            public bool TryResolveEntity<TEntity>(Guid sourceId, out TEntity entity) where TEntity : class, IEntity
            {
                if (objectReferenceTargetsBySourceId.TryGetValue(sourceId, out IEntity sourceTarget) == false)
                {
                    entity = null;
                    return false;
                }

                IEntity resolvedEntity = copiesBySourceId.TryGetValue(sourceId, out IEntity copy) ? copy : sourceTarget;
                entity = resolvedEntity as TEntity;
                return entity != null;
            }
        }

        private readonly IProcessSerializer serializer;

        public SerializerBackedEntityCloner(IProcessSerializer serializer)
        {
            this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        /// <inheritdoc />
        public TEntity Clone<TEntity>(TEntity source) where TEntity : class, IEntity
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            IReadOnlyList<IEntity> sourceEntities = GetOwnedEntities(source);
            IReadOnlyDictionary<Guid, IEntity> sourceEntitiesById = IndexEntitiesById(sourceEntities, "source");
            IReadOnlyCollection<EntityReference> sourceReferences = GetEntityReferences(sourceEntities);
            IReadOnlyDictionary<Guid, IEntity> objectReferenceTargetsBySourceId = IndexObjectReferenceTargets(sourceReferences);

            byte[] serializedEntity = serializer.EntityToByteArray(source);
            if (serializedEntity == null)
            {
                throw new InvalidOperationException("The configured serializer returned no data while cloning an entity.");
            }

            IEntity deserializedEntity = serializer.EntityFromByteArray(serializedEntity);

            if (deserializedEntity is not TEntity copy)
            {
                throw new InvalidOperationException($"The configured serializer returned '{deserializedEntity?.GetType().FullName ?? "null"}' while cloning '{source.GetType().FullName}'.");
            }

            IReadOnlyList<IEntity> copiedEntities = GetOwnedEntities(copy);
            IReadOnlyDictionary<Guid, IEntity> copiedEntitiesByOldId = IndexEntitiesById(copiedEntities, "copied");
            ValidateCopiedGraph(sourceEntities, sourceEntitiesById, copiedEntitiesByOldId);
            IReadOnlyCollection<EntityReference> copiedReferences = GetEntityReferences(copiedEntities);
            ValidateCopiedReferences(sourceReferences, copiedReferences);
            IReadOnlyList<KeyValuePair<EntityReference, Guid>> copiedReferencesWithOldIds = copiedReferences
                .Select(reference => new KeyValuePair<EntityReference, Guid>(reference, reference.ReferencedId))
                .ToList();

            HashSet<Guid> regeneratedIds = new HashSet<Guid>();

            foreach (IEntity sourceEntity in sourceEntities)
            {
                IEntity copiedEntity = copiedEntitiesByOldId[sourceEntity.Id];
                copiedEntity.RegenerateId();

                if (copiedEntity.Id == Guid.Empty || sourceEntitiesById.ContainsKey(copiedEntity.Id) || regeneratedIds.Add(copiedEntity.Id) == false)
                {
                    throw new InvalidOperationException($"Regenerating the identifier of '{copiedEntity.GetType().FullName}' produced the invalid or conflicting identifier '{copiedEntity.Id}'.");
                }
            }

            CloneContext context = new CloneContext(copiedEntitiesByOldId, objectReferenceTargetsBySourceId);
            foreach (KeyValuePair<EntityReference, Guid> copiedReference in copiedReferencesWithOldIds)
            {
                copiedReference.Key.Remap(copiedReference.Value, context);
            }

            return copy;
        }

        private static IReadOnlyList<IEntity> GetOwnedEntities(IEntity root)
        {
            List<IEntity> entities = new List<IEntity>();
            HashSet<IEntity> visited = new HashSet<IEntity>(ReferenceComparer<IEntity>.Instance);
            CollectOwnedEntities(root, entities, visited);
            return entities;
        }

        private static void CollectOwnedEntities(IEntity entity, ICollection<IEntity> entities, ISet<IEntity> visited)
        {
            if (entity == null || visited.Add(entity) == false)
            {
                return;
            }

            entities.Add(entity);

            if (entity is IDataOwner dataOwner && dataOwner.Data is IEntityCollectionData collectionData)
            {
                foreach (IEntity child in collectionData.GetChildren() ?? Enumerable.Empty<IEntity>())
                {
                    CollectOwnedEntities(child, entities, visited);
                }
            }
        }

        private static IReadOnlyDictionary<Guid, IEntity> IndexEntitiesById(IEnumerable<IEntity> entities, string graphName)
        {
            Dictionary<Guid, IEntity> entitiesById = new Dictionary<Guid, IEntity>();

            foreach (IEntity entity in entities)
            {
                if (entity.Id == Guid.Empty)
                {
                    throw new InvalidOperationException($"The {graphName} entity '{entity.GetType().FullName}' has an empty identifier.");
                }

                if (entitiesById.TryGetValue(entity.Id, out IEntity conflictingEntity))
                {
                    throw new InvalidOperationException($"The {graphName} entities '{conflictingEntity.GetType().FullName}' and '{entity.GetType().FullName}' share the identifier '{entity.Id}'.");
                }

                entitiesById.Add(entity.Id, entity);
            }

            return entitiesById;
        }

        private static void ValidateCopiedGraph(IReadOnlyList<IEntity> sourceEntities, IReadOnlyDictionary<Guid, IEntity> sourceEntitiesById,
            IReadOnlyDictionary<Guid, IEntity> copiedEntitiesById)
        {
            if (sourceEntitiesById.Count != copiedEntitiesById.Count || sourceEntitiesById.Keys.Any(id => copiedEntitiesById.ContainsKey(id) == false))
            {
                throw new InvalidOperationException("The configured serializer did not preserve the owned entity graph while cloning.");
            }

            foreach (IEntity sourceEntity in sourceEntities)
            {
                IEntity copiedEntity = copiedEntitiesById[sourceEntity.Id];
                if (sourceEntity.GetType() != copiedEntity.GetType())
                {
                    throw new InvalidOperationException($"The configured serializer changed entity '{sourceEntity.Id}' from type '{sourceEntity.GetType().FullName}' to '{copiedEntity.GetType().FullName}'.");
                }

                IReadOnlyList<Guid?> sourceChildIds = GetChildIds(sourceEntity);
                IReadOnlyList<Guid?> copiedChildIds = GetChildIds(copiedEntity);

                if (sourceChildIds.SequenceEqual(copiedChildIds) == false)
                {
                    throw new InvalidOperationException($"The configured serializer did not preserve the children of entity '{sourceEntity.Id}'.");
                }
            }
        }

        private static IReadOnlyList<Guid?> GetChildIds(IEntity entity)
        {
            if (entity is IDataOwner dataOwner && dataOwner.Data is IEntityCollectionData collectionData)
            {
                return (collectionData.GetChildren() ?? Enumerable.Empty<IEntity>()).Select(child => child?.Id).ToList();
            }

            return Array.Empty<Guid?>();
        }

        private static IReadOnlyDictionary<Guid, IEntity> IndexObjectReferenceTargets(IEnumerable<EntityReference> references)
        {
            Dictionary<Guid, IEntity> targetsById = new Dictionary<Guid, IEntity>();

            foreach (EntityReference reference in references.Where(reference => reference.ReferencedEntity != null))
            {
                Guid referencedId = reference.ReferencedId;
                IEntity referencedEntity = reference.ReferencedEntity;

                if (referencedId == Guid.Empty)
                {
                    throw new InvalidOperationException($"The object-backed entity reference to '{referencedEntity.GetType().FullName}' has an empty identifier.");
                }

                if (targetsById.TryGetValue(referencedId, out IEntity conflictingTarget) && ReferenceEquals(conflictingTarget, referencedEntity) == false)
                {
                    throw new InvalidOperationException($"Entity references point to different objects sharing the identifier '{referencedId}'.");
                }

                targetsById[referencedId] = referencedEntity;
            }

            return targetsById;
        }

        private static void ValidateCopiedReferences(IEnumerable<EntityReference> sourceReferences, IEnumerable<EntityReference> copiedReferences)
        {
            IReadOnlyDictionary<(Type Type, Guid Id), int> sourceReferenceCounts = CountReferences(sourceReferences);
            IReadOnlyDictionary<(Type Type, Guid Id), int> copiedReferenceCounts = CountReferences(copiedReferences);

            if (sourceReferenceCounts.Count != copiedReferenceCounts.Count ||
                sourceReferenceCounts.Any(pair => copiedReferenceCounts.TryGetValue(pair.Key, out int copiedCount) == false || copiedCount != pair.Value))
            {
                throw new InvalidOperationException("The configured serializer did not preserve the entity references of the copied graph.");
            }
        }

        private static IReadOnlyDictionary<(Type Type, Guid Id), int> CountReferences(IEnumerable<EntityReference> references)
        {
            Dictionary<(Type Type, Guid Id), int> counts = new Dictionary<(Type Type, Guid Id), int>();

            foreach (EntityReference reference in references)
            {
                (Type Type, Guid Id) key = (reference.GetType(), reference.ReferencedId);
                counts[key] = counts.TryGetValue(key, out int count) ? count + 1 : 1;
            }

            return counts;
        }

        private static IReadOnlyCollection<EntityReference> GetEntityReferences(IEnumerable<IEntity> entities)
        {
            HashSet<EntityReference> references = new HashSet<EntityReference>(ReferenceComparer<EntityReference>.Instance);

            foreach (IEntity entity in entities)
            {
                if (entity is IDataOwner dataOwner)
                {
                    HashSet<object> visited = new HashSet<object>(ReferenceComparer<object>.Instance);
                    CollectEntityReferences(dataOwner.Data, references, visited);
                }
            }

            return references;
        }

        private static void CollectEntityReferences(object value, ISet<EntityReference> references, ISet<object> visited)
        {
            if (value == null)
            {
                return;
            }

            if (value is EntityReference entityReference)
            {
                references.Add(entityReference);
                return;
            }

            if (value is IEntity || value is string || value is Delegate || value is Type)
            {
                return;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is decimal || value is Guid || value is DateTime || value is TimeSpan)
            {
                return;
            }

            if (type.IsValueType == false && visited.Add(value) == false)
            {
                return;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    CollectEntityReferences(entry.Key, references, visited);
                    CollectEntityReferences(entry.Value, references, visited);
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    CollectEntityReferences(item, references, visited);
                }

                return;
            }

            string namespaceName = type.Namespace ?? string.Empty;
            if (namespaceName.StartsWith("System", StringComparison.Ordinal) ||
                namespaceName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                namespaceName.StartsWith("UnityEditor", StringComparison.Ordinal))
            {
                return;
            }

            foreach (FieldInfo field in GetInstanceFields(type))
            {
                CollectEntityReferences(field.GetValue(value), references, visited);
            }
        }

        private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
        {
            for (Type currentType = type; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
            {
                foreach (FieldInfo field in currentType
                    .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(field => field.IsStatic == false && field.IsNotSerialized == false))
                {
                    yield return field;
                }
            }
        }
    }
}
