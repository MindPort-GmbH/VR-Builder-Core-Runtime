// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Resolves references from a source entity graph to a copied entity graph.
    /// </summary>
    public interface IEntityCloneContext
    {
        /// <summary>
        /// Returns the regenerated identifier for an entity owned by the copied graph.
        /// Identifiers outside the copied graph are returned unchanged.
        /// </summary>
        Guid RemapId(Guid sourceId);

        /// <summary>
        /// Tries to get the copy of an entity owned by the source graph.
        /// </summary>
        bool TryGetCopy<TEntity>(TEntity source, out TEntity copy) where TEntity : class, IEntity;
    }
}
