using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PubSub
{
    public class Hub
    {
        public List<Handler> _handlers = new List<Handler>();
        internal object _locker = new object();
        private static Hub _default;

        public static Hub Default
        {
            get
            {
                return _default ?? (_default = new Hub());
            }
        }

        public void Publish<T>(T data = default(T))
        {
            foreach (var handler in GetAliveHandlers<T>())
            {
                var action = handler.Action as Action<T>;
                if (action != null)
                {
                    action(data);
                    continue;
                }

                var func = handler.Action as Func<T, Task>;
                if (func != null)
                {
                    func(data);
                    continue;
                }
            }
        }

        public Task PublishAsync<T>(T data = default(T))
        {
            var taskList = new List<Task>();

            foreach (var handler in GetAliveHandlers<T>())
            {
                var action = handler.Action as Action<T>;
                if (action != null)
                {
                    taskList.Add(Task.Run(() => action(data)));
                    continue;
                }

                var func = handler.Action as Func<T, Task>;
                if (func != null)
                {
                    taskList.Add(func(data));
                    continue;
                }
            }

            return Task.Run(() => Task.WaitAll(taskList.ToArray()));
        }

        /// <summary>
        ///     Allow subscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void Subscribe<T>(Action<T> handler)
        {
            Subscribe(this, handler);
        }

        public void Subscribe<T>(object subscriber, Action<T> handler)
        {
            SubscribeDelegate<T>(subscriber, handler);
        }

        public void Subscribe<T>(Func<T, Task> handler)
        {
            Subscribe(this, handler);
        }

        public void Subscribe<T>(object subscriber, Func<T, Task> handler)
        {
            SubscribeDelegate<T>(subscriber, handler);
        }

        /// <summary>
        ///     Allow unsubscribing directly to this Hub.
        /// </summary>
        public void Unsubscribe()
        {
            Unsubscribe(this);
        }

        public void Unsubscribe(Delegate handler)
        {
            Unsubscribe(this, handler);
        }

        public void Unsubscribe(object subscriber, Delegate handler = null)
        {
            lock (_locker)
            {
                var query = _handlers.Where(a => !a.Sender.IsAlive ||
                                                a.Sender.Target.Equals(subscriber));

                if (handler != null)
                    query = query.Where(a => a.Action.Equals(handler));

                foreach (var h in query.ToList())
                    _handlers.Remove(h);
            }
        }

        /// <summary>
        ///     Allow unsubscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void Unsubscribe<T>()
        {
            Unsubscribe<T>(this);
        }

        /// <summary>
        ///     Allow unsubscribing directly to this Hub.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="handler"></param>
        public void Unsubscribe<T>(Delegate handler)
        {
            Unsubscribe(this, handler);
        }

        public void Unsubscribe<T>(object subscriber, Delegate handler = null)
        {
            lock (_locker)
            {
                var query = _handlers.Where(a => !a.Sender.IsAlive ||
                                                a.Sender.Target.Equals(subscriber) && a.Type == typeof(T));

                if (handler != null)
                    query = query.Where(a => a.Action.Equals(handler));

                foreach (var h in query.ToList())
                    _handlers.Remove(h);
            }
        }

        public bool Exists<T>()
        {
            return Exists<T>(this);
        }

        public bool Exists<T>(object subscriber)
        {
            lock (_locker)
            {
                foreach (var h in _handlers)
                {
                    if (Equals(h.Sender.Target, subscriber) &&
                         typeof(T) == h.Type)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool Exists<T>(object subscriber, Action<T> handler)
        {
            lock (_locker)
            {
                foreach (var h in _handlers)
                {
                    if (Equals(h.Sender.Target, subscriber) &&
                         typeof(T) == h.Type &&
                         h.Action.Equals(handler))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void SubscribeDelegate<T>(object subscriber, Delegate handler)
        {
            var item = new Handler
            {
                Action = handler,
                Sender = new WeakReference(subscriber),
                Type = typeof(T)
            };

            lock (_locker)
            {
                _handlers.Add(item);
            }
        }

        private List<Handler> GetAliveHandlers<T>()
        {
            PruneHandlers();
            return _handlers.Where(h => h.Type.GetType().IsAssignableFrom(typeof(T).GetType())).ToList();
        }

        private void PruneHandlers()
        {
            lock (_locker)
            {
                for (int i = _handlers.Count - 1; i >= 0; --i)
                {
                    if (!_handlers[i].Sender.IsAlive)
                        _handlers.RemoveAt(i);
                }
            }
        }

        public class Handler
        {
            public Delegate Action { get; set; }
            public WeakReference Sender { get; set; }
            public Type Type { get; set; }
        }
    }
}