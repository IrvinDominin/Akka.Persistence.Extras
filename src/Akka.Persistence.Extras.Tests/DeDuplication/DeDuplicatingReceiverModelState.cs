// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverModelState.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverModelState : IReceiverState
    {
        public DeDuplicatingReceiverModelState(ImmutableDictionary<string, DateTime> senderLru,
            ImmutableDictionary<string, ImmutableHashSet<long>> senderIds, DateTime currentTime)
        {
            SenderLru = senderLru;
            SenderIds = senderIds;
            CurrentTime = currentTime;
        }

        public DateTime CurrentTime { get; }

        public ImmutableDictionary<string, DateTime> SenderLru { get; private set; }

        public ImmutableDictionary<string, ImmutableHashSet<long>> SenderIds { get; }
        public ReceiveOrdering Ordering => ReceiveOrdering.AnyOrder;

        public IReceiverState ConfirmProcessing(long confirmationId, string senderId)
        {
            UpdateLru(senderId);
            var buffer = SenderIds.ContainsKey(senderId)
                ? SenderIds[senderId]
                : ImmutableHashSet<long>.Empty;

            return new DeDuplicatingReceiverModelState(SenderLru,
                SenderIds.SetItem(senderId, buffer.Add(confirmationId)),
                CurrentTime);
        }

        public bool AlreadyProcessed(long confirmationId, string senderId)
        {
            UpdateLru(senderId);
            return SenderIds.ContainsKey(senderId) &&
                   SenderIds[senderId].Contains(confirmationId);
        }

        public IReadOnlyDictionary<string, DateTime> TrackedSenders => SenderLru;

        public (IReceiverState newState, IReadOnlyList<string> prunedSenders) Prune(TimeSpan notUsedSince)
        {
            var targetTime = CurrentTime - notUsedSince;
            var prunedSenderIds = SenderLru.Where(x => x.Value <= targetTime).Select(x => x.Key).ToList();
            return (
                new DeDuplicatingReceiverModelState(SenderLru.RemoveRange(prunedSenderIds),
                    SenderIds.RemoveRange(prunedSenderIds), CurrentTime), prunedSenderIds);
        }

        public IReceiverStateSnapshot ToSnapshot()
        {
            throw new NotImplementedException();
        }

        public IReceiverState FromSnapshot(IReceiverStateSnapshot snapshot)
        {
            throw new NotImplementedException();
        }

        public DeDuplicatingReceiverModelState AddTime(TimeSpan additionalTime)
        {
            return new DeDuplicatingReceiverModelState(SenderLru, SenderIds, CurrentTime + additionalTime);
        }

        private void UpdateLru(string senderId)
        {
            SenderLru = SenderLru.SetItem(senderId, CurrentTime);
        }

        public override string ToString()
        {
            return
                $"DeDuplicatingReceiverModel(CurrentTime={CurrentTime}, SenderLru=[{string.Join(",", SenderLru.Select(x => x.Key + ":" + x.Value))}]," +
                $"SenderRecvCounts=[{string.Join(",", SenderIds.Select(x => x.Key + "->" + x.Value.Count))}]";
        }
    }
}