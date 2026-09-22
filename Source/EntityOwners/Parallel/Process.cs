// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using VRBuilder.Core.Behaviors;
using VRBuilder.Core.Configuration.Modes;

namespace VRBuilder.Core.EntityOwners.ParallelEntityCollection
{
    /// <summary>
    /// A base process for entity collection.
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    internal abstract class Process<TData> : Core.StageProcess<TData> where TData : class, IEntityCollectionData, IModeData
    {
        /// <summary>
        /// Returns whether a blocking child is currently in the requested stage.
        /// </summary>
        protected bool HasBlockingChildInStage(Stage stage)
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                IEntity child = children[i];
                if (Data.Mode.CheckIfSkipped(child.GetType()) || child.LifeCycle.Stage != stage)
                {
                    continue;
                }

                if (child is IDataOwner dataOwner && dataOwner.Data is IBackgroundBehaviorData blockingData && blockingData.IsBlocking == false)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        protected Process(TData data) : base(data)
        {
        }
    }
}
