// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using VRBuilder.Core.Behaviors;
using VRBuilder.Core.Configuration.Modes;
using VRBuilder.Core.EntityOwners;

namespace VRBuilder.Core
{
    /// <summary>
    /// Provides the identity shared by all process entities.
    /// </summary>
    [DataContract(IsReference = true)]
    public abstract class EntityBase
    {
        /// <summary>
        /// Unique identifier of the entity.
        /// </summary>
        [DataMember]
        public Guid Id { get; private set; }

        protected EntityBase()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Sets the entity identifier during migration from legacy metadata.
        /// </summary>
        protected void SetId(Guid id)
        {
            Id = id;
        }

        internal void SetEntityId(Guid id)
        {
            Id = id;
        }
    }

    /// <summary>
    /// Abstract helper class that can be used for instances that implement <see cref="IEntity"/>. Provides implementation of the events and properties, and also
    /// offers member functions to trigger state changes.
    /// </summary>
    [DataContract(IsReference = true)]
    public abstract class Entity<TData> : EntityBase, IEntity, IDataOwner<TData> where TData : class, IData, new()
    {
        /// <inheritdoc />
        [DataMember]
        public TData Data { get; private set; }

        /// <inheritdoc />
        IData IDataOwner.Data
        {
            get { return ((IDataOwner<TData>)this).Data; }
        }

        /// <inheritdoc />
        [IgnoreDataMember]
        public ILifeCycle LifeCycle { get; }

        /// <inheritdoc />
        [IgnoreDataMember]
        public IEntity Parent { get; set; }

        protected Entity()
        {
            LifeCycle = new LifeCycle(this);
            Data = new TData();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetActivatingProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetActiveProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetDeactivatingProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetAbortingProcess()
        {
            return new EmptyProcess();
        }

        /// <summary>
        /// Override this method if your behavior or condition supports changing between process modes (<see cref="IMode"/>).
        /// By default returns an empty configurator that does nothing.
        /// </summary>
        protected virtual IConfigurator GetConfigurator()
        {
            return new EmptyConfigurator();
        }

        /// <inheritdoc />
        public virtual void Configure(IMode mode)
        {
            if (Data is IEntityCollectionData collectionData)
            {
                foreach (IEntity child in collectionData.GetChildren().Distinct())
                {
                    child.Parent = this;
                    child.Configure(mode);
                }
            }

            GetConfigurator().Configure(mode, LifeCycle.Stage);

            if (Data is IModeData modeData)
            {
                modeData.Mode = mode;
            }
        }

        /// <inheritdoc />
        public void Update()
        {
            LifeCycle.Update();

            // IStepData implements IEntitySequenceData despite not being a sequence,
            // so we have to manually exclude it.
            if (Data is IEntitySequenceData sequenceData && Data is IStepData == false)
            {
                sequenceData.Current?.Update();
            }
            else if (Data is IEntityCollectionData collectionData)
            {
                foreach (IEntity child in collectionData.GetChildren().Distinct())
                {
                    child.Update();
                }
            }
        }
    }

    /// <summary>
    /// Utilities for assigning fresh identifiers to copied entity trees.
    /// </summary>
    public static class EntityCopyUtils
    {
        /// <summary>
        /// Assigns fresh identifiers to the supplied entity and all entities it owns.
        /// References to chapters contained in the same tree are updated to their new identifiers.
        /// </summary>
        public static void RegenerateIds(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            IList<IEntity> entities = GetEntityTree(entity);
            Dictionary<Guid, Guid> idMap = new Dictionary<Guid, Guid>();

            foreach (IEntity child in entities)
            {
                Guid previousId = child.Id;
                Guid newId = Guid.NewGuid();

                if (child is EntityBase mutableEntity)
                {
                    mutableEntity.SetEntityId(newId);
                    idMap[previousId] = newId;
                    SynchronizeMetadata(child);
                }
            }

            RemapChapterReferences(entities, idMap);
        }

        internal static void FinalizeCopy(IEntity source, IEntity copy)
        {
            if (source == null || copy == null)
            {
                return;
            }

            IList<IEntity> sourceEntities = GetEntityTree(source);
            IList<IEntity> copiedEntities = GetEntityTree(copy);
            Dictionary<Guid, Guid> idMap = new Dictionary<Guid, Guid>();
            int pairedEntityCount = Math.Min(sourceEntities.Count, copiedEntities.Count);

            for (int index = 0; index < copiedEntities.Count; index++)
            {
                IEntity copiedEntity = copiedEntities[index];
                Guid copiedId = copiedEntity.Id;
                Guid newId = Guid.NewGuid();

                if (copiedEntity is EntityBase mutableEntity)
                {
                    mutableEntity.SetEntityId(newId);
                    idMap[copiedId] = newId;

                    if (index < pairedEntityCount)
                    {
                        idMap[sourceEntities[index].Id] = newId;
                    }

                    SynchronizeMetadata(copiedEntity);
                }
            }

            RemapChapterReferences(copiedEntities, idMap);
        }

        private static IList<IEntity> GetEntityTree(IEntity root)
        {
            List<IEntity> entities = new List<IEntity>();
            HashSet<IEntity> visited = new HashSet<IEntity>();
            CollectEntities(root, entities, visited);
            return entities;
        }

        private static void CollectEntities(IEntity entity, ICollection<IEntity> entities, ISet<IEntity> visited)
        {
            if (entity == null || visited.Add(entity) == false)
            {
                return;
            }

            entities.Add(entity);

            if (entity is IDataOwner dataOwner && dataOwner.Data is IEntityCollectionData collectionData)
            {
                IEnumerable<IEntity> children = collectionData.GetChildren();
                if (children == null)
                {
                    return;
                }

                foreach (IEntity child in children)
                {
                    CollectEntities(child, entities, visited);
                }
            }
        }

        private static void SynchronizeMetadata(IEntity entity)
        {
            if (entity is IStep step && step.StepMetadata != null)
            {
                step.StepMetadata.Guid = entity.Id;
            }

            if (entity is IChapter chapter && chapter.ChapterMetadata != null)
            {
                chapter.ChapterMetadata.Guid = entity.Id;
            }

            if (entity is IProcess process && process.ProcessMetadata != null)
            {
                process.ProcessMetadata.Guid = entity.Id;
            }
        }

        private static void RemapChapterReferences(IEnumerable<IEntity> entities, IReadOnlyDictionary<Guid, Guid> idMap)
        {
            foreach (GoToChapterBehavior behavior in entities.OfType<GoToChapterBehavior>())
            {
                if (idMap.TryGetValue(behavior.Data.ChapterGuid, out Guid copiedChapterId))
                {
                    behavior.Data.ChapterGuid = copiedChapterId;
                }
            }
        }
    }
}
