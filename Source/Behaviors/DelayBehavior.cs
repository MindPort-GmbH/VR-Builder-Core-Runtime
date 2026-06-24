using UnityEngine;
using System.Collections;
using System.Runtime.Serialization;
using VRBuilder.Core.Attributes;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace VRBuilder.Core.Behaviors
{
    /// <summary>
    /// Behavior that waits for `DelayTime` seconds before finishing its activation.
    /// </summary>
    [DataContract(IsReference = true)]
    [HelpLink("https://mindport-gmbh.github.io/VR-Builder-Documentation/articles/core/delay-behavior.html?utm_source=unity_editor&utm_medium=referral&utm_campaign=from_unity&utm_id=from_unity")]
    public class DelayBehavior : Behavior<DelayBehavior.EntityData>
    {
        /// <summary>
        /// The data class for a delay behavior.
        /// </summary>
        [DisplayName("Delay")]
        [DataContract(IsReference = true)]
        public class EntityData : IBehaviorData
        {
            [DataMember]
            [DisplayName("Delay")]
            [DisplayTooltip("Delay before the behavior completes, in seconds.")]
            public float DelayTime { get; set; }

            public Metadata Metadata { get; set; }

            [IgnoreDataMember]
            public string Name
            {
                get
                {
                    return $"Wait for {DelayTime} seconds";
                }
            }
        }

        [JsonConstructor, Preserve]
        public DelayBehavior() : this(0)
        {
        }

        public DelayBehavior(float delayTime)
        {
            if (delayTime < 0f)
            {
                Debug.LogWarningFormat("DelayTime has to be zero or positive, but it was {0}. Setting to 0 instead.", delayTime);
                delayTime = 0f;
            }

            Data.DelayTime = delayTime;
        }

        private class ActivatingProcess : StageProcess<EntityData>
        {
            public ActivatingProcess(EntityData data) : base(data)
            {
            }

            /// <inheritdoc />
            public override void Start()
            {
            }

            /// <inheritdoc />
            public override IEnumerator Update()
            {
                float timeStarted = Time.time;

                while (Time.time - timeStarted < Data.DelayTime)
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
            }
        }

        /// <inheritdoc />
        public override IStageProcess GetActivatingProcess()
        {
            return new ActivatingProcess(Data);
        }
    }
}
