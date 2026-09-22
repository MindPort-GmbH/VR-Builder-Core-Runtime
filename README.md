# VR-Builder-Core-Runtime
Engine-agnostic core architecture for VR Builder.

## Entity cloning

Entities no longer clone themselves. Inject an `IEntityCloner` and replace calls such as `process.Clone()` with `entityCloner.Clone(process)`. Runtime code can use the cloner exposed by `BaseRuntimeConfiguration`; editor integrations should expose a cloner built from their configured `IProcessSerializer`.

Cross-entity references should use `EntityReference<TEntity>`. The cloner discovers and remaps these values automatically, mapping references inside the owned graph to their copies and leaving external references unchanged. `ITransitionData.TargetStep` and `GoToChapterBehavior.EntityData.ChapterGuid` are obsolete compatibility facades; they remain serialized so existing process data still populates the typed references.
