// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using VRBuilder.Core.Attributes;
using VRBuilder.Core.Configuration.Modes;
using VRBuilder.Core.EntityOwners;
using VRBuilder.Core.EntityOwners.ParallelEntityCollection;
using VRBuilder.Core.Exceptions;
using VRBuilder.Core.Utils;
using VRBuilder.Core.Utils.Logging;

namespace VRBuilder.Core
{
    /// <summary>
    /// A chapter of a process <see cref="Process"/>.
    /// </summary>
    [DataContract(IsReference = true)]
    public class Chapter : Entity<Chapter.EntityData>, IChapter
    {
        /// <summary>
        /// The chapter's data class.
        /// </summary>
        [DataContract(IsReference = true)]
        public class EntityData : EntityCollectionData<IStep>, IChapterData
        {
            /// <inheritdoc />
            [DataMember]
            [HideInProcessInspector]
            public string Name { get; set; }

            /// <summary>
            /// The first step of the chapter.
            /// </summary>
            [DataMember]
            public IStep FirstStep { get; set; }

            /// <summary>
            /// All steps of the chapter.
            /// </summary>
            [DataMember]
            public IList<IStep> Steps { get; set; }

            /// <inheritdoc />
            public override IEnumerable<IStep> GetChildren()
            {
                return Steps.ToArray();
            }

            /// <inheritdoc />
            public void SetName(string name)
            {
                Name = name;
            }

            /// <inheritdoc />
            public IMode Mode { get; set; }

            /// <inheritdoc />
            [IgnoreDataMember]
            public IStep Current { get; set; }

            /// <inheritdoc />
            IEntity IEntitySequenceData.Current => Current;
        }

        private class ActivatingProcess : EntityIteratingProcess<IEntitySequenceDataWithMode<IStep>, IStep>
        {
            private readonly IStep firstStep;
            private bool hasStarted;

            public ActivatingProcess(IChapterData data) : base(data)
            {
                firstStep = data.FirstStep;
            }

            /// <inheritdoc />
            public override void Start()
            {
                hasStarted = false;
                base.Start();
            }

            /// <inheritdoc />
            protected override bool ShouldActivateCurrent()
            {
                return true;
            }

            /// <inheritdoc />
            protected override bool ShouldDeactivateCurrent()
            {
                return FindCompletedTransition(Data.Current) != null;
            }

            /// <inheritdoc />
            public override void End()
            {
                base.End();
            }

            /// <inheritdoc />
            protected override bool TryNext(out IStep entity)
            {
                if (hasStarted == false)
                {
                    hasStarted = true;
                    entity = firstStep;
                }
                else
                {
                    ITransition transition = FindCompletedTransition(Data.Current);
                    entity = transition?.Data.TargetStepReference.Entity;
                }

                return entity != null;
            }

            /// <inheritdoc />
            public override void FastForward()
            {
                if (Data.Current == null)
                {
                    return;
                }

                if (Data.Current.FindPathInGraph(step => step.Data.Transitions.Data.Transitions.Select(transition => transition.Data.TargetStepReference.Entity), null, out IList<IStep> pathToChapterEnd) == false)
                {
                    throw new InvalidStateException("The end of the chapter is not reachable from the current step.");
                }

                foreach (IStep step in pathToChapterEnd)
                {
                    if (Data.Current.LifeCycle.Stage == Stage.Inactive)
                    {
                        Data.Current.LifeCycle.Activate();
                    }

                    Data.Current.LifeCycle.MarkToFastForward();

                    ITransition toAutocomplete = FindTransitionTo(Data.Current, step);
                    if (toAutocomplete.IsCompleted == false)
                    {
                        toAutocomplete.Autocomplete();
                    }

                    Data.Current.LifeCycle.Deactivate();

                    Data.Current = step;
                }
            }

            private static ITransition FindCompletedTransition(IStep step)
            {
                if (step == null)
                {
                    return null;
                }

                IEntity[] transitions = RuntimeEntityGraph.GetChildren(step.Data.Transitions.Data);
                for (int i = 0; i < transitions.Length; i++)
                {
                    ITransition transition = (ITransition)transitions[i];
                    if (transition.IsCompleted)
                    {
                        return transition;
                    }
                }

                return null;
            }

            private static ITransition FindTransitionTo(IStep source, IStep target)
            {
                IEntity[] transitions = RuntimeEntityGraph.GetChildren(source.Data.Transitions.Data);
                for (int i = 0; i < transitions.Length; i++)
                {
                    ITransition transition = (ITransition)transitions[i];
                    if (transition.Data.TargetStepReference.Entity == target)
                    {
                        return transition;
                    }
                }

                throw new InvalidOperationException("No transition to the requested step was found.");
            }
        }

        /// <inheritdoc />
        [DataMember]
        public ChapterMetadata ChapterMetadata { get; set; }

        /// <inheritdoc />
        public override void RegenerateId()
        {
            base.RegenerateId();

            if (ChapterMetadata != null)
            {
                ChapterMetadata.Guid = Id;
            }
        }

        /// <inheritdoc />
        public override IStageProcess GetActivatingProcess()
        {
            return new ActivatingProcess(Data);
        }

        /// <inheritdoc />
        public override IStageProcess GetDeactivatingProcess()
        {
            return new StopEntityIteratingProcess<IStep>(Data);
        }

        /// <inheritdoc />
        public override IStageProcess GetAbortingProcess()
        {
            return new ParallelAbortingProcess<EntityData>(Data);
        }

        /// <inheritdoc />
        protected override IConfigurator GetConfigurator()
        {
            return new SequenceConfigurator<IStep>(Data);
        }

        /// <inheritdoc />
        IChapterData IDataOwner<IChapterData>.Data
        {
            get { return Data; }
        }

        protected Chapter() : this(null, null)
        {
        }

        public Chapter(string name, IStep firstStep)
        {
            ChapterMetadata = new ChapterMetadata();
            ChapterMetadata.Guid = Id;

            Data.Name = name;
            Data.FirstStep = firstStep;
            Data.Steps = new List<IStep>();

            if (firstStep != null)
            {
                Data.Steps.Add(firstStep);
            }

            if (LifeCycleLoggingConfig.Instance.LogChapters)
            {
                LifeCycle.StageChanged += (sender, args) =>
                {
                    Debug.LogFormat("<b>Chapter</b> <i>'{0}'</i> is <b>{1}</b>.\n", Data.Name, LifeCycle.Stage.ToString());
                };
            }
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            ChapterMetadata = ChapterMetadata ?? new ChapterMetadata();

            if (ChapterMetadata.Guid == Guid.Empty)
            {
                ChapterMetadata.Guid = Id;
            }
            else
            {
                SetId(ChapterMetadata.Guid);
            }
        }
    }
}
