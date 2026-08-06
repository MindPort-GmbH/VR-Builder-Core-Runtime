// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System.Collections;
using VRBuilder.Core.Configuration.Modes;

namespace VRBuilder.Core.EntityOwners.ParallelEntityCollection
{
    /// <summary>
    /// A process which deactivates a collection of entities in parallel.
    /// </summary>
    internal class ParallelDeactivatingProcess<TCollectionData> : Process<TCollectionData> where TCollectionData : class, IEntityCollectionData, IModeData
    {
        public ParallelDeactivatingProcess(TCollectionData data) : base(data)
        {
        }

        /// <inheritdoc />
        public override void Start()
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                if (Data.Mode.CheckIfSkipped(children[i].GetType()) == false)
                {
                    children[i].LifeCycle.Deactivate();
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerator Update()
        {
            while (HasBlockingChildInStage(Stage.Deactivating))
            {
                yield return null;
            }
        }

        /// <inheritdoc />
        public override void End()
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].LifeCycle.Stage != Stage.Inactive)
                {
                    children[i].LifeCycle.MarkToFastForward();
                }
            }
        }

        /// <inheritdoc />
        public override void FastForward()
        {
        }
    }
}
