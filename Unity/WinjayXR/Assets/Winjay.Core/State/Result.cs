using System.Collections.Generic;
using Winjay.Core.Events;

namespace Winjay.Core.State
{
    public sealed class Result
    {
        private readonly List<WinjayEvent> _events;

        private Result(bool ok, string errorCode, string error, IReadOnlyList<WinjayEvent> events)
        {
            Ok = ok;
            ErrorCode = errorCode;
            Error = error;
            _events = new List<WinjayEvent>(events);
        }

        public bool Ok { get; }
        public string ErrorCode { get; }
        public string Error { get; }
        public IReadOnlyList<WinjayEvent> Events => _events;

        internal void AttachRejectionEvent(WinjayEvent evt)
        {
            _events.Add(evt);
        }

        public static Result Success(IReadOnlyList<WinjayEvent> events)
        {
            return new Result(true, null, null, events);
        }

        public static Result Failure(string code, string message)
        {
            return new Result(false, code, message, new WinjayEvent[0]);
        }

        public static class Codes
        {
            public const string NotFound = "NOT_FOUND";
            public const string DuplicateId = "DUPLICATE_ID";
            public const string InvalidTransition = "INVALID_TRANSITION";
            public const string StaleVersion = "STALE_VERSION";
            public const string ValidationFailed = "VALIDATION_FAILED";
        }
    }
}
