// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections.Generic;
using System.Linq;
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
        private sealed class ReferenceComparer : IEqualityComparer<IEntity>
        {
            public static ReferenceComparer Instance { get; } = new ReferenceComparer();

            public bool Equals(IEntity left, IEntity right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(IEntity entity)
            {
                return RuntimeHelpers.GetHashCode(entity);
            }
        }

        private sealed class CloneContext : IEntityCloneContext
        {
            private readonly IReadOnlyDictionary<IEntity, IEntity> copiesBySource;
            private readonly IReadOnlyDictionary<Guid, Guid> copiedIdsBySourceId;

            public CloneContext(IReadOnlyDictionary<IEntity, IEntity> copiesBySource, IReadOnlyDictionary<Guid, Guid> copiedIdsBySourceId)
            {
                this.copiesBySource = copiesBySource;
                this.copiedIdsBySourceId = copiedIdsBySourceId;
            }

            public Guid RemapId(Guid sourceId)
            {
                return copiedIdsBySourceId.TryGetValue(sourceId, out Guid copiedId) ? copiedId : sourceId;
            }

            public bool TryGetCopy<TEntity>(TEntity source, out TEntity copy) where TEntity : class, IEntity
            {
                if (source != null && copiesBySource.TryGetValue(source, out IEntity copiedEntity) && copiedEntity is TEntity typedCopy)
                {
                    copy = typedCopy;
                    return true;
                }

                copy = null;
                return false;
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

            Dictionary<IEntity, IEntity> copiesBySource = new Dictionary<IEntity, IEntity>(ReferenceComparer.Instance);
            foreach (IEntity sourceEntity in sourceEntities)
            {
                copiesBySource.Add(sourceEntity, copiedEntitiesByOldId[sourceEntity.Id]);
            }

            Dictionary<Guid, Guid> copiedIdsBySourceId = new Dictionary<Guid, Guid>();
            HashSet<Guid> regeneratedIds = new HashSet<Guid>();

            foreach (IEntity sourceEntity in sourceEntities)
            {
                IEntity copiedEntity = copiesBySource[sourceEntity];
                copiedEntity.RegenerateId();

                if (copiedEntity.Id == Guid.Empty || sourceEntitiesById.ContainsKey(copiedEntity.Id) || regeneratedIds.Add(copiedEntity.Id) == false)
                {
                    throw new InvalidOperationException($"Regenerating the identifier of '{copiedEntity.GetType().FullName}' produced the invalid or conflicting identifier '{copiedEntity.Id}'.");
                }

                copiedIdsBySourceId.Add(sourceEntity.Id, copiedEntity.Id);
            }

            CloneContext context = new CloneContext(copiesBySource, copiedIdsBySourceId);
            foreach (IEntity sourceEntity in sourceEntities)
            {
                if (copiesBySource[sourceEntity] is IEntityReferenceRemapper remapper)
                {
                    remapper.RemapReferencesFrom(sourceEntity, context);
                }
            }

            return copy;
        }

        private static IReadOnlyList<IEntity> GetOwnedEntities(IEntity root)
        {
            List<IEntity> entities = new List<IEntity>();
            HashSet<IEntity> visited = new HashSet<IEntity>(ReferenceComparer.Instance);
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
    }
}
