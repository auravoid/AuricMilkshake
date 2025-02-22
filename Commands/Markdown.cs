namespace MechanicalMilkshake.Commands;

public class Markdown : ApplicationCommandModule
{
    [SlashCommand("markdown", "Expose the Markdown formatting behind a message!")]
    public static async Task MarkdownCommand(InteractionContext ctx,
        [Option("message", "The message you want to expose the formatting of. Accepts message IDs and links.")]
        string messageToExpose)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

        DiscordMessage message;
        if (!Regex.IsMatch(messageToExpose, @".*.discord.com\/channels\/([\d+]*\/)+[\d+]*"))
        {
            if (messageToExpose.Length < 17)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "Hmm, that doesn't look like a valid message ID or link."));
                return;
            }

            ulong messageId;
            try
            {
                messageId = Convert.ToUInt64(messageToExpose);
            }
            catch
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "Hmm, that doesn't look like a valid message ID or link. I wasn't able to get the Markdown data from it."));
                return;
            }

            try
            {
                message = await ctx.Channel.GetMessageAsync(messageId);
            }
            catch
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "I wasn't able to read that message! Make sure I have permission to access it."));
                return;
            }
        }
        else
        {
            // Assume the user provided a message link. Extract channel and message IDs to get message content.

            // Extract all IDs from URL. This will leave you with something like "guild_id/channel_id/message_id".
            // Remove the guild ID, leaving you with "channel_id/message_id".
            Regex extractId = new(@".*.discord.com\/channels\/(\d+/)");
            var selectionToRemove = extractId.Match(messageToExpose);
            messageToExpose = messageToExpose.Replace(selectionToRemove.ToString(), "");

            // If IDs have letters in them, the user provided an invalid link.
            if (Regex.IsMatch(messageToExpose, @"[A-z]"))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "Hmm, that doesn't look like a valid message ID or link. I wasn't able to get the Markdown data from it."));
                return;
            }

            // Extract channel ID. This will leave you with "/channel_id".
            Regex getChannelId = new(@"[0-9]+\/");
            var channelId = getChannelId.Match(messageToExpose);
            // Remove '/' to get "channel_id"
            var targetChannelId = Convert.ToUInt64(channelId.ToString().Replace("/", ""));

            DiscordChannel channel;
            try
            {
                channel = await ctx.Client.GetChannelAsync(targetChannelId);
            }
            catch
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "I wasn't able to find that message! Make sure I have permission to see the channel it's in."));
                return;
            }

            // Now we have the channel ID and need to get the message inside that channel. To do this we'll need the message ID from what we had before...

            Regex getMessageId = new(@"[0-9]+\/");
            var idsToRemove = getMessageId.Match(messageToExpose);
            var targetMsgId = messageToExpose.Replace(idsToRemove.ToString(), "");

            // Remove '/' to get "message_id"
            targetMsgId = Regex.Replace(targetMsgId, @"[^\d]", "");
            var targetMessage = Convert.ToUInt64(targetMsgId);

            try
            {
                message = await channel.GetMessageAsync(targetMessage);
            }
            catch
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                    "I wasn't able to read that message! Make sure I have permission to access it."));
                return;
            }
        }

        var markdown = message.Content;
        var embeds = message.Embeds;

        var response = new DiscordFollowupMessageBuilder()
            .WithContent($"Here's the Markdown data for [that message]({message.JumpLink}):");

        if (!string.IsNullOrWhiteSpace(markdown))
            response.AddEmbed(new DiscordEmbedBuilder()
                .WithTitle("Message Content")
                .WithDescription(MarkdownHelpers.Parse(markdown))
                .WithColor(Program.BotColor));

        if (embeds.Count > 0)
            foreach (var embed in embeds)
            {
                if (response.Embeds.Count >= 10)
                    continue;
                if (embed.Type == "image")
                    continue;
                if (string.IsNullOrWhiteSpace(embed.Title) && string.IsNullOrWhiteSpace(embed.Description) &&
                    string.IsNullOrWhiteSpace(embed.Footer?.Text) && string.IsNullOrWhiteSpace(embed.Author?.Name) &&
                    string.IsNullOrWhiteSpace(embed.Fields.ToString()))
                    continue;

                var markdownEmbed = new DiscordEmbedBuilder()
                    .WithTitle(string.IsNullOrWhiteSpace(embed.Title)
                        ? "Embed Content"
                        : $"Embed Content: {MarkdownHelpers.Parse(embed.Title)}")
                    .WithDescription(embeds[0].Description is not null ? MarkdownHelpers.Parse(embed.Description) : "")
                    .WithColor(embed.Color.HasValue == false ? Program.BotColor : embed.Color.Value);

                if (embed.Fields is not null)
                    foreach (var field in embed.Fields)
                        markdownEmbed.AddField(MarkdownHelpers.Parse(field.Name), MarkdownHelpers.Parse(field.Value),
                            field.Inline);
                response.AddEmbed(markdownEmbed);
            }

        if (response.Embeds.Count == 0)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                "That message doesn't have any text content! I can only parse the Markdown data from messages with content."));
            return;
        }

        // If the embeds have more than 6000 characters, return a kind message instead of a 400 error.
        if (response.Embeds.Sum(e =>
                e.Description.Length + e.Title.Length + e.Fields.Sum(f => f.Name.Length + f.Value.Length)) > 6000)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                "That message is too long! I can only parse the Markdown data from messages shorter than 6000 characters."));
            return;
        }

        await ctx.FollowUpAsync(response);
    }
}