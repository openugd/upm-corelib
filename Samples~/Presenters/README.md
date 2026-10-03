# Presenters

A presenter tree on a scope of the host's: `Presenter.Attach` with `ContextPresenterFactory`, injection into
presenters created with `new`, a view swap that shows `ViewLifetime` unwiring the old view, a presenter that
closes with its view, and the order in which a tree tears down.

## How to run it

1. Create an empty scene.
2. Add an empty GameObject and put `PresentersSample` on it.
3. Press Play and watch the Console. One step runs per second; the views appear as children of the
   GameObject, and each `CounterView`'s inspector shows the text its presenter rendered. In play mode, the
   *Click* item of a `CounterView`'s context menu clicks it.

## What happens

| Step | What the host does | What to read in the Console |
| --- | --- | --- |
| 1. open | Defines a scope of its own, `Presenter.Attach(screen, scope, factory)`, then `SetModel` and `SetView` on both counters. | `First` and `Second` are attached and injected before the screen finishes its `OnInitialize`; each wires its view. |
| 2. click | Clicks View A and View B. | `First` counts to 1, `Second` to 11. |
| 3. swap | Gives `First` View C instead of View A, then clicks both. | `unwired View A`, `view detached`, `wired View C`. Clicking View A reaches nothing; clicking View C counts to 2. View A still shows `First: 1`: a detached view is no longer rendered into. |
| 4. close with a view | Adds a `Popup` counter with `CloseWith(popupView.Lifetime)` and destroys its view. | The popup unwires and closes when its view is destroyed, and leaves the screen's children. |
| 5. close | Terminates the screen's scope. | See below. |

The teardown in step 5, verbatim apart from the tag prefix `Presenters.`:

```text
Screen->clean-up registered after the children: runs before they close
Second->unwired View B
Second->closed
First->unwired View C
First->closed
Screen->closed, after its children
Host->screen scope ended: the views can be released now
```

A `Lifetime` unwinds newest first. The screen's own clean-up was registered after its children, so it runs
first; then the children close, last attached first, each ending its view scope before its `OnClose`; then the
screen's `OnClose`; and last, the action the host registered on the scope *before* `Attach`, which is where a
host returns pooled views.

## What to look at

| File | What it shows |
| --- | --- |
| `PresentersSample.cs` | **Start here.** The host: a context with `ContextPresenterFactory` registered as `IPresenterFactory`, and the five steps. |
| `CounterPresenter.cs` | `OnViewAdded` wires a view and registers the unwiring on `ViewLifetime`; `OnRefresh` renders and is idempotent; an `[Inject]` member filled by the factory. |
| `ScreenPresenter.cs` | A presenter with no view that adds two children in `OnInitialize`. |
| `ICounterView.cs`, `CounterView.cs` | The presenter is typed on an interface, so it compiles without UnityEngine and a test can hand it a plain object; `CounterView` is the `ViewBehaviour` that implements it. |

This is sample code: once imported it is yours to change, and nothing in the package depends on it.
