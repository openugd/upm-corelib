using UnityEngine;

namespace OpenUGD.Core.Loggers
{
    public class UnityLoggerProvider : ILoggerProvider
    {
        public void Log(LoggerFlag flag, string tag, object message)
        {
            switch (flag)
            {
                case LoggerFlag.Verbose:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LoggerFlag.Info:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LoggerFlag.Warning:
                    Debug.LogWarning($"{tag}->{message}");
                    break;
                case LoggerFlag.Error:
                    Debug.LogError($"{tag}->{message}");
                    break;
                case LoggerFlag.Debug:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LoggerFlag.Fatal:
                    Debug.LogError($"{tag}->{message}");
                    break;
            }
        }
    }

    public static class UnityLoggerProviderExtension
    {
        public static void UseUnityLogger(this LoggerGlobal logger, Lifetime lifetime)
        {
            var unityLogger = new UnityLoggerProvider();
            logger.Subscribe(unityLogger);
            lifetime.AddAction(() => logger.Unsubscribe(unityLogger));
        }
    }
}
