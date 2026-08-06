// Copyright (c) 2026 MindPort GmbH

using System;
using System.Linq;
using VRBuilder.Core.EntityOwners;

namespace VRBuilder.Core
{
    /// <summary>
    /// Internal contract implemented by entities which support a prepared runtime graph.
    /// </summary>
    internal interface IRuntimeEntity
    {
        bool IsRuntimeGraphPrepared { get; }

        IEntity[] RuntimeChildren { get; }

        void PrepareRuntimeGraph();
    }

    /// <summary>
    /// Internal contract implemented by collection data which can expose its owning entity's runtime snapshot.
    /// </summary>
    internal interface IRuntimeEntityCollectionData
    {
        bool IsRuntimeGraphPrepared { get; }

        IEntity[] RuntimeChildren { get; }

        void SetRuntimeChildren(IEntity[] children);
    }

    /// <summary>
    /// Prepares and provides access to the immutable structural snapshot used while a process is running.
    /// </summary>
    internal static class RuntimeEntityGraph
    {
        /// <summary>
        /// Recursively snapshots the complete, unfiltered entity topology. Repeated calls are idempotent.
        /// </summary>
        public static void Prepare(IEntity root)
        {
            (root as IRuntimeEntity)?.PrepareRuntimeGraph();
        }

        /// <summary>
        /// Returns the prepared children when available. Direct custom collection-data implementations retain
        /// a live fallback, although that fallback is not allocation-free.
        /// </summary>
        public static IEntity[] GetChildren(IEntityCollectionData data)
        {
            if (data is IRuntimeEntityCollectionData runtimeData && runtimeData.IsRuntimeGraphPrepared)
            {
                return runtimeData.RuntimeChildren;
            }

            return data.GetChildren().ToArray();
        }

        /// <summary>
        /// Materializes a collection with the same stable first-occurrence semantics as Distinct().
        /// </summary>
        public static IEntity[] Snapshot(IEntityCollectionData data)
        {
            return data.GetChildren().Distinct().ToArray();
        }
    }
}
