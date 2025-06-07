using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace Blizztrack.Discord
{
    public class FileCommandModule(IServiceProvider provider, ILogger<FileCommandModule> logger)
        : ApplicationCommandModule<ApplicationCommandContext>
    {
        private readonly ILogger<FileCommandModule> _logger = logger;
        private readonly IServiceProvider _provider = provider;

        [SlashCommand("file", "Downloads a file given a set of filters.")]
        public async Task HandleDownloadRequest(
            [SlashCommandParameter(Name = "ekey", Description = "The encoding key to look for, if any.")]
            string? encodingKey = null,
            [SlashCommandParameter(Name = "ckey", Description = "The content key to look for, if any.")]
            string? contentKey = null,
            [SlashCommandParameter(Name = "fdid", Description = "The file ID to look for, if any.",  MinValue = 1)]
            int fileDataID = 0,
            [SlashCommandParameter(Name = "path", Description = "A (maybe partial) path to the file.")]
            string? filePath = null)
        {
            if (encodingKey == null && contentKey == null && fileDataID == 0 && filePath == null)
            {
                await Context.Interaction.SendResponseAsync(InteractionCallback.Message(new () {
                    Content = "At least one parameter must be provided.",
                    Flags = MessageFlags.Ephemeral
                }));
            }
            else
            {
                // Get the service, create the query, and execute it.
            }
        }

        [SlashCommand("binaries", "Provides links to every executable file found within a given build")]
        public async Task HandleBinariesRequest()
        {
            await Task.Yield();
        }
    }
}
