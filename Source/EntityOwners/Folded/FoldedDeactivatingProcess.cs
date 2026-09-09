// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections;

namespace VRBuilder.Core.EntityOwners.FoldedEntityCollection
{
    /// <summary>
    /// A process over entities' sequence which deactivates entities in an opposite order.
    /// </summary>
    internal class FoldedDeactivatingProcess<TEntity> : StageProcess<IEntitySequenceDataWithMode<TEntity>> where TEntity : IEntity
    {
        private IEntity[] children;
        private int currentIndex;

        public FoldedDeactivatingProcess(IEntitySequenceDataWithMode<TEntity> data) : base(data)
        {
        }

        /// <inheritdoc />
        public override void Start()
        {
            children = RuntimeEntityGraph.GetChildren(Data);
            currentIndex = children.Length - 1;
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

                if (Data.Current.LifeCycle.Stage != Stage.Inactive)
                {
                    Data.Current.LifeCycle.Deactivate();
                }

                if (Data.Current.LifeCycle.Stage == Stage.Deactivating && Data.Mode.CheckIfSkipped(Data.Current.GetType()))
                {
                    Data.Current.LifeCycle.MarkToFastForwardStage(Stage.Deactivating);
                }

                while (Data.Current.LifeCycle.Stage != Stage.Inactive)
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
            if (Equals(Data.Current, default))
            {
                if (TryMoveNext(out TEntity child))
                {
                    Data.Current = child;
                }
            }

            while (Equals(Data.Current, default) == false)
            {
                if (Data.Current == null)
                {
                    throw new NullReferenceException();
                }

                if (Data.Current.LifeCycle.Stage == Stage.Active)
                {
                    Data.Current.LifeCycle.Deactivate();
                }

                if (Data.Current.LifeCycle.Stage == Stage.Deactivating)
                {
                    Data.Current.LifeCycle.MarkToFastForwardStage(Stage.Deactivating);
                }

                Data.Current = TryMoveNext(out TEntity nextChild) ? nextChild : default;
            }
        }

        private bool TryMoveNext(out TEntity child)
        {
            if (children == null || currentIndex < 0)
            {
                child = default;
                return false;
            }

            child = (TEntity)children[currentIndex--];
            return true;
        }
    }
}
