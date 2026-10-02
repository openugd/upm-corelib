# Multi Instance

Several game instances in one process, one per display, with input and audio focus switched between them.
These are the corelib 0.6.x components, moved out of the package in 2.0.0 and fixed on the way:

| Script | What it does |
| --- | --- |
| `ContextInstanceComponent` | One instance: its `EventSystem`, its `AudioListener`, its `TargetDisplay`, and a selected flag that enables or disables both components. |
| `IContextInstanceProvider` | The read-only face of an instance, for the game's context to register and read. |
| `ContextFactoryInstancesComponent` | Clones the instance prefab once per display and moves focus between the clones. |
| `Editor/ContextFactoryInstancesComponentEditor` | Adds a Rebuild button to the factory's inspector, enabled in play mode. |
| `InstanceContextExample` | New in 2.0: a `ContextBehaviour` that registers the instance and points its cameras at its display. |

This is sample code: once imported it is yours to change, and nothing in the package depends on it.

## Prefabs and scenes from 0.6.x

The four 0.6.x scripts keep their original GUIDs, so a prefab or scene that carried them binds to these copies
after the import, with no "Missing script":

| Script | GUID |
| --- | --- |
| `ContextInstanceComponent` | `bde234200e4d407696b34ff23b9e6ea2` |
| `ContextFactoryInstancesComponent` | `cdba43c5badf44d28b2cfdf25807862b` |
| `IContextInstanceProvider` | `1fc09025e03547a7b692210cd488a0e6` |
| `ContextFactoryInstancesComponentEditor` | `bbe59184e52143a9953989764296a74b` |

The serialized fields keep their names too, so the references a prefab stored (`EventSystem`, `AudioListener`,
`TargetDisplay`, `Prefab`, `Count`) keep their values.

Keep exactly one copy of the sample in a project. A second copy carries the same GUIDs, and Unity resolves the
conflict by giving one of them new ones.

## Code from 0.6.x

The types keep the namespace `OpenUGD.Core` and the same members, and `ContextFactoryInstancesComponent` is
not sealed. Code that read `IContextInstanceProvider.TargetDisplay`, or an empty
`GameFactoryComponent : ContextFactoryInstancesComponent { }`, compiles against the copy unchanged. The
sample's assembly, `com.openugd.corelib.samples.multiinstance`, is auto-referenced, so `Assembly-CSharp` sees
it; an assembly definition of your own has to reference it.

The context that registered the instance does need porting, like every 0.6.x context. Where it called
`Injector.ToValue<IContextInstanceProvider>(...)`, a 2.0 `ContextBehaviour` registers the instance in
`CreateContextAsync`:

```csharp
builder.Services.AddInstance<IContextInstanceProvider>(GetComponent<ContextInstanceComponent>());
```

`InstanceContextExample.cs` is the whole of it, including the part every game did by hand: copying
`TargetDisplay` to the instance's cameras.

## Changes from 0.6.x

- **The instance's scope is created in `Awake`.** The 0.6.x component created it in a field initializer, so
  an instance that was never activated, and so never received `OnDestroy`, left a `Lifetime` on
  `Lifetime.Eternal` for the rest of the process. Calling `Subscribe` before `Awake` now throws
  `InvalidOperationException`.
- **The instances are `DontDestroyOnLoad`, like the factory.** In 0.6.x only the factory was, so a scene load
  destroyed the instances and left the factory tracking dead components. The factory now destroys its
  instances when it is destroyed, since no scene unload will.
- **`Rebuild` is play mode only.** The 0.6.x inspector button called it in edit mode, where Unity's
  `Destroy` is not allowed and the clones were created into the open scene. The button is now disabled
  outside play mode, and `Rebuild` itself only warns there.
- **The event system and the audio listener are optional.** `EventSystem` comes from `com.unity.ugui` and
  `AudioListener` from the built-in Audio module. The sample's assembly definition defines
  `OPENUGD_UGUI_PACKAGE` and `OPENUGD_AUDIO_MODULE` through `versionDefines` when those packages are in the
  project. Without one, its property and its half of the focus switch are compiled out.

## Limits

- Nothing calls `Display.Activate`. In a player, every display but the primary one stays off until the game
  activates it ([Unity Manual: Multi-display](https://docs.unity3d.com/6000.0/Documentation/Manual/MultiDisplay.html)).
  In the editor, the Game view's Display menu shows each one.
- Only `InstanceContextExample` acts on `TargetDisplay`, and only for cameras. A canvas in Screen Space -
  Overlay mode has its own `targetDisplay`.
- Nothing stops two instances from being selected at once. Move focus through
  `ContextFactoryInstancesComponent.Select`, which unselects the outgoing instance first.
- An instance destroyed by anything other than the factory leaves a dead entry in the factory's list. Call
  `Rebuild` afterwards.
- The sample has no tests. The package's checks compile it; nothing runs it.
- If what you need is several clients in one editor for testing, compare Unity's Multiplayer Play Mode
  package, which runs each player in its own process.
