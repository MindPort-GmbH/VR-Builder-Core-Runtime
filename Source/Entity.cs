// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Linq;
using System.Runtime.Serialization;
using VRBuilder.Core.Configuration.Modes;
using VRBuilder.Core.EntityOwners;

namespace VRBuilder.Core
{
    /// <summary>
    /// Abstract helper class that can be used for instances that implement <see cref="IEntity"/>. Provides implementation of the events and properties, and also
    /// offers member functions to trigger state changes.
    /// </summary>
    [DataContract(IsReference = true)]
    public abstract class Entity<TData> : IEntity, IDataOwner<TData>, IRuntimeEntity where TData : class, IData, new()
    {
        [IgnoreDataMember]
        private IEntity[] runtimeChildren;

        /// <inheritdoc />
        [DataMember]
        public Guid Id { get; private set; }

        /// <inheritdoc />
        [DataMember]
        public TData Data { get; private set; }

        /// <inheritdoc />
        IData IDataOwner.Data
        {
            get { return ((IDataOwner<TData>)this).Data; }
        }

        /// <inheritdoc />
        [IgnoreDataMember]
        public ILifeCycle LifeCycle { get; }

        /// <inheritdoc />
        [IgnoreDataMember]
        public IEntity Parent { get; set; }

        protected Entity()
        {
            Id = Guid.NewGuid();
            LifeCycle = new LifeCycle(this);
            Data = new TData();
        }

        /// <inheritdoc />
        public virtual void RegenerateId()
        {
            Id = Guid.NewGuid();
        }

        /// <summary>
        /// Sets the entity identifier during migration from legacy metadata.
        /// </summary>
        protected void SetId(Guid id)
        {
            Id = id;
        }

        /// <inheritdoc />
        public virtual IStageProcess GetActivatingProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetActiveProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetDeactivatingProcess()
        {
            return new EmptyProcess();
        }

        /// <inheritdoc />
        public virtual IStageProcess GetAbortingProcess()
        {
            return new EmptyProcess();
        }

        /// <summary>
        /// Override this method if your behavior or condition supports changing between process modes (<see cref="IMode"/>).
        /// By default returns an empty configurator that does nothing.
        /// </summary>
        protected virtual IConfigurator GetConfigurator()
        {
            return new EmptyConfigurator();
        }

        /// <inheritdoc />
        public virtual void Configure(IMode mode)
        {
            if (Data is IEntityCollectionData collectionData)
            {
                if (runtimeChildren == null)
                {
                    foreach (IEntity child in collectionData.GetChildren().Distinct())
                    {
                        child.Parent = this;
                        child.Configure(mode);
                    }
                }
                else
                {
                    for (int i = 0; i < runtimeChildren.Length; i++)
                    {
                        IEntity child = runtimeChildren[i];
                        child.Parent = this;
                        child.Configure(mode);
                    }
                }
            }

            GetConfigurator().Configure(mode, LifeCycle.Stage);

            if (Data is IModeData modeData)
            {
                modeData.Mode = mode;
            }
        }

        /// <inheritdoc />
        public void Update()
        {
            LifeCycle.Update();

            // IStepData implements IEntitySequenceData despite not being a sequence,
            // so we have to manually exclude it.
            if (Data is IEntitySequenceData sequenceData && Data is IStepData == false)
            {
                sequenceData.Current?.Update();
            }
            else if (Data is IEntityCollectionData collectionData)
            {
                if (runtimeChildren == null)
                {
                    foreach (IEntity child in collectionData.GetChildren().Distinct())
                    {
                        child.Update();
                    }
                }
                else
                {
                    for (int i = 0; i < runtimeChildren.Length; i++)
                    {
                        runtimeChildren[i].Update();
                    }
                }
            }
        }

        bool IRuntimeEntity.IsRuntimeGraphPrepared => runtimeChildren != null;

        IEntity[] IRuntimeEntity.RuntimeChildren => runtimeChildren ?? System.Array.Empty<IEntity>();

        void IRuntimeEntity.PrepareRuntimeGraph()
        {
            if (runtimeChildren != null)
            {
                return;
            }

            if (Data is IEntityCollectionData collectionData)
            {
                IRuntimeEntityCollectionData runtimeData = Data as IRuntimeEntityCollectionData;
                if (runtimeData != null && runtimeData.IsRuntimeGraphPrepared)
                {
                    runtimeChildren = runtimeData.RuntimeChildren;
                }
                else
                {
                    runtimeChildren = RuntimeEntityGraph.Snapshot(collectionData);
                    runtimeData?.SetRuntimeChildren(runtimeChildren);
                }
            }
            else
            {
                runtimeChildren = System.Array.Empty<IEntity>();
            }

            for (int i = 0; i < runtimeChildren.Length; i++)
            {
                RuntimeEntityGraph.Prepare(runtimeChildren[i]);
            }
        }

    }
}
