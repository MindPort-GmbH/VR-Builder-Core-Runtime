using System.Collections;

namespace VRBuilder.Core.EntityOwners.ParallelEntityCollection
{
    /// <summary>
    /// A process over a collection of entities which aborts them at the same time, in parallel.
    /// </summary>
    internal class ParallelAbortingProcess<TCollectionData> : StageProcess<TCollectionData> where TCollectionData : class, IEntityCollectionData
    {
        public ParallelAbortingProcess(TCollectionData data) : base(data)
        {
        }

        public override void End()
        {
        }

        public override void FastForward()
        {
        }

        /// <inheritdoc />
        public override void Start()
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].LifeCycle.Stage != Stage.Inactive)
                {
                    children[i].LifeCycle.Abort();
                }
            }
        }

        public override IEnumerator Update()
        {
            while (HasActiveChild())
            {
                yield return null;
            }
        }

        private bool HasActiveChild()
        {
            IEntity[] children = RuntimeEntityGraph.GetChildren(Data);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].LifeCycle.Stage != Stage.Inactive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
