namespace MechanicalMilkshake;

public class PerServerFeatures
{
    public class ComplaintSlashCommands : ApplicationCommandModule
    {
        [SlashCommand("complaint", "File a complaint to a specific department.")]
        public static async Task Complaint(InteractionContext ctx,
            [Choice("hr", "HR")]
            [Choice("ia", "IA")]
            [Choice("it", "IT")]
            [Choice("corporate", "Corporate")]
            [Option("department", "The department to send the complaint to.")]
            string department,
            [Option("complaint", "Your complaint.")] [MaximumLength(4000)]
            string complaint)
        {
            if (ctx.Guild.Id != 631118217384951808 && ctx.Guild.Id != 984903591816990730 &&
                ctx.Guild.Id != Program.HomeServer.Id)
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                    .WithContent("This command is not available in this server.").AsEphemeral());
                return;
            }

            if (department != "HR" && department != "IA" && department != "IT" && department != "Corporate")
            {
                await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder()
                    .WithContent("Please choose from one of the four departments to send your complaint to.")
                    .AsEphemeral());
                return;
            }

            await ctx.CreateResponseAsync(new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder
            {
                Title = "Your complaint has been recorded", Color = Program.BotColor,
                Description =
                    $"You will be contacted soon about your issue. You can see your complaint below.\n> {complaint}"
            }).AsEphemeral());

            var logChannel = await ctx.Client.GetChannelAsync(968515974271741962);
            DiscordEmbedBuilder embed = new()
            {
                Title = "New complaint received!",
                Color = Program.BotColor,
                Description = complaint
            };
            embed.AddField("Sent by", $"{ctx.User.Username}#{ctx.User.Discriminator} (`{ctx.User.Id}`)");
            embed.AddField("Sent from", $"\"{ctx.Guild.Name}\" (`{ctx.Guild.Id}`)");
            embed.AddField("Department", department);
            await logChannel.SendMessageAsync(embed);
        }
    }

    public class RoleCommands : ApplicationCommandModule
    {
        [SlashCommand("rolename", "Change the name of your role.")]
        public static async Task RoleName(InteractionContext ctx,
            [Option("name", "The name to change to.")]
            string name)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AsEphemeral());

            if (ctx.Guild.Id != 984903591816990730)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent("This command is not available in this server.").AsEphemeral());
                return;
            }

            List<DiscordRole> roles = new();
            if (ctx.Member.Roles.Any())
            {
                roles.AddRange(ctx.Member.Roles.OrderBy(role => role.Position).Reverse());
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("You don't have any roles.")
                    .AsEphemeral());
                return;
            }

            if (roles.Count == 1 && roles.First().Id is 984903591833796659 or 984903591816990739 or 984936907874136094)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent("You don't have a role that can be renamed!").AsEphemeral());
                return;
            }

            DiscordRole roleToModify = default;
            foreach (var role in roles)
                if (role.Id is 984903591833796659 or 984903591816990739 or 984936907874136094)
                {
                }
                else
                {
                    roleToModify = role;
                    break;
                }

            if (roleToModify == default)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .WithContent("You don't have a role that can be renamed!").AsEphemeral());
                return;
            }

            await roleToModify.ModifyAsync(role => role.Name = name);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .WithContent($"Your role has been renamed to **{name}**.").AsEphemeral());
        }
    }

    public class MusicUtilityCommands : ApplicationCommandModule
    {
        [SlashCommand("fixmusic", "Fixes the music bot by restarting Lavalink.")]
        public static async Task FixMusic(InteractionContext ctx) {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            await EvalCommands.RunCommand("ssh floatingmilkshake@meow.floatingmilkshake.com " +
                                          "\"tmux kill-session -t lavalink && " +
                                          "cd /home/floatingmilkshake/projects/esmbot/Lavalink && tmux new -s lavalink -d " +
                                          "/home/floatingmilkshake/.sdkman/candidates/java/current/bin/java " +
                                          "-Djdk.tls.client.protocols=TLSv1.2 -jar " +
                                          "/home/floatingmilkshake/projects/esmbot/Lavalink/Lavalink.jar\"");

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().WithContent("Done!"));
        }
    }

    public class ReminderCommands : ApplicationCommandModule
    {
        [SlashCommand("import_sink_reminders", "Import reminders exported from Kitchen Sink.")]
        public static async Task ImportSinkRemindersCommand(InteractionContext ctx,
            [Option("file", "The JSON file containing your reminders exported from Kitchen Sink.")] DiscordAttachment file)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var sinkReminders =
                JsonConvert.DeserializeObject<List<SinkReminderList>>(
                    await Program.HttpClient.GetStringAsync(file.Url));

            foreach (var sinkReminder in sinkReminders)
            {
                // parse sinkReminder.ReminderDue which would be a unix timestamp (like what you would put in discord as <t:timestamp:F>)

                var reminderTime = DateTimeOffset.FromUnixTimeSeconds((long)sinkReminder.ReminderDue).ToLocalTime().DateTime;
    
                Random random = new();
                var reminderId = random.Next(1000, 9999);
    
                var reminders = await Program.Db.HashGetAllAsync("reminders");
                foreach (var rem in reminders)
                    while (rem.Name == reminderId)
                        reminderId = random.Next(1000, 9999);
                
                var channel = await Program.Discord.GetChannelAsync(sinkReminder.Channel);
                var guildId = channel.Guild.Id.ToString();
                
                Reminder reminder = new()
                {
                    UserId = ctx.User.Id,
                    ChannelId = sinkReminder.Channel,
                    GuildId = guildId,
                    ReminderId = reminderId,
                    ReminderText = sinkReminder.Message,
                    ReminderTime = reminderTime,
                    SetTime = DateTime.Now,
                    IsPrivate = false
                };
    
                await Program.Db.HashSetAsync("reminders", reminderId, JsonConvert.SerializeObject(reminder));
            }

            await ctx.FollowUpAsync(
                new DiscordFollowupMessageBuilder().WithContent("Reminders successfully imported from Kitchen Sink!"));
        }

        private class SinkReminderList
        {
            [JsonProperty("id")] public ulong Id { get; set; }
            
            [JsonProperty("channel")] public ulong Channel { get; set; }
            
            [JsonProperty("message")] public string Message { get; set; }
            
            [JsonProperty("reminder_due")] public ulong ReminderDue { get; set; }
        }
    }

    public class Checks
    {
        public static async Task MessageCreateChecks(MessageCreateEventArgs e)
        {
            if (!e.Channel.IsPrivate && e.Guild.Id == 984903591816990730 && e.Message.Content.StartsWith("ch!"))
            {
                var message = new DiscordMessageBuilder()
                    .WithContent("bots have changed, try `m!` instead.").WithReply(e.Message.Id);
                await e.Channel.SendMessageAsync(message);
            }

            if (e.Channel.Id == 1012735880869466152 && e.Message.Author.Id == 1012735924284702740 &&
                e.Message.Content.Contains("has banned the IP"))
            {
                Regex ipRegex = new(@"[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}");
                var ipAddr = ipRegex.Match(e.Message.Content).ToString();

                Regex attemptCountRegex = new("after ([0-9].*) failed");
                var attemptCount = int.Parse(attemptCountRegex.Match(e.Message.Content).Groups[1].ToString());

                if (attemptCount > 3)
                {
                    var msg = await e.Channel.SendMessageAsync(
                        $"<@455432936339144705> `{ipAddr}` attempted to connect {attemptCount} times before being banned. Waiting for approval to ban permanently...");


                    await EvalCommands.RunCommand(
                        $"ssh meow@meow \"sudo ufw deny from {ipAddr} to any && sudo ufw reload\"");

                    await msg.ModifyAsync(
                        $"<@455432936339144705> `{ipAddr}` attempted to connect {attemptCount} times before being banned. It has been permanently banned automatically.");
                }
            }

            if (e.Guild == Program.HomeServer && e.Message.Author.Id == 1031968180974927903 &&
                (await e.Message.Channel.GetMessagesBeforeAsync(e.Message.Id, 1))[0].Content
                .Contains("caption"))
            {
                var chan = await Program.Discord.GetChannelAsync(1048242806486999092);
                if (string.IsNullOrWhiteSpace(e.Message.Content))
                    await chan.SendMessageAsync(e.Message.Attachments[0].Url);
                else if (e.Message.Content.Contains("http"))
                    await chan.SendMessageAsync(e.Message.Content);
            }
        }
    }

    public class MessageCommands : BaseCommandModule
    {
        // Per-server commands go here. Use the [TargetServer(serverId)] attribute to restrict a command to a specific guild.

        // Note that this command here can be removed if another command is added; there just needs to be one here to prevent an exception from being thrown when the bot is run.
        [Command("dummycommand")]
        [Hidden]
        public async Task DummyCommand(CommandContext ctx)
        {
            await ctx.RespondAsync(
                "Hi! This command does nothing other than prevent an exception from being thrown when the bot is run. :)");
        }
        
        [Command("remind")]
        [Aliases("remindme", "reminder", "remember")]
        [Description("Reminds you of something.")]
        public async Task Remind(CommandContext ctx, string time, string firstTimeOrText = null, string secondTimeOrText = null, [RemainingText] string text = null)
        {
            if (ctx.Guild.Id != 1007457740655968327 && ctx.Guild.Id != Program.HomeServer.Id) return;

            if (firstTimeOrText is null && secondTimeOrText is null && text is null)
            {
                await ctx.RespondAsync("I couldn't understand that time. Try something like \"in 5 minutes\" or \"tomorrow at 4pm\".");
                return;
            }

            DateTime? reminderTime;
            if (time != "null")
            {
                try
                {
                    reminderTime = HumanDateParser.HumanDateParser.Parse(time + firstTimeOrText + secondTimeOrText);
                }
                catch
                {
                    try
                    {
                        reminderTime = HumanDateParser.HumanDateParser.Parse(time + firstTimeOrText);
                        text = secondTimeOrText;
                    }
                    catch
                    {
                        try
                        {
                            reminderTime = HumanDateParser.HumanDateParser.Parse(time);
                            text = firstTimeOrText;
                        }
                        catch
                        {
                            // Parse error, either because the user did it wrong or because HumanDateParser is weird

                            await ctx.RespondAsync("I couldn't understand that time. Try something like \"in 5 minutes\" or \"tomorrow at 4pm\".");
                            return;
                        }
                    }
                }
                
                if (reminderTime <= DateTime.Now)
                {
                    // If user says something like "4pm" and its past 4pm, assume they mean "4pm tomorrow"
                    if (reminderTime.Value.Date == DateTime.Now.Date &&
                        reminderTime.Value.TimeOfDay < DateTime.Now.TimeOfDay)
                    {
                        reminderTime = reminderTime.Value.AddDays(1);
                    }
                    else
                    {
                        await ctx.RespondAsync("You can't set a reminder to go off in the past!");
                        return;
                    }
                }
            }
            else
            {
                reminderTime = null;
            }

            var guildId = ctx.Channel.IsPrivate ? "@me" : ctx.Guild.Id.ToString();

            Random random = new();
            var reminderId = random.Next(1000, 9999);

            var reminders = await Program.Db.HashGetAllAsync("reminders");
            foreach (var rem in reminders)
                while (rem.Name == reminderId)
                    reminderId = random.Next(1000, 9999);
            // This is to avoid the potential for duplicate reminders
            Reminder reminder = new()
            {
                UserId = ctx.User.Id,
                ChannelId = ctx.Channel.Id,
                GuildId = guildId,
                ReminderId = reminderId,
                ReminderText = text,
                ReminderTime = reminderTime,
                SetTime = DateTime.Now,
                IsPrivate = false
            };

            if (reminderTime is not null)
            {
                var unixTime = ((DateTimeOffset)reminderTime).ToUnixTimeSeconds();

                var message = await ctx.RespondAsync($"Reminder set for <t:{unixTime}:F> (<t:{unixTime}:R>)!" +
                                                     $"\nReminder ID: `{reminder.ReminderId}`");
                reminder.MessageId = message.Id;
            }
            else
            {
                var message = await ctx.RespondAsync("Reminder set!");
                reminder.MessageId = message.Id;
            }

            await Program.Db.HashSetAsync("reminders", reminderId, JsonConvert.SerializeObject(reminder));
        }
    }

    public class TargetServerAttribute : CheckBaseAttribute
    {
        public TargetServerAttribute(ulong targetGuild)
        {
            TargetGuild = targetGuild;
        }

        private ulong TargetGuild { get; }

        public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        {
            return Task.FromResult(!ctx.Channel.IsPrivate && ctx.Guild.Id == TargetGuild);
        }
    }
}
