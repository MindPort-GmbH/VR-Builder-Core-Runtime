// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System.Collections;

namespace VRBuilder.Core.EntityOwners.FoldedEntityCollection
{
    /// <summary>
    /// An activating process over an entities' sequence which activates all entities in order.
    /// </summary>
    internal class FoldedActivatingProcess<TEntity> : StageProcess<IEntitySequenceDataWithMode<TEntity>> where TEntity : IEntity
    {
        private IEntity[] children;
        private int currentIndex;

        public FoldedActivatingProcess(IEntitySequenceDataWithMode<TEntity> data) : base(data)
        {
        }

        /// <inheritdoc />
        public override void Start()
        {
            children = RuntimeEntityGraph.GetChildren(Data);
            currentIndex = 0;
        }

        /// <inheritdoc />
        public override IEnumerator Update()
        {
            while (TryMoveNext(out TEntity child))
            {
                Data.Current = child;

                if (Data.Current == null)
                {
                    continue;
                }

                Data.Current.LifeCycle.Activate();

                if (Data.Current.LifeCycle.Stage == Stage.Activating && Data.Mode.CheckIfSkipped(Data.Current.GetType()))
                {
                    Data.Current.LifeCycle.MarkToFastForwardStage(Stage.Activating);
                }

                while (Data.Current.LifeCycle.Stage != Stage.Active)
                {
                    yield return null;
                }
            }
        }

        /// <inheritdoc />
        public override void End()
        {
            children = null;
        }

        /// <inheritdoc />
        public override void FastForward()
        {
            if (Equals(Data.Current, default(IEntity)))
            {
                if (TryMoveNext(out TEntity child))
                {
                    Data.Current = child;
                }
            }

            while (Equals(Data.Current, default(IEntity)) == false)
            {
                if (Data.Current.LifeCycle.Stage == Stage.Inactive)
                {
                    Data.Current.LifeCycle.Activate();
                }

                if (Data.Current.LifeCycle.Stage == Stage.Activating)
                {
                    Data.Current.LifeCycle.MarkToFastForwardStage(Stage.Activating);
                }

                Data.Current = TryMoveNext(out TEntity nextChild) ? nextChild : default;
            }
        }

        private bool TryMoveNext(out TEntity child)
        {
            if (children == null || currentIndex >= children.Length)
            {
                child = default;
                return false;
            }

            child = (TEntity)children[currentIndex++];
            return true;
        }
    }
}
