using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Winjay.Agents;
using Winjay.Core.Events;
using Winjay.Core.Model;
using Winjay.Core.State;

namespace Winjay.Gateway
{
    /// <summary>
    /// One WebSocket session = one isolated Winjay world (engine + agents +
    /// runner). The browser drives it with run/step/reset and receives
    /// tick batches: proposal steps + semantic events + entity snapshots.
    /// </summary>
    public sealed class Session
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

        private WinjayEventBus _bus;
        private WinjayStateEngine _engine;
        private ScenarioRunner _runner;
        private string _scenario = "conflict";
        private int _speedMs = 600;
        private long _lastSentSeq;
        private int _lastTick;
        private CancellationTokenSource _runCts;

        public Session(System.Net.WebSockets.WebSocket socket, CancellationToken lifetime)
        {
            _socket = socket;
            _lifetime = lifetime;
            Reset();
        }

        public string ScenarioName => _scenario;

        public async Task RunAsync()
        {
            try
            {
                await SendAsync(new { type = "hello", scenario = ScenarioTitle(_scenario), speed = _speedMs });

                var buffer = new byte[8192];
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
                        await TickOnceAndSendAsync();
                        break;

                    case "reset":
                        CancelRun();
                        Reset();
                        await SendAsync(new { type = "hello", scenario = ScenarioTitle(_scenario), speed = _speedMs });
                        break;

                    case "speed":
                        if (root.TryGetProperty("ms", out var msProp) && msProp.TryGetInt32(out var ms))
                        {
                            _speedMs = Math.Clamp(ms, 50, 5000);
                        }
                        break;

                    case "scenario":
                        if (root.TryGetProperty("name", out var nameProp))
                        {
                            CancelRun();
                            _scenario = nameProp.GetString() == "resolve" ? "resolve" : "conflict";
                            Reset();
                            await SendAsync(new { type = "hello", scenario = ScenarioTitle(_scenario), speed = _speedMs });
                        }
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
                    if (!await TickOnceAndSendAsync())
                    {
                        await SendAsync(new { type = "done", ticks = _lastTick });
                        break;
                    }
                    await Task.Delay(_speedMs, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private async Task<bool> TickOnceAndSendAsync()
        {
            List<StepDto> steps;
            List<EventDto> events;
            List<EntityDto> entities;

            lock (_sync)
            {
                if (!_runner.Tick()) return false;

                _lastTick = _runner.LastSteps.Count > 0 ? _runner.LastSteps[0].Tick : _lastTick;
                steps = _runner.LastSteps.Select(StepDto.From).ToList();
                events = _bus.Journal
                    .Where(e => e.Sequence > _lastSentSeq)
                    .Select(EventDto.From)
                    .ToList();
                entities = new List<EntityDto>();
                foreach (var evt in _bus.Journal.Where(e => e.Sequence > _lastSentSeq))
                {
                    if (evt.Type == WinjayEventType.EntityCreated && evt.EntityId != null
                        && _engine.Entities.TryGetValue(evt.EntityId, out var entity))
                    {
                        entities.Add(EntityDto.From(entity));
                    }
                }
                if (events.Count > 0) _lastSentSeq = events.Max(e => e.Seq);
            }

            await SendAsync(new
            {
                type = "tick",
                tick = steps.Count > 0 ? steps[0].Tick : 0,
                steps,
                events,
                entities
            });
            return true;
        }

        private void Reset()
        {
            lock (_sync)
            {
                _bus = new WinjayEventBus();
                _engine = new WinjayStateEngine(_bus);
                var agents = _scenario == "resolve" ? ResolveAgents() : ConflictAgents();
                _runner = new ScenarioRunner(_engine, _bus, agents);
                _lastSentSeq = 0;
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

        private static IWinjayAgent[] ConflictAgents()
        {
            return new IWinjayAgent[]
            {
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81),
                    new EvidenceScriptItem("E002", "Evidence B", 0.70)),
                new MockAnalyzer("MockAnalyzer", "H001", resolveWhenFullySupported: false, "E001", "E002"),
                new MockFalsifier("MockFalsifier", "H001",
                    new EvidenceScriptItem("E003", "Contradicting observation", 0.90)),
                new MockDecision("MockDecision", "H001"),
            };
        }

        private static IWinjayAgent[] ResolveAgents()
        {
            return new IWinjayAgent[]
            {
                new MockResearcher("MockResearcher",
                    new EvidenceScriptItem("E001", "Evidence A", 0.81)),
                new MockAnalyzer("MockAnalyzer", "H001", resolveWhenFullySupported: true, "E001"),
                new MockFalsifier("MockFalsifier", "H001", contradiction: null),
                new MockDecision("MockDecision", "H001"),
            };
        }

        private static string ScenarioTitle(string scenario)
        {
            return scenario == "resolve" ? "ScenarioResolve" : "DemoScenario01";
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
