-stories-ff-number = #ffd166
-stories-ff-damage = #ff6b6b
-stories-ff-heal = #6fdc8c
-stories-ff-fire = #ff9f43
-stories-ff-xeno = #c792ea
-stories-ff-build = #74b9ff
-stories-ff-misc = #a0e7e5

stories-fun-facts-header = [bold][color=#ffd166]Fun facts:[/color][/bold]

stories-fun-facts-survived-1 = and survived! Great work!
stories-fun-facts-survived-2 = and made it to the end of the round. Keep it up!
stories-fun-facts-survived-3 = and is still standing!
stories-fun-facts-died-1 = before dying. Good work!
stories-fun-facts-died-2 = before falling. Rest in peace!
stories-fun-facts-died-3 = before their death. Not a death in vain!
stories-fun-facts-died-to-1 = before dying to [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Good work!
stories-fun-facts-died-to-2 = before falling to [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Rest in peace!
stories-fun-facts-died-to-3 = but was still taken down by [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Revenge will be sweet!

stories-fun-fact-kills = [bold]{ $name }[/bold] earned [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]{ $value ->
    [one] kill
   *[other] kills
}[/color][/bold]
stories-fun-fact-damage-taken = [bold]{ $name }[/bold] took [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]damage[/color][/bold]
stories-fun-fact-damage-dealt = [bold]{ $name }[/bold] dealt [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]damage[/color][/bold] to enemies
stories-fun-fact-friendly-damage = [bold]{ $name }[/bold] dealt [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]damage[/color][/bold] to [color={ -stories-ff-damage }]their own allies[/color]
stories-fun-fact-crit-time = [bold]{ $name }[/bold] spent [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] minute
   *[other] minutes
}[/bold] [color={ -stories-ff-damage }]in critical condition[/color]
stories-fun-fact-revives = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]revived[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] person
   *[other] people
}[/bold]
stories-fun-fact-times-revived = [bold]{ $name }[/bold] was [color={ -stories-ff-heal }]brought back from the dead[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-evolutions = [bold]{ $name }[/bold] [color={ -stories-ff-xeno }]evolved[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-shots = [bold]{ $name }[/bold] fired [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] round
   *[other] rounds
}[/color][/bold] with [bold][color={ -stories-ff-number }]{ $accuracy }%[/color][/bold] accuracy
stories-fun-fact-healed = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]healed[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-heal }]damage[/color][/bold] on others
stories-fun-fact-surgeries = [bold]{ $name }[/bold] performed [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-heal }]{ $value ->
    [one] surgery
   *[other] surgeries
}[/color][/bold]
stories-fun-fact-built = [bold]{ $name }[/bold] [color={ -stories-ff-build }]built[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-build }]{ $value ->
    [one] structure
   *[other] structures
}[/color][/bold]
stories-fun-fact-dogtags = [bold]{ $name }[/bold] collected [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] dogtag
   *[other] dogtags
}[/color][/bold] from the fallen
stories-fun-fact-grenades = [bold]{ $name }[/bold] primed [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-fire }]{ $value ->
    [one] explosive
   *[other] explosives
}[/color][/bold]
stories-fun-fact-ignited = [bold]{ $name }[/bold] [color={ -stories-ff-fire }]caught fire[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-eaten = [bold]{ $name }[/bold] ate [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] meal
   *[other] meals
}[/color][/bold]
stories-fun-fact-nested = [bold]{ $name }[/bold] secured [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] host
   *[other] hosts
}[/bold] [color={ -stories-ff-xeno }]to nests[/color]
stories-fun-fact-infected = [bold]{ $name }[/bold] [color={ -stories-ff-xeno }]infected[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] host
   *[other] hosts
}[/bold]
stories-fun-fact-resin = [bold]{ $name }[/bold] secreted [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]resin { $value ->
    [one] structure
   *[other] structures
}[/color][/bold]
stories-fun-fact-weeds = [bold]{ $name }[/bold] planted [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]weed { $value ->
    [one] node
   *[other] nodes
}[/color][/bold]
stories-fun-fact-acid = [bold]{ $name }[/bold] applied [color={ -stories-ff-xeno }]acid[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-xeno-heals = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]healed sisters[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-knockdowns = [bold]{ $name }[/bold] [color={ -stories-ff-damage }]knocked down enemies[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] time
   *[other] times
}[/bold]
stories-fun-fact-rescues = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]freed[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] person
   *[other] people
}[/bold] [color={ -stories-ff-heal }]from nests[/color]
stories-fun-fact-deaths = [bold]{ $name }[/bold] [color={ -stories-ff-damage }]died[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] times[/bold] this round. Practice makes perfect!
stories-fun-fact-larvae = [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]larvae[/color][/bold] burst from hosts infected by [bold]{ $name }[/bold]!

cmd-funfacts-desc = Shows the current fun facts statistics of every tracked body and player.
cmd-funfacts-help = Usage: funfacts
cmd-funfacts-empty = No fun facts statistics have been collected yet.
