using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Pool;

namespace OpenUGD.Commands
{
    public class CommandMap : ITellMessage, IMapCommand
    {
        private readonly IInjector _injector;
        private readonly Lifetime _lifetime;
        private readonly Dictionary<Type, Container> _map;
        private readonly List<ITellMessage> _tellMessages;

        public CommandMap(Lifetime lifetime, IInjector injector)
        {
            _tellMessages = new List<ITellMessage>();
            _map = new Dictionary<Type, Container>();
            _lifetime = lifetime;
            _injector = new Injector(injector);
            _lifetime.AddAction(() => {
                _tellMessages.Clear();
                foreach (var pair in _injector.ToArray())
                {
                    _injector.UnRegister(pair.Key);
                }
            });
        }

        public ICommandMapper Map<TMessage>() where TMessage : IMessage
        {
            Container container;
            if (!_map.TryGetValue(typeof(TMessage), out container))
            {
                var lifetime = Lifetime.Define(_lifetime);
                var mapper = new CommandMapper(lifetime.Lifetime, typeof(TMessage), _injector);

                _map[typeof(TMessage)] = container = new Container {
                    Lifetime = lifetime,
                    Mapper = mapper
                };

                lifetime.Lifetime.AddAction(() => { _map.Remove(typeof(TMessage)); });
            }

            return container.Mapper;
        }

        public void Tell(object message)
        {
            Container container;
            if (_map.TryGetValue(message.GetType(), out container))
            {
                container.Mapper.Tell(message);
            }

            var pool = ListPool<ITellMessage>.Get();
            pool.AddRange(_tellMessages);
            foreach (var tellMessage in pool)
            {
                tellMessage.Tell(message);
            }

            pool.Clear();
            ListPool<ITellMessage>.Release(pool);
        }

        public void Subscribe(Lifetime lifetime, ITellMessage tellMessage)
        {
            _tellMessages.Add(tellMessage);
            lifetime.AddAction(() => { _tellMessages.Remove(tellMessage); });
        }

        private class Container
        {
            public Lifetime.Definition Lifetime;
            public CommandMapper Mapper;
        }
    }
}
