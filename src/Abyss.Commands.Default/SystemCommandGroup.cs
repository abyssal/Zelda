using Discord;
using HandlebarsDotNet;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Qmmands;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DescriptionAttribute = Qmmands.DescriptionAttribute;

namespace Abyss.Commands.Default
{
    [Name("System")]
    [Group("sys")]
    [Description("Provides commands for my creator.")]
    [RequireOwner]
    public class SystemCommandGroup : AbyssModuleBase
    {
        private readonly ILogger<SystemCommandGroup> _logger;
        private readonly DataService _dataService;

        public SystemCommandGroup(ILogger<SystemCommandGroup> logger, DataService dataService)
        {
            _logger = logger;
            _dataService = dataService;
        }

        [Command("throwex")]
        [Description("Throws a InvalidOperation .NET exception. For testing purposes.")]
        public Task<ActionResult> Command_ThrowExceptionAsync([Name("Message")] [Description("The message for the exception.")] [Remainder] string message = "Test exception.")
        {
            throw new InvalidOperationException(message);
        }

        [Command("hb")]
        [RunMode(RunMode.Parallel)]
        [Description("Evaluates and compiles a Handlebars template against the current execution context.")]
        public ActionResult Command_HandlebarsEvaluate([Name("Template")] [Description("A Handlebars-compatible template.")] [Remainder]
            string script)
        {
            var handlebars = Handlebars.Create(new HandlebarsConfiguration { });
            handlebars.RegisterHelper("create_message", async (output, context, arguments) =>
            {
                var str = string.Join(" ", arguments);
                await Context.ReplyAsync(str);
            });

            try
            {
                var hbscript = handlebars.Compile(script);

                var output = hbscript(new EvaluationHelper(Context));

                return Ok(output);
            }
            catch (HandlebarsException e)
            {
                return BadRequest("Handlebars failed: " + e.Message);
            }
            catch (Exception e)
            {
                return BadRequest("Generic error: " + e.Message);
            }
        }

        [Command("eval")]
        [RunMode(RunMode.Parallel)]
        [Description("Evaluates a piece of C# code.")]
        [ResponseFormatOptions(ResponseFormatOptions.DontEmbed)]
        public async Task<ActionResult> Command_EvaluateAsync(
            [Name("Code")] [Description("The code to execute.")] [Remainder]
            string script)
        {
            var props = new EvaluationHelper(Context);
            var result = await ScriptingHelper.EvaluateScriptAsync(script, props).ConfigureAwait(false);

            var canUseEmbed = true;
            string? stringRep;

            if (result.IsSuccess)
            {
                if (result.ReturnValue != null)
                {
                    var special = false;

                    switch (result.ReturnValue)
                    {
                        case ActionResult bresult:
                            return bresult;

                        case string str:
                            stringRep = str;
                            break;

                        case IDictionary dictionary:
                            var asb = new StringBuilder();
                            asb.AppendLine("Dictionary of type ``" + dictionary.GetType().Name + "``");
                            foreach (var ent in dictionary.Keys) asb.AppendLine($"- ``{ent}``: ``{dictionary[ent!]}``");

                            stringRep = asb.ToString();
                            special = true;
                            canUseEmbed = false;
                            break;

                        case IEnumerable enumerable:
                            var asb0 = new StringBuilder();
                            asb0.AppendLine("Enumerable of type ``" + enumerable.GetType().Name + "``");
                            foreach (var ent in enumerable) asb0.AppendLine($"- ``{ent}``");

                            stringRep = asb0.ToString();
                            special = true;
                            canUseEmbed = false;
                            break;

                        default:
                            stringRep = result.ReturnValue.ToString();
                            break;
                    }
                    if (stringRep == null)
                    {
                        stringRep = "No results returned.";
                    }

                    if (stringRep.StartsWith("```") && stringRep.EndsWith("```") || special)
                        canUseEmbed = false;
                    else
                        stringRep = $"```cs\n{stringRep}```";
                }
                else
                {
                    stringRep = "No results returned.";
                }

                if (canUseEmbed)
                {
                    var footerString =
                        $"{(result.CompilationTime != -1 ? $"Compilation time: {result.CompilationTime}ms" : "")} {(result.ExecutionTime != -1 ? $"| Execution time: {result.ExecutionTime}ms" : "")}";

                    return Ok(e => e
                        .WithTitle("Scripting Result")
                        .WithDescription(result.ReturnValue != null
                            ? "Type: `" + result.ReturnValue.GetType().Name + "`"
                            : "")
                        .AddField("Input", $"```cs\n{script}```")
                        .AddField("Output", stringRep)
                        .WithFooter(footerString, Context.BotUser.GetEffectiveAvatarUrl()));
                }

                return Ok(stringRep);
            }

            var embed = new EmbedBuilder
            {
                Title = "Scripting Result",
                Description = $"Scripting failed during stage **{FormatEnumMember(result.FailedStage)}**"
            };
            embed.AddField("Input", $"```cs\n{script}```");
            if (result.CompilationDiagnostics?.Count > 0)
            {
                var field = new EmbedFieldBuilder { Name = "Compilation Errors" };
                var sb = new StringBuilder();
                foreach (var compilationDiagnostic in result.CompilationDiagnostics.OrderBy(a =>
                    a.Location.SourceSpan.Start))
                {
                    var start = compilationDiagnostic.Location.SourceSpan.Start;
                    var end = compilationDiagnostic.Location.SourceSpan.End;

                    var bottomRow = script[start..end];

                    if (!string.IsNullOrEmpty(bottomRow)) sb.AppendLine("`" + bottomRow + "`");
                    sb.AppendLine(
                        $" - ``{compilationDiagnostic.Id}`` ({FormatDiagnosticLocation(compilationDiagnostic.Location)}): **{compilationDiagnostic.GetMessage()}**");
                    sb.AppendLine();
                }

                field.Value = sb.ToString();

                if (result.Exception != null) sb.AppendLine();
                embed.AddField(field);
            }

            if (result.Exception != null)
                embed.AddField("Exception", $"``{result.Exception.GetType().Name}``: ``{result.Exception.Message}``");
            return Ok(embed);
        }

