// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using VRBuilder.Core.Attributes;
using VRBuilder.Core.Cloning;
using VRBuilder.Core.Conditions;
using VRBuilder.Core.Configuration.Modes;
using VRBuilder.Core.EntityOwners;
using VRBuilder.Core.EntityOwners.ParallelEntityCollection;
using VRBuilder.Core.RestrictiveEnvironment;
using VRBuilder.Core.Utils.Logging;
using VRBuilder.Unity;

namespace VRBuilder.Core
{
    /// <summary>
    /// A class for a transition from one step to another.
    /// </summary>
    [DataContract(IsReference = true)]
    public class Transition : CompletableEntity<Transition.EntityData>, ITransition, ILockablePropertiesProvider
    {
        /// <summary>
        /// The transition's data class.
        /// </summary>
        [DisplayName("Transition")]
        public class EntityData : EntityCollectionData<ICondition>, ITransitionData
        {
            ///<inheritdoc />
            [DataMember]
            [DisplayName("Conditions"), Foldable, ReorderableListOf(typeof(FoldableAttribute), typeof(HelpAttribute), typeof(MenuAttribute)), ExtendableList]
            public IList<ICondition> Conditions { get; set; }

            ///<inheritdoc />
            public override IEnumerable<ICondition> GetChildren()
            {
                return Conditions.ToArray();
            }

            /// <summary>
            /// Clone-aware reference to the target step.
            /// </summary>
            [HideInProcessInspector]
            [DataMember]
            public EntityReference<IStep> TargetStepReference { get; } = new EntityReference<IStep>();

            ///<inheritdoc />
            [HideInProcessInspector]
            [DataMember]
            [System.Obsolete("Use TargetStepReference instead.")]
            public IStep TargetStep
            {
                get => TargetStepReference.Entity;
                set => TargetStepReference.Set(value);
            }

            ///<inheritdoc />
            public IMode Mode { get; set; }

            ///<inheritdoc />
            public bool IsCompleted { get; set; }

            [IgnoreDataMember]
            [IgnoreInStepInspector]
            public string Name
            {
                get
                {
                    ICondition condition = Conditions.FirstOrDefault();

                    string additionalConditions = Conditions.Count > 1 ? $" (+{Conditions.Count - 1})" : "";

                    if (condition != null)
                    {
                        string conditionName = condition.Data.Name;

                        if (conditionName.Length > 32)
                        {
                            conditionName = $"{conditionName.Remove(32)}...";
                        }

                        return $"{conditionName}{additionalConditions}";
                    }
                    else
                    {
                        return "";
                    }
                }
            }
        }

        private class ActivatingProcess : InstantProcess<EntityData>
        {
            public ActivatingProcess(EntityData data) : base(data)
            {
            }

            ///<inheritdoc />
            public override void Start()
            {
                Data.IsCompleted = false;
            }
        }

        private class ActiveProcess : BaseActiveProcessOverCompletable<EntityData>
        {
            public ActiveProcess(EntityData data) : base(data)
            {
            }

            ///<inheritdoc />
            protected override bool CheckIfCompleted()
            {
                IEntity[] conditions = RuntimeEntityGraph.GetChildren(Data);
                for (int i = 0; i < conditions.Length; i++)
                {
                    ICondition condition = (ICondition)conditions[i];
                    if (Data.Mode.CheckIfSkipped(condition.GetType()) == false && condition.IsCompleted == false)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private class EntityAutocompleter : Autocompleter<EntityData>
        {
            public EntityAutocompleter(EntityData data) : base(data)
            {
            }

            ///<inheritdoc />
            public override void Complete()
            {
                IEntity[] conditions = RuntimeEntityGraph.GetChildren(Data);
                for (int i = 0; i < conditions.Length; i++)
                {
                    ICondition condition = (ICondition)conditions[i];
                    if (Data.Mode.CheckIfSkipped(condition.GetType()) == false)
                    {
                        condition.Autocomplete();
                    }
                }
            }
        }

        ///<inheritdoc />
        ITransitionData IDataOwner<ITransitionData>.Data
        {
            get { return Data; }
        }

        ///<inheritdoc />
        public override IStageProcess GetActivatingProcess()
        {
            return new CompositeProcess(new EntityOwners.ParallelEntityCollection.ParallelActivatingProcess<EntityData>(Data), new ActivatingProcess(Data));
        }

        ///<inheritdoc />
        public override IStageProcess GetActiveProcess()
        {
            return new CompositeProcess(new EntityOwners.ParallelEntityCollection.ParallelActiveProcess<EntityData>(Data), new ActiveProcess(Data));
        }

        ///<inheritdoc />
        public override IStageProcess GetDeactivatingProcess()
        {
            return new EntityOwners.ParallelEntityCollection.ParallelDeactivatingProcess<EntityData>(Data);
        }

        /// <inheritdoc />
        public override IStageProcess GetAbortingProcess()
        {
            return new ParallelAbortingProcess<EntityData>(Data);
        }

        ///<inheritdoc />
        protected override IConfigurator GetConfigurator()
        {
            return new ParallelConfigurator<ICondition>(Data);
        }

        ///<inheritdoc />
        protected override IAutocompleter GetAutocompleter()
        {
            return new EntityAutocompleter(Data);
        }

        /// <inheritdoc />
        public Transition()
        {
            Data.Conditions = new List<ICondition>();
            Data.TargetStepReference.Set(null);

            if (LifeCycleLoggingConfig.Instance.LogTransitions)
            {
                LifeCycle.StageChanged += (sender, args) =>
                {
                    IStep targetStep = Data.TargetStepReference.Entity;
                    Debug.LogFormat("{0}<b>Transition to</b> <i>{1}</i> is <b>{2}</b>.\n", ConsoleUtils.GetTabs(3), targetStep != null ? targetStep.Data.Name + " (Step)" : "chapter's end", LifeCycle.Stage);
                };
            }
        }

        /// <inheritdoc />
        public IEnumerable<LockablePropertyData> GetLockableProperties()
        {
            IEnumerable<LockablePropertyData> lockable = new List<LockablePropertyData>();
            foreach (ICondition condition in Data.Conditions)
            {
                if (condition is ILockablePropertiesProvider lockableCondition)
                {
                    lockable = lockable.Union(lockableCondition.GetLockableProperties());
                }
            }
            return lockable;
        }

    }
}
