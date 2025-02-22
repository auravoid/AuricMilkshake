﻿namespace MechanicalMilkshake.Checks;

public class ReminderChecks
{
    public static async Task ReminderCheck()
    {
        var reminders = await Program.Db.HashGetAllAsync("reminders");

        foreach (var reminder in reminders)
        {
            var reminderData = JsonConvert.DeserializeObject<Reminder>(reminder.Value);

            if (reminderData!.ReminderTime is null) continue;
            if (reminderData!.ReminderTime > DateTime.Now) continue;

            var setTime = ((DateTimeOffset)reminderData.SetTime).ToUnixTimeSeconds();
            DiscordEmbedBuilder embed = new()
            {
                Color = new DiscordColor("#7287fd"),
                Title = $"Reminder from <t:{setTime}:R>",
                Description = $"{reminderData.ReminderText}"
            };

            string context;
            if (reminderData.IsPrivate)
                context =
                    "This reminder was set privately, so I can't link back to the message where it was set!" +
                    $" However, [this link](https://discord.com/channels/{reminderData.GuildId}" +
                    $"/{reminderData.ChannelId}/{reminderData.MessageId}) should show you messages around the time" +
                    " that you set the reminder.";
            else
                context =
                    $"[Jump Link](https://discord.com/channels/{reminderData.GuildId}/{reminderData.ChannelId}" +
                    $"/{reminderData.MessageId})";

            embed.AddField("Context", context);

            AddReminderPushbackEmbedField(embed);

            DiscordMember targetMember;
            if (reminderData.GuildId == "@me")
            {
                DiscordUser user;
                try
                {
                    user = await Program.Discord.GetUserAsync(reminderData.UserId);
                }
                catch
                {
                    // Reminder will not be sent because user cannot be fetched..? Delete reminder to prevent error spam
                    await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);
                    return;
                }

                DiscordGuild mutualServer = default;
                foreach (var guild in Program.Discord.Guilds)
                    if (guild.Value.Members.Any(m =>
                            m.Value.Username == user.Username && m.Value.Discriminator == user.Discriminator))
                    {
                        mutualServer = await Program.Discord.GetGuildAsync(guild.Value.Id);
                        break;
                    }

                if (mutualServer == default)
                {
                    // Reminder cannot be sent because there's no way to DM the user... delete it to prevent error spam
                    await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);
                    return;
                }

                targetMember = await mutualServer.GetMemberAsync(user.Id);
            }
            else
            {
                var guildId = Convert.ToUInt64(reminderData.GuildId);
                var guild = await Program.Discord.GetGuildAsync(guildId);
                targetMember = await guild.GetMemberAsync(reminderData.UserId);
            }

            if (reminderData.IsPrivate)
                try
                {
                    // Try to DM user for private reminder

                    var msg = await targetMember.SendMessageAsync(
                        $"<@{reminderData.UserId}>, I have a reminder for you:",
                        embed);

                    embed.RemoveFieldAt(1);
                    AddReminderPushbackEmbedField(embed, msg.Id);
                    await msg.ModifyAsync(msg.Content, embed.Build());

                    await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);

                    return;
                }
                catch
                {
                    // Couldn't DM user for private reminder - DMs are disabled or bot is blocked. Try to ping in public channel.
                    try
                    {
                        var reminderCmd = Program.ApplicationCommands.FirstOrDefault(c => c.Name == "reminder");

                        var reminderChannel = await Program.Discord.GetChannelAsync(reminderData.ChannelId);
                        var msgContent =
                            $"Hi {targetMember.Mention}! I have a reminder for you. I tried to DM it to you, but either your DMs are off or I am blocked." +
                            " Please make sure that I can DM you.\n\n**Your reminder will not be sent automatically following this alert.**";

                        if (reminderCmd is not null)
                            msgContent +=
                                $" You can use </{reminderCmd.Name} modify:{reminderCmd.Id}> to modify your reminder, or </{reminderCmd.Name} delete:{reminderCmd.Id}> to delete it.";

                        await reminderChannel.SendMessageAsync(msgContent);

                        reminderData.ReminderTime = null;
                        await Program.Db.HashSetAsync("reminders", reminderData.ReminderId,
                            JsonConvert.SerializeObject(reminderData));

                        return;
                    }
                    catch (Exception ex)
                    {
                        // Couldn't DM user or send an alert in the channel the reminder was set in... log error
                        // Also delete reminder to prevent error spam...

                        await LogReminderError(Program.HomeChannel, ex);

                        await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);

                        return;
                    }
                }

            try
            {
                var targetChannel = await Program.Discord.GetChannelAsync(reminderData.ChannelId);
                var msg = await targetChannel.SendMessageAsync(
                    $"<@{reminderData.UserId}>, I have a reminder for you:",
                    embed);

                embed.RemoveFieldAt(1);
                AddReminderPushbackEmbedField(embed, msg.Id);
                await msg.ModifyAsync(msg.Content, embed.Build());

                await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);

                return;
            }
            catch
            {
                try
                {
                    // Couldn't send the reminder in the channel it was created in.
                    // Try to DM user instead.

                    var msg = await targetMember.SendMessageAsync(
                        $"<@{reminderData.UserId}>, I have a reminder for you:",
                        embed);

                    embed.RemoveFieldAt(1);
                    AddReminderPushbackEmbedField(embed, msg.Id);
                    await msg.ModifyAsync(msg.Content, embed.Build());

                    await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);

                    return;
                }
                catch (Exception ex)
                {
                    // Couldn't DM user. Log error.
                    // Delete reminder to prevent error spam...

                    await LogReminderError(Program.HomeChannel, ex);

                    await Program.Db.HashDeleteAsync("reminders", reminderData.ReminderId);

                    return;
                }
            }
        }
    }

    private static async Task LogReminderError(DiscordChannel logChannel, Exception ex)
    {
        DiscordEmbedBuilder errorEmbed = new()
        {
            Color = DiscordColor.Red,
            Title = "An exception occurred when checking reminders",
            Description =
                $"`{ex.GetType()}` occurred when checking for overdue reminders."
        };
        errorEmbed.AddField("Message", $"{ex.Message}");
        errorEmbed.AddField("Stack Trace", $"```\n{ex.StackTrace}\n```");

        Console.WriteLine(
            $"{ex.GetType()} occurred when checking reminders: {ex.Message}\n{ex.StackTrace}");

        await logChannel.SendMessageAsync(errorEmbed);
    }

    private static void AddReminderPushbackEmbedField(DiscordEmbedBuilder embed, ulong msgId = default)
    {
        var id = msgId == default ? "[loading...]" : $"`{msgId}`";

        embed.AddField("Need to delay this reminder?",
            $"Use {SlashCmdMentionHelpers.GetSlashCmdMention("reminder", "pushback")}and set `message` to {id}.");
    }
}