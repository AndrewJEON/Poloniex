﻿using Poloniex.Core.Domain.Constants;
using Poloniex.Core.Domain.Models;
using Poloniex.Data.Contexts;
using Poloniex.Log;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace Poloniex.Core.Implementation
{
    public class EventActionScheduler
    {
        public List<EventAction> _EventActionsToStart { get; set; }
        public List<EventAction> _EventActionsToStop { get; set; }

        public void PollForEventActionsToStart()
        {
            using (var db = new PoloniexContext())
            {
                var tmpDateTime = DateTime.UtcNow.AddMinutes(-3);
                _EventActionsToStart = db.EventActions.Where(x => x.EventActionStatus == EventActionStatus.RequestToStart && x.Task.TaskLoop.LoopStartedDateTime < tmpDateTime).ToList();
            }
        }

        public void PollForEventActionsToStop()
        {
            using (var db = new PoloniexContext())
            {
                var eventActionsToStop = db.EventActions.Where(x => x.EventActionStatus == EventActionStatus.RequestToStop).ToList();
            }
        }

        public void StartEventActions()
        {
            foreach (var ea in _EventActionsToStart)
            {
                switch (ea.EventActionType)
                {
                    case EventActionType.MovingAverage:
                        MovingAverageManager.InitEmaBySma(ea.EventActionId);
                        ea.Action = MovingAverageManager.UpdateEma;
                        break;
                }
                var globalStateEvent = new GlobalStateManager().GetTaskLoop(ea.TaskId);
                var eventActions = globalStateEvent.Item3;
                ea.EventActionStatus = EventActionStatus.Started;
                using (var db = new PoloniexContext())
                {
                    db.Entry(ea).State = EntityState.Modified;
                    db.SaveChanges();
                }
                eventActions.Add(ea);
                Logger.Write($"Started {ea.EventActionType} with eventActionId: {ea.EventActionId}", Logger.LogType.ServiceLog);
            }
        }

        public void StopEventActions()
        {
            foreach (var ea in _EventActionsToStart)
            {
                var globalStateEvent = new GlobalStateManager().GetTaskLoop(ea.TaskId);
                var eventActions = globalStateEvent.Item3;
                for (int i = 0; i < eventActions.Count; i++)
                {
                    if (eventActions[i].EventActionId == ea.EventActionId)
                    {
                        eventActions.RemoveAt(i);
                        break;
                    }
                }
                ea.EventActionStatus = EventActionStatus.Stopped;
                using (var db = new PoloniexContext())
                {
                    db.Entry(ea).State = EntityState.Modified;
                    db.SaveChanges();
                }
                Logger.Write($"Stopped {ea.EventActionType} with eventActionId: {ea.EventActionId}", Logger.LogType.ServiceLog);
            }
        }
    }
}
