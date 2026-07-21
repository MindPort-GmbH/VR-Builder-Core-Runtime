# AGENTS.md
This file provides guidance to AI agents when working with code in this repository.

## Project Overview
This repository contains MindPort Process Engine (`co.mindport.processengine`), the shared runtime process architecture used by VR Builder (Unity) and, in a future version, by Tinkerflow (Godot).

If existing, use the parent or workspace-root `AGENTS.md` for global rules such as MCP usage, coding style, Git workflow, and reporting format.

## Directory Structure
```text
[Package Root]
├── Source/                       # Runtime source in the VRBuilder.Core assembly
│   ├── Attributes/               # Metadata used by editor drawers and process authoring tools
│   ├── Behaviors/                # Built-in process behaviors and execution-stage contracts
│   ├── Conditions/               # Built-in completion conditions
│   ├── Configuration/            # Runtime services, scene configuration, and modes
│   ├── Entities/Factories/       # Process, chapter, step, and transition construction
│   ├── EntityOwners/             # Sequential, parallel, and folded lifecycle orchestration
│   ├── IO/                       # Platform file systems and process asset strategies
│   ├── ProcessController/        # Process loading and standard/spectator controllers
│   ├── ProcessValidation/        # Runtime validation contracts and report entries
│   ├── Properties/               # Serializable data, scene object, and operation properties
│   ├── SceneObjects/             # GUID-based scene object registration and references
│   ├── Serialization/            # Newtonsoft JSON serializers and Unity value converters
│   ├── TextToSpeech/             # Provider contracts, settings, and file/SAPI providers
│   ├── UI/                       # Runtime console, keyboard, selectable values, and spectator UI
│   ├── Unity/                    # Shared Unity helpers and singleton infrastructure
│   └── Utils/                    # Runtime utilities, audio data, Bezier, and particle helpers
├── package.json                  # Unity package identity and compatibility
└── README.md                     # Package summary
```

## Architecture
- `VRBuilder.Core.asmdef` defines the single runtime assembly. It references Input System, Localization, and Newtonsoft JSON behind compile/version defines.
- `IProcess`, `IChapter`, `IStep`, and `ITransition` define the process hierarchy. Concrete entities keep serializable data in nested `EntityData` types.
- `LifeCycle`, `StageProcess`, and `EntityOwners/*` coordinate activation, active execution, deactivation, aborting, and fast-forward behavior.
- `ProcessRunner` owns active process execution and exposes process, chapter, step, transition, and fast-forward events.
- `RuntimeConfigurator` composes runtime services. Controllers in `ProcessController/` load and operate processes in scenes.
- `Serialization/` and `IO/` form the persisted process boundary. Scene references use stable GUID-based identities from `SceneObjects/`.

## Package Boundaries
- Treat public interfaces, nested data types, serialization attributes, type names, and namespaces as compatibility contracts. Changes can break extensions or persisted process JSON.
- Keep runtime logic in this package. Editor-only behavior belongs in `co.mindport.vrbuilder.core`; existing `UNITY_EDITOR` sections support runtime types inside editor sessions and must remain guarded.
- Preserve platform-specific paths and compile guards for Android, WebGL, Input System, Unity versions, and editor-only APIs.
- Keep entity lifecycle transitions deterministic. New behaviors and conditions must implement existing data and lifecycle contracts instead of bypassing `LifeCycle` or `ProcessRunner`.
- Keep scene object references GUID-based. Do not replace them with transient instance IDs or direct scene lookups in serialized data.
- Coordinate dependency changes with consuming VR Builder Core package setup. Update `VRBuilder.Core.asmdef` references, defines, and version defines together.
- Preserve Unity `.meta` files and GUIDs. Do not move or rename serialized types without an explicit migration plan.

## Authoritative Files
- `package.json`: package identity, version, repository, and minimum Unity version.
- `Source/VRBuilder.Core.asmdef`: assembly references, compile constraints, and version defines.
- `Source/IProcess.cs`, `Source/IChapter.cs`, `Source/IStep.cs`, `Source/ITransition.cs`: process model contracts.
- `Source/LifeCycle.cs`, `Source/ProcessRunner.cs`: execution state and runtime event flow.
- `Source/Configuration/RuntimeConfigurator.cs`: runtime service composition.
- `Source/Serialization/*` and `Source/IO/*`: persisted process and platform storage contracts.

## Sibling Repositories
### When environment is Unity
- VR Builder Core consumes and extends this package: `co.mindport.vrbuilder.core`.
- VR Builder Pro extends VR Builder Core and this runtime: `co.mindport.vrbuilder.pro`.
- Tests live in `co.mindport.vrbuilder.tests`. Implement tests there unless instructed otherwise.
- If required sibling repository is unavailable, report once and continue with available scope.

## Validation Checklist
### When environment is Unity
- Confirm change belongs in shared process runtime rather than Core editor or Pro layer.
- Run Unity compilation with `NEWTONSOFT_JSON` and `UNITY_LOCALIZATION` available.
- Run relevant EditMode or PlayMode tests from `co.mindport.vrbuilder.tests`; prioritize lifecycle, process hierarchy, serialization, IO, properties, and scene object tests matching changed area.
- Enter Play mode when execution, controller, configuration, scene reference, or platform behavior changes.
- Verify serialized process compatibility when changing data contracts, converters, factories, attributes, or type names.
- Report checks actually run and remaining platform-specific uncertainty.
