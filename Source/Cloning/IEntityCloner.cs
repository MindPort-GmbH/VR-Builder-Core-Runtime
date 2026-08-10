// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Creates independent copies of entity ownership graphs.
    /// </summary>
    public interface IEntityCloner
    {
        /// <summary>
        /// Creates an independent copy of <paramref name="source"/>.
        /// </summary>
        TEntity Clone<TEntity>(TEntity source) where TEntity : class, IEntity;
    }
}
