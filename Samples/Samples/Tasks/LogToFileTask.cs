﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Text;
using Acr.Ble;
using Acr.Ble.Plugins;
using Autofac;
using ReactiveUI;
using Samples.Services;


namespace Samples.Tasks
{
    public class LogToFileTask : IStartable
    {
        readonly object syncLock = new object();
        readonly IAdapter adapter;
        readonly IAppSettings settings;
        IDisposable sub;


        public LogToFileTask(IAdapter adapter, IAppSettings settings)
        {
            this.adapter = adapter;
            this.settings = settings;
        }


        public void Start()
        {
            this.settings
                .WhenAnyValue(x => x.IsBackgroundLoggingEnabled)
                .Subscribe(doLog =>
                {
                    if (doLog)
                    {
                        this.sub = this.adapter
                            .WhenActionOccurs(BleLogFlags.All)
                            .Buffer(TimeSpan.FromSeconds(3))
                            .Subscribe(this.WriteLog);
                    }
                    else
                    {
                        this.sub?.Dispose();
                    }
                });

        }


        void WriteLog(IList<string> msgs)
        {
            var sb = new StringBuilder();
            foreach (var msg in msgs)
            {
                sb.AppendLine($"[{DateTime.Now:T}] {msg}");
            }
            lock(this.syncLock)
            {
                // TODO
            }
        }
    }
}
