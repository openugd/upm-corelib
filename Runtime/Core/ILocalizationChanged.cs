using System;

namespace OpenUGD.Core
{
    public interface ILocalizationChanged
    {
        void Subscribe(Lifetime lifetime, Action listener);
    }
}
