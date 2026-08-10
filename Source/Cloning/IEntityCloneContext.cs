// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Resolves references from a source entity graph to a copied entity graph.
    /// </summary>
    internal interface IEntityCloneContext
    {
        /// <summary>
        /// Returns the regenerated identifier for an entity owned by the copied graph.
        /// Identifiers outside the copied graph are returned unchanged.
        /// </summary>
        Guid RemapId(Guid sourceId);

        /// <summary>
        /// Resolves an object-backed reference to either the copied owned entity or the original external entity.
        /// </summary>
        bool TryResolveEntity<TEntity>(Guid sourceId, out TEntity entity) where TEntity : class, IEntity;
    }
}
