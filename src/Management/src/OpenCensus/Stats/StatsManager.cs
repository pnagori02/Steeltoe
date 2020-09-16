﻿// <copyright file="StatsManager.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

namespace OpenCensus.Stats
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Common;
    using OpenCensus.Internal;
    using OpenCensus.Tags;

    internal sealed class StatsManager
    {
        private readonly IEventQueue queue;

        // clock used throughout the stats implementation
        private readonly IClock clock;

        private readonly CurrentStatsState state;
        private readonly MeasureToViewMap measureToViewMap = new MeasureToViewMap();

        internal StatsManager(IEventQueue queue, IClock clock, CurrentStatsState state)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal ISet<IView> ExportedViews
        {
            get
            {
                return measureToViewMap.ExportedViews;
            }
        }

        internal void RegisterView(IView view)
        {
            measureToViewMap.RegisterView(view, clock);
        }

        internal IViewData GetView(IViewName viewName)
        {
            return measureToViewMap.GetView(viewName, clock, state.Internal);
        }

        internal void Record(ITagContext tags, IEnumerable<IMeasurement> measurementValues)
        {
            // TODO(songya): consider exposing No-op MeasureMap and use it when stats state is DISABLED, so
            // that we don't need to create actual MeasureMapImpl.
            if (state.Internal == StatsCollectionState.ENABLED)
            {
                queue.Enqueue(new StatsEvent(this, tags, measurementValues));
            }
        }

        internal void ClearStats()
        {
            measureToViewMap.ClearStats();
        }

        internal void ResumeStatsCollection()
        {
            measureToViewMap.ResumeStatsCollection(clock.Now);
        }

        private class StatsEvent : IEventQueueEntry
        {
            private readonly ITagContext tags;
            private readonly IEnumerable<IMeasurement> stats;
            private readonly StatsManager statsManager;

            public StatsEvent(StatsManager statsManager, ITagContext tags, IEnumerable<IMeasurement> stats)
            {
                this.statsManager = statsManager;
                this.tags = tags;
                this.stats = stats;
            }

            public void Process()
            {
                // Add Timestamp to value after it went through the DisruptorQueue.
                statsManager.measureToViewMap.Record(tags, stats, statsManager.clock.Now);
            }
        }
}
}