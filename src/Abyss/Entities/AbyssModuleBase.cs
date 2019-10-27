﻿using Abyssal.Common;
using Discord;
using Qmmands;
using System;
using System.Threading.Tasks;

namespace Abyss
{
    public abstract class AbyssModuleBase : ModuleBase<AbyssRequestContext>
    {
        public Task ReplyAsync(string? content = null, EmbedBuilder? embed = null,
            RequestOptions? options = null)
        {
            return Context.ReplyAsync(content, embed, options);
        }

        public static ActionResult Image(params FileAttachment[] attachments)
        {
            return new OkResult((string?) null, attachments);
        }

        public ActionResult Image(string? title, string imageUrl)
        {
            return Ok(e =>
            {
                e.ImageUrl = imageUrl;
                e.Title = title;
            });
        }

        public static OkResult Text(string raw) => new OkResult(raw);

        public static ReplySuccessResult Ok() => new ReplySuccessResult();

        public static ReactSuccessResult OkReaction() => new ReactSuccessResult();

        public ActionResult Ok(string content, params FileAttachment[] attachments)
        {
            return (Context.Command.GetType().HasCustomAttribute<ResponseFormatOptionsAttribute>(out var at) && at!.Options.HasFlag(ResponseFormatOptions.DontEmbed))
                ? new OkResult(content, attachments)
                : Ok(new EmbedBuilder().WithDescription(content), attachments);
        }

        public static ActionResult Ok(AbyssRequestContext context, EmbedBuilder builder, params FileAttachment[] attachments)
        {
            bool attachFooter = false;
            if (builder.Footer == null)
            {
                if (context.Command.GetType().HasCustomAttribute<ResponseFormatOptionsAttribute>(out var at))
                {
                    attachFooter = !at!.Options.HasFlag(ResponseFormatOptions.DontAttachFooter);
                }
                else attachFooter = true;
            }

            bool attachTimestamp = false;
            if (builder.Timestamp == null)
            {
                if (context.Command.GetType().HasCustomAttribute<ResponseFormatOptionsAttribute>(out var at0))
                {
                    attachTimestamp = !at0!.Options.HasFlag(ResponseFormatOptions.DontAttachTimestamp);
                }
                else attachTimestamp = true;
            }

            if (attachFooter) builder.WithRequesterFooter(context);
            if (attachTimestamp) builder.WithCurrentTimestamp();
            return new OkResult(builder.WithColor(context.BotUser.GetHighestRoleColourOrDefault()), attachments);
        }

        public ActionResult Ok(EmbedBuilder builder, params FileAttachment[] attachments)
        {
            return Ok(Context, builder, attachments);
        }

        public ActionResult Ok(Action<EmbedBuilder> actor, params FileAttachment[] attachments)
        {
            var eb = new EmbedBuilder();
            actor(eb);
            return Ok(eb, attachments);
        }

        public static ActionResult BadRequest(string reason)
        {
            return new BadRequestResult(reason);
        }

        public static ActionResult Empty()
        {
            return new EmptyResult();
        }
    }
}