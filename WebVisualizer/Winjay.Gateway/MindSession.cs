using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Winjay.Agents.Mind;
using Winjay.Core.Mind;

namespace Winjay.Gateway
{
    /// <summary>
    /// One WebSocket session = one living mind map (engine + agents + story).
    /// The browser drives it with run/step/reset and curator actions, and
    /// receives full-payload events — the client rebuilds the map purely by
    /// folding the event log, which is also what powers its rewind slider.
    /// </summary>
    public sealed class MindSession
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly System.Net.WebSockets.WebSocket _socket;
        private readonly CancellationToken _lifetime;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _sync = new object();

        private DemoMindStory _story;
        private int _speedMs = 700;
        private long _lastSentSeq;
        private int _sayCount;
        private CancellationTokenSource _runCts;

        public MindSession(System.Net.WebSockets.WebSocket socket, CancellationToken lifetime)
        {
            _socket = socket;
            _lifetime = lifetime;
            lock (_sync)
            {
                Reset();
            }
        }

        public async Task RunAsync()
        {
            try
            {
                await SendHelloAsync();

                var buffer = new byte[16384];
                while (!_lifetime.IsCancellationRequested)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _lifetime);
                    if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                        break;

                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessage(text);
                }
            }
            catch (OperationCanceledException) { }
            catch (System.Net.WebSockets.WebSocketException) { }
            finally
            {
                CancelRun();
            }
        }

        private async Task HandleMessage(string text)
        {
            JsonDocument doc = null;
            try { doc = JsonDocument.Parse(text); }
            catch (JsonException) { return; }

            using (doc)
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeProp)) return;
                var type = typeProp.GetString();

                switch (type)
                {
                    case "run":
                        CancelRun();
                        _runCts = new CancellationTokenSource();
                        _ = AutoRunAsync(_runCts.Token);
                        break;

                    case "step":
                        CancelRun();
                        await StepOnceAndSendAsync();
                        break;

                    case "reset":
                        CancelRun();
                        lock (_sync) { Reset(); }
                        await SendHelloAsync();
                        break;

                    case "speed":
                        if (root.TryGetProperty("ms", out var msProp) && msProp.TryGetInt32(out var ms))
                            _speedMs = Math.Clamp(ms, 50, 5000);
                        break;

                    case "prune":
                        await HumanActionAsync(root, id => new PruneMindNode { Actor = "Human", Id = id }, "prune");
                        break;

                    case "lock":
                        await HumanActionAsync(root, id => new LockMindNode { Actor = "Human", Id = id }, "lock");
                        break;

                    case "unlock":
                        await HumanActionAsync(root, id => new UnlockMindNode { Actor = "Human", Id = id }, "unlock");
                        break;

                    case "acceptGhost":
                        await HumanActionAsync(root, id => new AcceptMindGhost { Actor = "Human", Id = id }, "accept the suggestion");
                        break;

                    case "rejectGhost":
                        await HumanActionAsync(root, id => new RejectMindGhost { Actor = "Human", Id = id }, "reject the suggestion");
                        break;

                    case "say":
                        await SayAsync(root);
                        break;

                    case "merge":
                        await MergeAsync(root);
                        break;
                }
            }
        }

        private async Task AutoRunAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var progressed = await StepOnceAndSendAsync();
                    if (!progressed)
                    {
                        await SendAsync(new
                        {
                            type = "done",
                            settled = true,
                            reason = _story.SettledReason,
                            ticks = _story.Engine.Journal.Count
                        });
                        break;
                    }
                    await Task.Delay(_speedMs, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>One story step (agent tick or scripted curator step). False when settled.</summary>
        private async Task<bool> StepOnceAndSendAsync()
        {
            IReadOnlyList<MindStoryStep> fresh;
            lock (_sync)
            {
                var before = _story.Steps.Count;
                if (!_story.Step()) return false;
                fresh = _story.Steps.Skip(before).ToList();
            }
            await SendStepAsync(fresh);
            return true;
        }

        private async Task HumanActionAsync(JsonElement root, Func<string, IMindAction> build, string verb)
        {
            if (!root.TryGetProperty("id", out var idProp)) return;
            var id = idProp.GetString();
            if (string.IsNullOrWhiteSpace(id)) return;

            var action = build(id);
            MindStoryStep step;
            lock (_sync)
            {
                var result = _story.Engine.Apply(action);
                step = _story.RecordHuman(action, result, verb + " " + id);
            }
            await SendStepAsync(new[] { step });
        }

        private async Task SayAsync(JsonElement root)
        {
            if (!root.TryGetProperty("label", out var labelProp)) return;
            var label = labelProp.GetString();
            if (string.IsNullOrWhiteSpace(label)) return;

            string parent = null;
            if (root.TryGetProperty("parentId", out var parentProp)) parent = parentProp.GetString();

            MindStoryStep step;
            lock (_sync)
            {
                var engine = _story.Engine;
                // Always a fresh SAYnn id: the scripted story owns well-known ids
                // like MOTION, and an ad-hoc speak must never collide with them.
                var id = "SAY" + (++_sayCount).ToString("00");
                while (engine.GetNode(id) != null) id = "SAY" + (++_sayCount).ToString("00");
                parent = parent ?? (engine.Root != null ? engine.Root.Id : null);
                var angle = 330 + _sayCount * 15;
                var a = angle * Math.PI / 180.0;
                var action = new MindHumanInput
                {
                    Actor = "Human", Id = id, Label = label, ParentId = parent,
                    Position = new MindVec3((float)(3.2 * Math.Cos(a)), 0f, (float)(3.2 * Math.Sin(a)))
                };
                var result = engine.Apply(action);
                step = _story.RecordHuman(action, result, "says: \"" + label + "\"");
            }
            await SendStepAsync(new[] { step });
        }

        private async Task MergeAsync(JsonElement root)
        {
            if (!root.TryGetProperty("fromId", out var fromProp)) return;
            if (!root.TryGetProperty("intoId", out var intoProp)) return;
            var fromId = fromProp.GetString();
            var intoId = intoProp.GetString();

            var action = new MergeMindNodes { Actor = "Human", FromId = fromId, IntoId = intoId };
            MindStoryStep step;
            lock (_sync)
            {
                var result = _story.Engine.Apply(action);
                step = _story.RecordHuman(action, result,
                    "merges " + fromId + " into " + intoId + " (links rewired)");
            }
            await SendStepAsync(new[] { step });
        }

        private void Reset()
        {
            _story = DemoMindScenario.CreateStory();
            _lastSentSeq = 0;
            _sayCount = 0;
        }

        private async Task SendHelloAsync()
        {
            List<MindEventDto> journal;
            string state;
            lock (_sync)
            {
                journal = _story.Engine.Journal.Select(MindEventDto.From).ToList();
                _lastSentSeq = journal.Count > 0 ? journal[journal.Count - 1].Seq : 0;
                state = _story.Settled ? "settled" : "ready";
            }

            await SendAsync(new
            {
                type = "hello",
                scenario = DemoMindScenario.TopicLabel,
                speed = _speedMs,
                actors = new[] { "Explorer", "Connector", "Challenger", "Human" },
                state,
                journal
            });
        }

        private async Task SendStepAsync(IReadOnlyList<MindStoryStep> steps)
        {
            List<MindEventDto> events;
            string state;
            string settledReason = null;
            int totalEvents;
            lock (_sync)
            {
                events = _story.Engine.Journal
                    .Where(e => e.Sequence > _lastSentSeq)
                    .Select(MindEventDto.From)
                    .ToList();
                if (events.Count > 0) _lastSentSeq = events[events.Count - 1].Seq;

                totalEvents = _story.Engine.Journal.Count;
                state = _story.Settled
                    ? "settled"
                    : steps.Any(s => s.Actor != "Human")
                        ? "busy"
                        : "idle";
                settledReason = _story.SettledReason;
            }

            await SendAsync(new
            {
                type = "step",
                state,
                settledReason,
                totalEvents,
                actions = steps.Select(s => new
                {
                    tick = s.Tick,
                    actor = s.Actor,
                    note = s.Note ?? DescribeAction(s.Action),
                    ok = s.Result != null && s.Result.Ok,
                    code = s.Result?.ErrorCode
                }).ToList(),
                events
            });
        }

        private static string DescribeAction(IMindAction action)
        {
            switch (action)
            {
                case AddMindNode a: return "grows \"" + a.Label + "\"";
                case AddMindLink a: return a.Type.ToString().ToLowerInvariant() + " link " + a.FromId + " -> " + a.ToId;
                case PruneMindNode a: return "prunes " + a.Id;
                case LockMindNode a: return "locks " + a.Id;
                case UnlockMindNode a: return "unlocks " + a.Id;
                case MindHumanInput a: return "says \"" + a.Label + "\"";
                case MergeMindNodes a: return "merges " + a.FromId + " into " + a.IntoId;
                case AcceptMindGhost a: return "accepts ghost " + a.Id;
                case RejectMindGhost a: return "rejects ghost " + a.Id;
                case AssignMindPhase a: return "settles " + a.Id + " into " + a.Phase;
                default: return action.GetType().Name;
            }
        }

        private void CancelRun()
        {
            var cts = _runCts;
            _runCts = null;
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private async Task SendAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(_lifetime);
            try
            {
                await _socket.SendAsync(new ArraySegment<byte>(bytes),
                    System.Net.WebSockets.WebSocketMessageType.Text, true, _lifetime);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
