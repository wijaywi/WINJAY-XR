using Winjay.Core.Events;
using Winjay.Core.State;

namespace Winjay.Tests
{
    internal static class TestHarness
    {
        public static (WinjayEventBus Bus, WinjayStateEngine Engine) NewEngine()
        {
            var bus = new WinjayEventBus();
            return (bus, new WinjayStateEngine(bus));
        }
    }
}
