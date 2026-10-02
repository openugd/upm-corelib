using System.Runtime.CompilerServices;

// Presenter.Internal is internal on purpose: initialising a presenter is the framework's job, not a consumer's.
// The test assembly needs it to build a presenter tree without going through a UI service.
[assembly: InternalsVisibleTo("com.openugd.corelib.tests")]
