// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Runtime.Serialization;

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Base class for references that can be remapped when their owning entity graph is copied.
    /// </summary>
    [DataContract(IsReference = true)]
    public abstract class EntityReference
    {
        internal abstract void RemapFrom(EntityReference source, IEntityCloneContext context);
    }

    /// <summary>
    /// Stores a reference to an entity by identifier, object, or both.
    /// </summary>
    /// <typeparam name="TEntity">Type of referenced entity.</typeparam>
    [DataContract(IsReference = true)]
    public sealed class EntityReference<TEntity> : EntityReference where TEntity : class, IEntity
    {
        [DataMember]
        private Guid id;

        [IgnoreDataMember]
        private TEntity entity;

        /// <summary>
        /// Referenced entity identifier.
        /// </summary>
        public Guid Id => id;

        /// <summary>
        /// Nonserialized referenced entity object, if available at runtime.
        /// </summary>
        public TEntity Entity => entity;

        /// <summary>
        /// Creates an empty reference.
        /// </summary>
        public EntityReference()
        {
        }

        /// <summary>
        /// Creates an identifier-based reference.
        /// </summary>
        public EntityReference(Guid id)
        {
            Set(id);
        }

        /// <summary>
        /// Creates an object-based reference.
        /// </summary>
        public EntityReference(TEntity entity)
        {
            Set(entity);
        }

        /// <summary>
        /// Replaces this reference with an identifier-based reference.
        /// </summary>
        public void Set(Guid referencedId)
        {
            id = referencedId;
            entity = null;
        }

        /// <summary>
        /// Replaces this reference with an object-based reference.
        /// </summary>
        public void Set(TEntity referencedEntity)
        {
            entity = referencedEntity;
            id = referencedEntity?.Id ?? Guid.Empty;
        }

        internal override void RemapFrom(EntityReference source, IEntityCloneContext context)
        {
            if (source is not EntityReference<TEntity> typedSource)
            {
                throw new ArgumentException($"Expected a reference of type '{typeof(EntityReference<TEntity>).FullName}'.", nameof(source));
            }

            if (typedSource.Entity != null)
            {
                Set(context.TryGetCopy(typedSource.Entity, out TEntity copiedEntity) ? copiedEntity : typedSource.Entity);
            }
            else
            {
                Set(context.RemapId(typedSource.Id));
            }
        }
    }
}
