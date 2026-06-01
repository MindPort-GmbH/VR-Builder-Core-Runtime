using System.Runtime.Serialization;
using VRBuilder.Core.Attributes;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace VRBuilder.Core.Conditions
{
    /// <summary>
    /// A condition that completes when a certain amount of time has passed.
    /// </summary>
    [DataContract(IsReference = true)]
    [HelpLink("https://mindport-gmbh.github.io/VR-Builder-Documentation/articles/core/timeout-condition.html?utm_source=unity_editor&utm_medium=referral&utm_campaign=from_unity&utm_id=from_unity")]
    public class TimeoutCondition : Condition<TimeoutCondition.EntityData>
    {
        /// <summary>
        /// The data for timeout condition.
        /// </summary>
        [DisplayName("Timeout")]
        public class EntityData : IConditionData
        {
            /// <summary>
            /// The delay before the condition completes.
            /// </summary>
            [DataMember]
            [DisplayName("Wait (in seconds)")]
            public float Timeout { get; set; }

            /// <inheritdoc />
            public bool IsCompleted { get; set; }

            /// <inheritdoc />
            [IgnoreDataMember]
            [HideInProcessInspector]
            public string Name
            {
                get
                {
                    return $"Complete after {Timeout.ToString()} seconds";
                }
            }

            /// <inheritdoc />
            public Metadata Metadata { get; set; }
        }

        private class ActiveProcess : BaseActiveProcessOverCompletable<EntityData>
        {
            public ActiveProcess(EntityData data) : base(data)
            {
            }

            private float timeStarted;

            /// <inheritdoc />
            protected override bool CheckIfCompleted()
            {
                return Time.time - timeStarted >= Data.Timeout;
            }

            /// <inheritdoc />
            public override void Start()
            {
                timeStarted = Time.time;
                base.Start();
            }
        }

        [JsonConstructor, Preserve]
        public TimeoutCondition() : this(0)
        {
        }

        public TimeoutCondition(float timeout)
        {
            Data.Timeout = timeout;
        }

        /// <inheritdoc />
        public override IStageProcess GetActiveProcess()
        {
            return new ActiveProcess(Data);
        }
    }
}
