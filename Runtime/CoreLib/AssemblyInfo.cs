using System.Runtime.CompilerServices;

// The play-mode suite starts and ends play sessions headlessly (PlaySession.Begin/End), standing in for the Unity
// callbacks that do it in a real session.
[assembly: InternalsVisibleTo("com.openugd.corelib.playmode.tests")]
