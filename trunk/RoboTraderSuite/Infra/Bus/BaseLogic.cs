﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Infra.Enum;
using Infra.Extensions;

namespace Infra.Bus
{
    public class MessageHandler : System.Attribute
    {
        
    }

    class TimedTask : IMessage
    {
        public TimedTask(Action t)
        {
            CurTask = t;
        }

        public Action CurTask { get; set; }
        public EapiDataTypes APIDataType { get; set; }
    }

    public abstract class SimpleBaseLogic : IBaseLogic
    {
        private readonly ConcurrentQueue<IMessage> _queue;
        private readonly int WORK_INTERVAL_MS = 100;

        protected SimpleBaseLogic()
        {
            _queue = new ConcurrentQueue<IMessage>();
            var t = new Thread(Work) {IsBackground = true };
            t.Start();
        }

        protected virtual string ThreadName => "SimpleBaseLogic_Work";

        private void Work()
        {
            Thread.CurrentThread.Name = ThreadName;
            while (true)
            {
                IMessage message;
                if (_queue.TryDequeue(out message))
                {
                    TimedTask timedTask = message as TimedTask;
                    if (timedTask != null)
                    {
                        //calls the task delegate action
                        timedTask.CurTask();
                    }
                    else
                    {
                        HandleMessage(message);
                    }

                }
                else
                {
                    Thread.Sleep(WORK_INTERVAL_MS);
                }
            }
        }

        public void Enqueue(IMessage message, bool duplicate=true)
        {
            if (duplicate)
            {
                _queue.Enqueue(message.Copy());
            }
            else
            {
                _queue.Enqueue(message);
            }
            
        }

        protected abstract void HandleMessage(IMessage message);


        protected void AddScheduledTask(TimeSpan span, Action task, bool reOccuring = false)
        {
            GeneralTimer.GeneralTimerInstance.AddTask(span, () =>
            {
                _queue.Enqueue(new TimedTask(task));
            }, reOccuring);
        }

    }


    public abstract class SmartBaseLogic : SimpleBaseLogic
    {
        private readonly global::Infra.Bus.Bus _bus;
        private readonly Dictionary<Type, MethodInfo> _handledTypes;

        protected SmartBaseLogic(global::Infra.Bus.Bus bus)
        {
            _bus = bus;
            _handledTypes = GetLogicHandledTypes();
            _bus.RegisterLogic(this, _handledTypes.Keys);

        }
        private Dictionary<Type, MethodInfo> GetLogicHandledTypes()
        {
            return this.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Where(
                method => Attribute.IsDefined(method, typeof(MessageHandler)))
                .Select(method => new { method.GetParameters()[0].ParameterType, method })
                .ToDictionary(t => t.ParameterType, t => t.method);
        }

        protected void PublishMessage(IMessage message)
        {
            _bus.SendMessage(message);
        }

        protected override void HandleMessage(IMessage message)
        {
            _handledTypes[message.GetType()].Invoke(this, new Object[] { message });
        }
    }


}
