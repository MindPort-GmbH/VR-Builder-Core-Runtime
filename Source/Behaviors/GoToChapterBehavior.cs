using Newtonsoft.Json;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine.Scripting;
using VRBuilder.Core.Attributes;
using VRBuilder.Core.Cloning;

namespace VRBuilder.Core.Behaviors
{
    /// <summary>
    /// This behavior sets the next chapter to an arbitrary chapter and immediately aborts the current chapter.
    /// </summary>
    [DataContract(IsReference = true)]
    public class GoToChapterBehavior : Behavior<GoToChapterBehavior.EntityData>, IEntityReferenceRemapper
    {
        /// <summary>
        /// Behavior data.
        /// </summary>
        [DisplayName("Go to Chapter")]
        [DataContract(IsReference = true)]
        public class EntityData : IBehaviorData
        {
            [DataMember]
            [DisplayName("Chapter")]
            [DisplayTooltip("Chapter to jump to. The current chapter is aborted immediately.")]
            public Guid ChapterGuid { get; set; }

            public Metadata Metadata { get; set; }

            [IgnoreDataMember]
            public string Name => "Go to Chapter";
        }

        [JsonConstructor, Preserve]
        public GoToChapterBehavior() : this(Guid.Empty)
        {
        }

        public GoToChapterBehavior(Guid chapterGuid)
        {
            Data.ChapterGuid = chapterGuid;
        }

        private class ActivatingProcess : StageProcess<EntityData>
        {
            public ActivatingProcess(EntityData data) : base(data)
            {
            }

            /// <inheritdoc />
            public override void Start()
            {
                if (Data.ChapterGuid == Guid.Empty)
                {
                    return;
                }

                IChapter chapter = ProcessRunner.Current.Data.Chapters.FirstOrDefault(chapter => chapter.ChapterMetadata.Guid == Data.ChapterGuid);

                if (chapter != null)
                {
                    ProcessRunner.SetNextChapter(chapter);
                }

                ProcessRunner.Current.Data.Current?.LifeCycle.Abort();
            }

            /// <inheritdoc />
            public override IEnumerator Update()
            {
                yield return null;
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

        /// <inheritdoc />
        public void RemapReferencesFrom(IEntity source, IEntityCloneContext context)
        {
            if (source is not GoToChapterBehavior sourceBehavior)
            {
                throw new ArgumentException($"Expected a source of type '{typeof(GoToChapterBehavior).FullName}'.", nameof(source));
            }

            Data.ChapterGuid = context.RemapId(sourceBehavior.Data.ChapterGuid);
        }
    }
}
