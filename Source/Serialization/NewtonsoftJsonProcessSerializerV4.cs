// Copyright (c) 2013-2019 Innoactive GmbH
// Licensed under the Apache License, Version 2.0
// Modifications copyright (c) 2021-2026 MindPort GmbH

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using UnityEngine;
using VRBuilder.Core.Behaviors;
using VRBuilder.Core.Configuration.Modes;
using VRBuilder.Core.EntityOwners;
using VRBuilder.Core.Serialization.NewtonsoftJson;

namespace VRBuilder.Core.Serialization
{
    /// <summary>
    /// Improved version of the NewtonsoftJsonProcessSerializer, which now flattens nested subchapters.
    /// </summary>
    public class NewtonsoftJsonProcessSerializerV4 : NewtonsoftJsonProcessSerializer
    {
        /// <inheritdoc/>
        public override string Name { get; } = "Newtonsoft Json Importer v4";

        protected override int Version { get; } = 4;

        /// <inheritdoc/>
        public override IProcess ProcessFromByteArray(byte[] data)
        {
            string stringData = new UTF8Encoding().GetString(data);
            JObject dataObject = JsonConvert.DeserializeObject<JObject>(stringData, ProcessSerializerSettings);

            // Check if process was serialized with a previous version.
            int version = dataObject.GetValue("$serializerVersion").ToObject<int>();
            if (version == 1)
            {
                return base.ProcessFromByteArray(data);
            }
            if (version == 2)
            {
                return new ImprovedNewtonsoftJsonProcessSerializer().ProcessFromByteArray(data);
            }
            if (version == 3)
            {
                return new NewtonsoftJsonProcessSerializerV3().ProcessFromByteArray(data);
            }

            ProcessWrapper wrapper = Deserialize<ProcessWrapper>(data, ProcessSerializerSettings);
            return wrapper.GetProcess();
        }

