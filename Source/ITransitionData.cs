// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using System;
using System.Collections.Generic;
using VRBuilder.Core.Cloning;
using VRBuilder.Core.Conditions;
using VRBuilder.Core.EntityOwners;

namespace VRBuilder.Core
{
    // An interface for a transition's data.
    public interface ITransitionData : IEntityCollectionDataWithMode<ICondition>, ICompletableData, INamedData
    {
        /// <summary>
        /// A list of conditions. When you complete all of them, this transition will trigger.
        /// </summary>
        IList<ICondition> Conditions { get; set; }

        /// <summary>
        /// The next step to take after this transition triggers.
        /// </summary>
        [Obsolete("Use TargetStepReference instead.")]
        IStep TargetStep { get; set; }

        /// <summary>
        /// A clone-aware reference to the next step to take after this transition triggers.
        /// </summary>
        EntityReference<IStep> TargetStepReference { get; }
    }
}
