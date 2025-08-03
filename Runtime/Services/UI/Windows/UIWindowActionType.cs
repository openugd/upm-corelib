using System;

namespace OpenUGD.Services.UI.Windows
{
    public enum UIWindowActionType
    {
        Opened = 0,
        [Obsolete] WindowOpened = Opened,

        Closed = 1,

        [Obsolete] WindowClosed = Closed
    }
}