﻿namespace MechanicalMilkshake.Commands;

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
            var targetMessage = Convert.ToUInt64(targetMsgId.Replace("/", ""));

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

        if (string.IsNullOrWhiteSpace(message.Content))
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent(
                "That message doesn't have any text content! I can only parse the Markdown data from messages with content (not including embeds...maybe some time in the future?)"));
            return;
        }

        var msgContentEscaped = message.Content.Replace("\\", "\\\\");
        msgContentEscaped = msgContentEscaped.Replace("`", @"\`");
        msgContentEscaped = msgContentEscaped.Replace("*", @"\*");
        msgContentEscaped = msgContentEscaped.Replace("_", @"\_");
        msgContentEscaped = msgContentEscaped.Replace("~", @"\~");
        msgContentEscaped = msgContentEscaped.Replace(">", @"\>");
        msgContentEscaped = msgContentEscaped.Replace("[", @"\[");
        msgContentEscaped = msgContentEscaped.Replace("]", @"\]");
        msgContentEscaped = msgContentEscaped.Replace("(", @"\(");
        msgContentEscaped = msgContentEscaped.Replace(")", @"\)");
        msgContentEscaped = msgContentEscaped.Replace("#", @"\#");
        msgContentEscaped = msgContentEscaped.Replace("+", @"\+");
        msgContentEscaped = msgContentEscaped.Replace("-", @"\-");
        msgContentEscaped = msgContentEscaped.Replace("=", @"\=");
        msgContentEscaped = msgContentEscaped.Replace("|", @"\|");
        msgContentEscaped = msgContentEscaped.Replace("{", @"\{");
        msgContentEscaped = msgContentEscaped.Replace("}", @"\}");
        msgContentEscaped = msgContentEscaped.Replace(".", @"\.");
        msgContentEscaped = msgContentEscaped.Replace("!", @"\!");

        DiscordEmbedBuilder embed = new()
        {
            Title = "Markdown",
            Color = Program.BotColor,
            Description = msgContentEscaped
        };

        await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
    }
}