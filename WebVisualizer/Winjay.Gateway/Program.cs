using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Winjay.Gateway
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var port = 8765;
            for (var i = 0; i < args.Length; i++)
            {
                string portValue;
                if (args[i] == "--port")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("ERROR: --port requires a value between 1 and 65535.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    portValue = args[++i];
                }
                else if (args[i].StartsWith("--port=", StringComparison.Ordinal))
                {
                    portValue = args[i].Substring(7);
                }
                else
                {
                    continue;
                }

                if (!int.TryParse(portValue, out var parsedPort) || parsedPort < 1 || parsedPort > 65535)
                {
                    Console.Error.WriteLine($"ERROR: Invalid port '{portValue}'. Provide a value between 1 and 65535.");
                    Environment.ExitCode = 1;
                    return;
                }

                port = parsedPort;
            }

            // Content root = output directory, where wwwroot was copied on build.
            var contentRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!Directory.Exists(contentRoot))
            {
                Console.WriteLine($"ERROR: wwwroot not found at {contentRoot}");
                return;
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = args,
                ContentRootPath = AppContext.BaseDirectory
            });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls($"http://localhost:{port}");

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();

            app.Map("/ws", HandleWebSocket);
            app.Map("/ws/mind", HandleMindWebSocket);

            Console.WriteLine("==============================================================");
            Console.WriteLine("  WINJAY GATEWAY — spatial state stream");
            Console.WriteLine($"  state machine:  http://localhost:{port}/");
            Console.WriteLine($"  mind map:       http://localhost:{port}/mind.html");
            Console.WriteLine("  Open in a browser, then press RUN.");
            Console.WriteLine("==============================================================");

            await app.RunAsync();
        }

        private static async Task HandleMindWebSocket(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var session = new MindSession(socket, context.RequestAborted);
            Console.WriteLine("[gateway] mind client connected");
            await session.RunAsync();
            Console.WriteLine("[gateway] mind client disconnected");
        }

        private static async Task HandleWebSocket(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            var session = new Session(socket, context.RequestAborted);
            Console.WriteLine($"[gateway] client connected ({session.ScenarioName})");
            await session.RunAsync();
            Console.WriteLine("[gateway] client disconnected");
        }
    }
}