        /// <inheritdoc/>
        public override byte[] ProcessToByteArray(IProcess process)
        {
            ProcessWrapper wrapper = new ProcessWrapper(process);
            byte[] bytes = null;

            try
            {
                JObject jObject = JObject.FromObject(wrapper, JsonSerializer.Create(ProcessSerializerSettings));
                jObject.Add("$serializerVersion", Version);
                bytes = new UTF8Encoding().GetBytes(jObject.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
            finally
            {
                // This line is required to undo the changes applied to the process.
                wrapper.GetProcess();
            }

            return bytes;
        }

        /// <inheritdoc/>
        public override IChapter ChapterFromByteArray(byte[] data)
        {
            string stringData = new UTF8Encoding().GetString(data);
            JObject dataObject = JsonConvert.DeserializeObject<JObject>(stringData, ProcessSerializerSettings);

            // Check if process was serialized with version 1
            int version = dataObject.GetValue("$serializerVersion").ToObject<int>();
            if (version == 1)
            {
                return base.ChapterFromByteArray(data);
            }
            if (version == 2)
            {
                return new ImprovedNewtonsoftJsonProcessSerializer().ChapterFromByteArray(data);
            }
            if (version == 3)
            {
                return new NewtonsoftJsonProcessSerializerV3().ChapterFromByteArray(data);
            }

            ChapterWrapper wrapper = Deserialize<ChapterWrapper>(data, ProcessSerializerSettings);
            return wrapper.GetChapter();
        }

        /// <inheritdoc/>
        public override byte[] ChapterToByteArray(IChapter chapter)
        {
            ChapterWrapper wrapper = new ChapterWrapper(chapter);
            byte[] bytes = null;

            try
            {
                JObject jObject = JObject.FromObject(wrapper, JsonSerializer.Create(ProcessSerializerSettings));
                jObject.Add("$serializerVersion", Version);
                bytes = new UTF8Encoding().GetBytes(jObject.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
            finally
            {
                // This line is required to undo the changes applied to the process.
                wrapper.GetChapter();
            }

            return bytes;
        }

        [Serializable]
        private class ChapterWrapper : Wrapper
        {
            [DataMember]
            public List<IChapter> SubChapters = new List<IChapter>();

            [DataMember]
            public List<IStep> Steps = new List<IStep>();

            [DataMember]
            public IChapter Chapter;

            public ChapterWrapper()
            {
            }

            public ChapterWrapper(IChapter chapter)
            {
                Chapter = chapter;

                try
                {
                    // Set LastSelectedStep to null, to prevent it needlessly serializing a full step tree.
                    ClearLastSelectedStep(chapter);

                    Steps.AddRange(GetSteps(chapter));
                    SubChapters.AddRange(GetSubChapters(chapter));

                    foreach (IStep step in Steps)
                    {
                        foreach (ITransition transition in step.Data.Transitions.Data.Transitions)
                        {
                            IStep targetStep = transition.Data.TargetStepReference.Entity;
                            if (targetStep != null)
                            {
                                ReplaceTargetStep(transition, new StepRef() { StepMetadata = new StepMetadata() { Guid = targetStep.StepMetadata.Guid } });
                            }
                        }
                    }

                    foreach (IChapter subChapter in SubChapters)
                    {
                        // Set LastSelectedStep to null, to prevent it needlessly serializing a full step tree.
                        ClearLastSelectedStep(subChapter);

                        List<IStep> stepRefs = new List<IStep>();
                        foreach (IStep step in subChapter.Data.Steps)
                        {
                            IStep stepRef = new StepRef() { StepMetadata = new StepMetadata() { Guid = step.StepMetadata.Guid } };
                            stepRefs.Add(stepRef);

                            if (subChapter.Data.FirstStep != null && subChapter.Data.FirstStep.StepMetadata.Guid == stepRef.StepMetadata.Guid)
                            {
                                ReplaceFirstStep(subChapter, stepRef);
                            }
                        }

                        ReplaceSteps(subChapter, stepRefs);
                    }
                }
                catch
                {
                    RestoreSourceGraph();
                    throw;
                }
            }

            public IChapter GetChapter()
            {
                try
                {
                    foreach (IStep step in Steps)
                    {
                        foreach (ITransition transition in step.Data.Transitions.Data.Transitions)
                        {
                            if (transition.Data.TargetStepReference.Entity != null && transition.Data.TargetStepReference.Entity is not StepRef)
                            {
                                continue;
                            }

                            Guid targetId = transition.Data.TargetStepReference.Id;
                            transition.Data.TargetStepReference.Set(Steps.FirstOrDefault(candidate => candidate.Id == targetId));
                        }
                    }

                    foreach (IChapter subChapter in SubChapters)
                    {
                        List<IStep> steps = new List<IStep>();

                        foreach (IStep stepRef in subChapter.Data.Steps)
                        {
                            IStep step = Steps.FirstOrDefault(step => step.StepMetadata.Guid == stepRef.StepMetadata.Guid);
                            steps.Add(step);

                            if (subChapter.Data.FirstStep != null && subChapter.Data.FirstStep.StepMetadata.Guid == stepRef.StepMetadata.Guid)
                            {
                                subChapter.Data.FirstStep = step;
                            }
                        }

                        subChapter.Data.Steps = steps;
                    }
                }
                finally
                {
                    RestoreSourceGraph();
                }

                return Chapter;
            }
        }

        [Serializable]
        private class ProcessWrapper : Wrapper
        {
            [DataMember]
            public List<IChapter> SubChapters = new List<IChapter>();

            [DataMember]
            public List<IStep> Steps = new List<IStep>();

            [DataMember]
            public IProcess Process;

            public ProcessWrapper()
            {
            }

            public ProcessWrapper(IProcess process)
            {
                Process = process;

                try
                {
                    foreach (IChapter chapter in process.Data.Chapters)
                    {
                        // Set LastSelectedStep to null, to prevent it needlessly serializing a full step tree.
                        ClearLastSelectedStep(chapter);

                        Steps.AddRange(GetSteps(chapter));
                        SubChapters.AddRange(GetSubChapters(chapter));
                    }

                    foreach (IStep step in Steps)
                    {
                        foreach (ITransition transition in step.Data.Transitions.Data.Transitions)
                        {
                            IStep targetStep = transition.Data.TargetStepReference.Entity;
                            if (targetStep != null)
                            {
                                ReplaceTargetStep(transition, new StepRef() { StepMetadata = new StepMetadata() { Guid = targetStep.StepMetadata.Guid } });
                            }
                        }
                    }

                    foreach (IChapter subChapter in SubChapters)
                    {
                        // Set LastSelectedStep to null, to prevent it needlessly serializing a full step tree.
                        ClearLastSelectedStep(subChapter);

                        List<IStep> stepRefs = new List<IStep>();
                        foreach (IStep step in subChapter.Data.Steps)
                        {
                            IStep stepRef = new StepRef() { StepMetadata = new StepMetadata() { Guid = step.StepMetadata.Guid } };
                            stepRefs.Add(stepRef);

                            if (subChapter.Data.FirstStep != null && subChapter.Data.FirstStep.StepMetadata.Guid == stepRef.StepMetadata.Guid)
                            {
                                ReplaceFirstStep(subChapter, stepRef);
                            }
                        }

                        ReplaceSteps(subChapter, stepRefs);
                    }
                }
                catch
                {
                    RestoreSourceGraph();
                    throw;
                }
            }

            public IProcess GetProcess()
            {
                try
                {
                    foreach (IStep step in Steps)
                    {
                        foreach (ITransition transition in step.Data.Transitions.Data.Transitions)
                        {
                            if (transition.Data.TargetStepReference.Entity != null && transition.Data.TargetStepReference.Entity is not StepRef)
                            {
                                continue;
                            }

                            Guid targetId = transition.Data.TargetStepReference.Id;
                            transition.Data.TargetStepReference.Set(Steps.FirstOrDefault(candidate => candidate.Id == targetId));
                        }
                    }

                    foreach (IChapter subChapter in SubChapters)
                    {
                        List<IStep> steps = new List<IStep>();

                        foreach (IStep stepRef in subChapter.Data.Steps)
                        {
                            IStep step = Steps.FirstOrDefault(step => step.StepMetadata.Guid == stepRef.StepMetadata.Guid);
                            steps.Add(step);

                            if (subChapter.Data.FirstStep != null && subChapter.Data.FirstStep.StepMetadata.Guid == stepRef.StepMetadata.Guid)
                            {
                                subChapter.Data.FirstStep = step;
                            }
                        }

                        subChapter.Data.Steps = steps;
                    }
                }
                finally
                {
                    RestoreSourceGraph();
                }

                return Process;
            }
        }

        private class Wrapper
        {
            [JsonIgnore]
            private readonly Stack<Action> restorations = new Stack<Action>();

            protected void ClearLastSelectedStep(IChapter chapter)
            {
                IStep originalStep = chapter.ChapterMetadata.LastSelectedStep;
                restorations.Push(() => chapter.ChapterMetadata.LastSelectedStep = originalStep);
                chapter.ChapterMetadata.LastSelectedStep = null;
            }

            protected void ReplaceTargetStep(ITransition transition, IStep replacement)
            {
                IStep originalStep = transition.Data.TargetStepReference.Entity;
                restorations.Push(() => transition.Data.TargetStepReference.Set(originalStep));
                transition.Data.TargetStepReference.Set(replacement);
            }

            protected void ReplaceFirstStep(IChapter chapter, IStep replacement)
            {
                IStep originalStep = chapter.Data.FirstStep;
                restorations.Push(() => chapter.Data.FirstStep = originalStep);
                chapter.Data.FirstStep = replacement;
            }

            protected void ReplaceSteps(IChapter chapter, IList<IStep> replacements)
            {
                IList<IStep> originalSteps = chapter.Data.Steps;
                restorations.Push(() => chapter.Data.Steps = originalSteps);
                chapter.Data.Steps = replacements;
            }

            protected void RestoreSourceGraph()
            {
                while (restorations.Count > 0)
                {
                    restorations.Pop().Invoke();
                }
            }

            protected IEnumerable<IStep> GetSteps(IChapter chapter)
            {
                List<IStep> steps = new List<IStep>();

                steps.AddRange(chapter.Data.Steps);

                IEnumerable<IChapter> subChapters = chapter.Data.Steps.SelectMany(step => step.Data.Behaviors.Data.Behaviors.Where(behavior => behavior.Data is IEntityCollectionData<IChapter>))
                    .Select(behavior => behavior.Data)
                    .Cast<IEntityCollectionData<IChapter>>()
                    .SelectMany(behavior => behavior.GetChildren());

                foreach (IChapter subChapter in subChapters)
                {
                    steps.AddRange(GetSteps(subChapter));
                }

                return steps;
            }

            protected IEnumerable<IChapter> GetSubChapters(IChapter chapter)
            {
                List<IChapter> subChapters = new List<IChapter>();

                foreach (IStep step in chapter.Data.Steps)
                {
                    foreach (IBehavior behavior in step.Data.Behaviors.Data.Behaviors)
                    {
                        if (behavior.Data is IEntityCollectionData<IChapter> data)
                        {
                            subChapters.InsertRange(0, data.GetChildren());

                            foreach (IChapter subChapter in data.GetChildren())
                            {
                                subChapters.InsertRange(0, GetSubChapters(subChapter));
                            }
                        }
                    }
                }

                return subChapters;
            }

            [Serializable]
            public class StepRef : IStep
            {
                IData IDataOwner.Data { get; } = null;

                IStepData IDataOwner<IStepData>.Data { get; } = null;

                public ILifeCycle LifeCycle { get; } = null;

                [JsonIgnore]
                public Guid Id => StepMetadata?.Guid ?? Guid.Empty;

                public void RegenerateId()
                {
                    throw new NotImplementedException();
                }

                public IStageProcess GetActivatingProcess()
                {
                    throw new NotImplementedException();
                }

                public IStageProcess GetActiveProcess()
                {
                    throw new NotImplementedException();
                }

                public IStageProcess GetDeactivatingProcess()
                {
                    throw new NotImplementedException();
                }

                public void Configure(IMode mode)
                {
                    throw new NotImplementedException();
                }

                public void Update()
                {
                    throw new NotImplementedException();
                }

                public IStageProcess GetAbortingProcess()
                {
                    throw new NotImplementedException();
                }

                public StepMetadata StepMetadata { get; set; }
                [JsonIgnore]
                public IEntity Parent { get; set; }
            }
        }
    }
}
