// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

namespace VRBuilder.Core.Cloning
{
    /// <summary>
    /// Implemented by entities that store references to other entities.
    /// </summary>
    public interface IEntityReferenceRemapper
    {
        /// <summary>
        /// Remaps references on this copied entity using the corresponding source entity.
        /// </summary>
        void RemapReferencesFrom(IEntity source, IEntityCloneContext context);
    }
}