        private static string FormatEnumMember(Enum value)
        {
            return value.ToString().Replace(value.GetType().Name + ".", "");
        }

        private static string FormatDiagnosticLocation(Location loc)
        {
            if (!loc.IsInSource) return "Metadata";
            if (loc.SourceSpan.Start == loc.SourceSpan.End) return "Ch " + loc.SourceSpan.Start;
            return $"Ch {loc.SourceSpan.Start}-{loc.SourceSpan.End}";
        }

        [Command("inspect")]
        [RunMode(RunMode.Parallel)]
        [Description("Evaluates and then inspects a type.")]
        public Task<ActionResult> Command_InspectObjectAsync(
            [Name("Object")] [Description("The object to inspect.")] [Remainder]
            string evaluateScript)
        {
            return Command_EvaluateAsync($"Inspect({evaluateScript})");
        }

        [Command("kill")]
        [Description("Stops the current bot process.")]
        [RunMode(RunMode.Parallel)]
        [RequireOwner]
        public async Task<ActionResult> Command_ShutdownAsync()
        {
            await ReplyAsync($"Later.").ConfigureAwait(false);
            _logger.LogInformation($"Application terminated by user {Context.Invoker} (ID {Context.Invoker.Id})");
            await Context.Client.LogoutAsync().ConfigureAwait(false);
            await Context.Client.StopAsync().ConfigureAwait(false);
            Context.Client.Dispose();

            Environment.Exit(0);
            return Empty();
        }

        [Command("edit")]
        [Description("Edits a message that was sent by me.")]
        [RequireOwner]
        public async Task<ActionResult> Command_EditAsync([Name("Message")] [Description("The message to edit.")]
            ulong messageId, [Name("New Content")] [Description("The new content of the message.")] [Remainder] string newContent)
        {
            var channel = Context.Channel;
            var message = channel.GetCachedMessage(messageId) ?? await channel.GetMessageAsync(messageId);
            if (message == null || !(message is IUserMessage msg) || msg.Author.Id != Context.Bot.Id)
                return BadRequest("Unknown message!");

            try
            {
                if (string.IsNullOrEmpty(message.Content))
                {
                    var embed = msg.Embeds.FirstOrDefault();
                    if (embed != null && embed.Type == EmbedType.Rich)
                    {
                        var newEmbed = embed.ToEmbedBuilder();
                        newEmbed.Description = newContent;
                        await msg.ModifyAsync(v => v.Embed = newEmbed.Build());
                    }
                }
                else
                {
                    await msg.ModifyAsync(v => v.Content = newContent);
                }
                return OkReaction();
            }
            catch (Exception)
            {
                return BadRequest("Failed to modify the message!");
            }
        }

        [Command("exec")]
        [Description("Executes an executable on the host platform.")]
        [RequireOwner]
        [RunMode(RunMode.Parallel)]
        public async Task<ActionResult> Command_ExecuteAsync([Name("Executable")] [Description("The executable to run.")] string executable,
            [Name("Arguments")] [Description("The arguments to provide.")] [Remainder] string? arguments = null)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    Arguments = arguments,
                    UseShellExecute = false,
                    FileName = executable
                }
            };
            process.Start();
            if (!process.WaitForExit(8000))
            {
                return BadRequest("Timed out after 8 seconds.");
            }
            var result = await process.StandardOutput.ReadToEndAsync();
            if (result.Length > 2048) result = result.Substring(0, 2000);
            return Ok(c =>
            {
                c.Description = new StringBuilder().AppendLine("Standard output:").AppendLine($"```{result}```").ToString();
            });
        }

        [Command("info")]
        [Description(
            "Dumps current information about the client, the commands system and the current execution environment.")]
        [RequireOwner]
        public Task<ActionResult> Command_SysInfoAsync()
        {
            var info = _dataService.GetServiceInfo();
            return Ok(e =>
            {
                e.Author = Context.BotUser.ToEmbedAuthorBuilder();
                e.Description = $"{info.ServiceName} instance running on {info.OperatingSystem} (runtime version {info.RuntimeVersion}), powering {info.Guilds} guilds ({info.Channels} channels, and {info.Users} users)";
                e.AddField("Command statistics", $"{info.Modules} modules | {info.Commands} commands | {info.CommandSuccesses} successful calls | {info.CommandFailures} unsuccessful calls");
                e.AddField("Process statistics", $"Process name {info.ProcessName} on machine name {info.MachineName} (thread {info.CurrentThreadId}, {info.ProcessorCount} processors)");
                e.AddField("Content root", info.ContentRootPath);
                e.AddField("Start time", info.StartTime.ToString("F"));
                e.AddField("Service count", info.ServicesRegistered, true);
            });
        }
    }
}
