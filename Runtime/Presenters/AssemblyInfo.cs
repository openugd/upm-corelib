using System.Runtime.CompilerServices;

// Presenter.Internal is internal on purpose: initialising a presenter is the framework's job, not a consumer's.
// The test assembly needs it to drive the attach sequence a presenter-opening service would use.
[assembly: InternalsVisibleTo("com.openugd.presenters.tests")]
