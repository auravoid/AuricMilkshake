﻿namespace MechanicalMilkshake.Commands;

public class ServerInfo : ApplicationCommandModule
{
    [SlashCommand("serverinfo", "Look up information about a server.")]
    [SlashRequireGuild]
    public static async Task ServerInfoCommand(InteractionContext ctx,
        [Option("server",
            "The ID of the server to look up. Defaults to the current server if you're not using this in DMs.")]
        string guildId = default)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
        
        DiscordGuild guild;
        
        if (ctx.Channel.IsPrivate && guildId == default)
        {
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent("You can't use this command in DMs without specifying a server ID! Please try again."));
            return;
        }

        if (guildId == default)
        {
            guild = ctx.Guild;
        }
        else
        {
            try
            {
                guild = await ctx.Client.GetGuildAsync(Convert.ToUInt64(guildId));
            }
            catch (Exception ex) when (ex is UnauthorizedException or NotFoundException)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent(
                        "Sorry, I can't read the details for that server because I'm not in it or it doesn't exist!"));
                return;
            }
            catch (FormatException)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().WithContent(
                        "That doesn't look like a valid server ID! Please try again."));
                return;
            }
        }
        
        var description = "None";

        if (guild.Description is not null) description = guild.Description;

        var createdAt = $"{IdHelpers.GetCreationTimestamp(guild.Id, true)}";

        var categoryCount = guild.Channels.Count(channel => channel.Value.Type == ChannelType.Category);

        var embed = new DiscordEmbedBuilder()
            .WithColor(Program.BotColor)
            .AddField("Server Owner", $"{guild.Owner.Username}#{guild.Owner.Discriminator}")
            .AddField("Description", $"{description}")
            .AddField("Created on", $"<t:{createdAt}:F> (<t:{createdAt}:R>)")
            .AddField("Channels", $"{guild.Channels.Count - categoryCount}", true)
            .AddField("Categories", $"{categoryCount}", true)
            .AddField("Roles", $"{guild.Roles.Count}", true)
            .AddField("Members (total)", $"{guild.MemberCount}", true)
            .AddField("Bots", "loading... this might take a while", true)
            .AddField("Humans", "loading... this might take a while", true)
            .WithThumbnail($"{guild.IconUrl}")
            .WithFooter($"Server ID: {guild.Id}");

        var response = new DiscordFollowupMessageBuilder()
            .WithContent($"Server Info for **{guild.Name}**").AddEmbed(embed);

        await ctx.FollowUpAsync(response);

        var members = await guild.GetAllMembersAsync();
        var botCount = members.Count(member => member.IsBot);
        var humanCount = guild.MemberCount - botCount;

        var newEmbed = response.Embeds[0];

        newEmbed.Fields.FirstOrDefault(field => field.Name == "Bots")!.Value = $"{botCount}";
        newEmbed.Fields.FirstOrDefault(field => field.Name == "Humans")!.Value = $"{humanCount}";

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response.Content).AddEmbed(embed));
    }
}