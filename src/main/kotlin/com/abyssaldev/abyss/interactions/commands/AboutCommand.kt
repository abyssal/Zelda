package com.abyssaldev.abyss.interactions.commands

import com.abyssaldev.abyss.AbyssEngine
import com.abyssaldev.abyss.interactions.framework.InteractionCommand
import com.abyssaldev.abyss.interactions.framework.InteractionRequest
import net.dv8tion.jda.api.MessageBuilder
import org.apache.commons.lang3.time.DurationFormatUtils
import java.lang.management.ManagementFactory

class AboutCommand : InteractionCommand() {
    override val name = "about"
    override val description: String = "Shows some information about me."

    override fun invoke(call: InteractionRequest): MessageBuilder = respond {
        embed {
            setTitle("About")
            appendDescriptionLine("I'm Abyss, and I'm running on a new Discord platform: 'slash commands'.")
            appendDescriptionLine("What this means is that you can see my commands on your screen by typing `/`.")
            appendDescriptionLine("This is very new technology, so there might be issues!")
            appendDescriptionLine("Feel free to report any issues you encounter.")
            addField("Uptime", DurationFormatUtils.formatDuration(ManagementFactory.getRuntimeMXBean().uptime, "H:mm:ss"), true)
            addField("Version", "16.1.0 (Production)", true)
            addField("Runtime", "JRE ${System.getProperty("java.version")}/Kotlin ${KotlinVersion.CURRENT}", true)
            addField("Add me", "https://abyss.abyssaldev.com/invite", true)
            addField("Source & Issue Tracker", "https://abyss.abyssaldev.com/", true)
            setFooter("© 2021 - An Abyssal production", AbyssEngine.instance.applicationInfo?.owner?.effectiveAvatarUrl)
        }
    }
}