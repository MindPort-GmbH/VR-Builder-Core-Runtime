// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System.Collections;
using VRBuilder.Core.Configuration.Modes;

namespace VRBuilder.Core.EntityOwners.ParallelEntityCollection
{
    /// <summary>
    /// A process over a collection of entities which activates them at the same time, in parallel.
    /// </summary>
    internal class ParallelActivatingProcess<TCollectionData> : Process<TCollectionData> where TCollectionData : class, IEntityCollectionData, IModeData
    {
        public ParallelActivatingProcess(TCollectionData data) : base(data)
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
                    children[i].LifeCycle.Activate();
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerator Update()
        {
            while (HasBlockingChildInStage(Stage.Activating))
            {
                yield return null;
            }
        }

        /// <inheritdoc />
        public override void End()
        {
        }

        /// <inheritdoc />
        public override void FastForward()
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].LifeCycle.Stage == Stage.Activating)
                {
                    children[i].LifeCycle.MarkToFastForwardStage(Stage.Activating);
                }
            }
        }
    }
}
