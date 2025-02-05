﻿using NUnit.Framework;
using System;
using System.Linq;

namespace PubSub.Tests
{
    [TestFixture]
    public class CoreHubTests
    {
        private Hub _hub;
        private object _subscriber;
        private object _condemnedSubscriber;
        private object _preservedSubscriber;

        [SetUp]
        public void Setup()
        {
            _hub = new Hub();
            _subscriber = new object();
            _condemnedSubscriber = new object();
            _preservedSubscriber = new object();
        }

        [Test]
        public void Publish_CallsAllRegisteredActions()
        {
            // arrange
            var callCount = 0;
            _hub.Subscribe(new object(), new Action<string>(a => callCount++));
            _hub.Subscribe(new object(), new Action<string>(a => callCount++));

            // act
            _hub.Publish(default(string));

            // assert
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void Publish_SpecialEvent_CaughtByBase()
        {
            // arrange
            var callCount = 0;
            _hub.Subscribe<Event>(_subscriber, a => callCount++);
            _hub.Subscribe(_subscriber, new Action<Event>(a => callCount++));

            // act
            _hub.Publish(new SpecialEvent());

            // assert
            Assert.AreEqual(2, callCount);
        }

        [Test]
        public void Publish_BaseEvent_NotCaughtBySpecial()
        {
            // arrange
            var callCount = 0;
            _hub.Subscribe(_subscriber, new Action<SpecialEvent>(a => callCount++));
            _hub.Subscribe(_subscriber, new Action<Event>(a => callCount++));

            // act
            _hub.Publish(new Event());

            // assert
            Assert.AreEqual(1, callCount);
        }


        [Test]
        public void Publish_CleansUpBeforeSending()
        {
            // arrange
            var liveSubscriber = new object();

            // act
            _hub.Subscribe(_condemnedSubscriber, new Action<string>(a => { }));
            _hub.Subscribe(liveSubscriber, new Action<string>(a => { }));

            _condemnedSubscriber = null;
            GC.Collect();

            _hub.Publish(default(string));

            // assert
            Assert.AreEqual(1, _hub._handlers.Count);
            GC.KeepAlive(liveSubscriber);
        }

        [Test]
        public void Subscribe_AddsHandlerToList()
        {
            // arrange
            var action = new Action<string>(a => { });

            // act
            _hub.Subscribe(_subscriber, action);

            // assert
            var h = _hub._handlers.First();
            Assert.AreEqual(_subscriber, h.Sender.Target);
            Assert.AreEqual(action, h.Action);
            Assert.AreEqual(action.Method.GetParameters().First().ParameterType, h.Type);
        }

        [Test]
        public void Unsubscribe_RemovesAllHandlers_OfAnyType_ForSender()
        {
            // act
            _hub.Subscribe(_preservedSubscriber, new Action<string>(a => { }));
            _hub.Subscribe(_subscriber, new Action<string>(a => { }));
            _hub.Unsubscribe(_subscriber);

            // assert
            Assert.IsTrue(_hub._handlers.Any(a => a.Sender.Target == _preservedSubscriber));
            Assert.IsFalse(_hub._handlers.Any(a => a.Sender.Target == _subscriber));
        }

        [Test]
        public void Unsubscribe_RemovesAllHandlers_OfSpecificType_ForSender()
        {
            // arrange
            _hub.Subscribe(_subscriber, new Action<string>(a => { }));
            _hub.Subscribe(_subscriber, new Action<string>(a => { }));
            _hub.Subscribe(_preservedSubscriber, new Action<string>(a => { }));

            // act
            _hub.Unsubscribe<string>(_subscriber);

            // assert
            Assert.IsFalse(_hub._handlers.Any(a => a.Sender.Target == _subscriber));
        }

        [Test]
        public void Unsubscribe_RemovesSpecificHandler_ForSender()
        {
            var actionToDie = new Action<string>(a => { });
            _hub.Subscribe(_subscriber, actionToDie);
            _hub.Subscribe(_subscriber, new Action<string>(a => { }));
            _hub.Subscribe(_preservedSubscriber, new Action<string>(a => { }));

            // act
            _hub.Unsubscribe(_subscriber, actionToDie);

            // assert
            Assert.IsFalse(_hub._handlers.Any(a => a.Action.Equals(actionToDie)));
        }

        [Test]
        public void Exists_EventDoesExist()
        {
            var action = new Action<string>(a => { });

            _hub.Subscribe(_subscriber, action);

            Assert.IsTrue(_hub.Exists(_subscriber, action));
        }


        [Test]
        public void Unsubscribe_CleanUps()
        {
            // arrange
            var actionToDie = new Action<string>(a => { });
            _hub.Subscribe(_subscriber, actionToDie);
            _hub.Subscribe(_subscriber, new Action<string>(a => { }));
            _hub.Subscribe(_condemnedSubscriber, new Action<string>(a => { }));

            _condemnedSubscriber = null;

            GC.Collect();

            // act
            _hub.Unsubscribe<string>(_subscriber);

            // assert
            Assert.AreEqual(0, _hub._handlers.Count);
        }

        [Test]
        public void PubSubUnsubDirectlyToHub()
        {
            // arrange
            var callCount = 0;
            var action = new Action<Event>(a => callCount++);
            var myhub = new Hub();

            // this lies and subscribes to the static hub instead.
            myhub.Subscribe(new Action<Event>(a => callCount++));
            myhub.Subscribe(new Action<SpecialEvent>(a => callCount++));
            myhub.Subscribe(action);

            // act
            myhub.Publish(new Event());
            myhub.Publish(new SpecialEvent());
            myhub.Publish<Event>();

            // assert
            Assert.AreEqual(7, callCount);

            // unsubscribe
            // this lies and unsubscribes from the static hub instead.
            myhub.Unsubscribe<SpecialEvent>();

            // act
            myhub.Publish(new SpecialEvent());

            // assert
            Assert.AreEqual(9, callCount);

            // unsubscribe specific action
            myhub.Unsubscribe(action);

            // act
            myhub.Publish(new SpecialEvent());

            // assert
            Assert.AreEqual(10, callCount);

            // unsubscribe to all
            myhub.Unsubscribe();

            // act
            myhub.Publish(new SpecialEvent());

            // assert
            Assert.AreEqual(10, callCount);
        }

        [Test]
        public void Publish_NoExceptionRaisedWhenHandlerCreatesNewSubscriber()
        {
            // arrange
            _hub.Subscribe(new Action<Event>(a => new Stuff(_hub)));

            // act
            try
            {
                _hub.Publish(new Event());
            }

            // assert
            catch (InvalidOperationException e)
            {
                Assert.Fail(
                    String.Format("Expected no exception, but got: {0}", e)
                );
            }
        }

        internal class Stuff
        {
            public Stuff(Hub hub)
            {
                hub.Subscribe(new Action<Event>(a => { }));
            }
        }
    }



    public class Event
    {
    }

    public class SpecialEvent : Event
    {
    }
}