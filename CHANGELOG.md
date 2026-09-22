# Changelog - VR Builder Core Runtime

**v1.1.0 (2026/09/18 - Current)**

*[Added]*
- Process entities now have unique identifiers and entity references that can be remapped when copying a process graph.
- Added a shared serializer-backed cloning service for independent copies of entities and their owned graphs.

*[Changed]*
- Reduced repeated graph traversal and allocations during process execution through cached runtime graph data and streamlined lifecycle processing.
- Custom integrations must migrate from entity Clone methods to IEntityCloner and adopt the updated entity reference contracts. IEntity now exposes Id and RegenerateId.

*[Fixed]*
- Fixed handling of processes with no chapters.
- Fixed handling of null data property values and non-generic list entry types.
- Scene reference hash codes are now independent of reference order.
- Serializing a process or chapter now restores the selected step in the source graph, including nested chapters.
