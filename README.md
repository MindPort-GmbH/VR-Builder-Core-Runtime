# VR-Builder-Core-Runtime
Engine-agnostic core architecture for VR Builder.

## Entity cloning

Entities no longer clone themselves. Inject an `IEntityCloner` and replace calls such as `process.Clone()` with `entityCloner.Clone(process)`. Runtime code can use the cloner exposed by `BaseRuntimeConfiguration`; editor integrations should expose a cloner built from their configured `IProcessSerializer`.

Cross-entity references in new data types should use `EntityReference<TEntity>`. The cloner discovers and remaps these values automatically, mapping references inside the owned graph to their copies and leaving external references unchanged. Existing `ITransitionData.TargetStep` and `GoToChapterBehavior.EntityData.ChapterGuid` members remain available and retain their serialized names as compatibility facades over typed references.
