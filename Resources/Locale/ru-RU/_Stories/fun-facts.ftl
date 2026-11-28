-stories-ff-number = #ffd166
-stories-ff-damage = #ff6b6b
-stories-ff-heal = #6fdc8c
-stories-ff-fire = #ff9f43
-stories-ff-xeno = #c792ea
-stories-ff-build = #74b9ff
-stories-ff-misc = #a0e7e5

stories-fun-facts-header = [bold][color=#ffd166]Забавные факты:[/color][/bold]

stories-fun-facts-survived-1 = и { $gender ->
    [female] выжила
   *[other] выжил
}! Отличная работа!
stories-fun-facts-survived-2 = и { $gender ->
    [female] дожила
   *[other] дожил
} до конца раунда. Так держать!
stories-fun-facts-survived-3 = и всё ещё { $gender ->
    [female] осталась
   *[other] остался
} в строю!
stories-fun-facts-died-1 = до своей гибели. Хорошая работа!
stories-fun-facts-died-2 = прежде чем пасть. Вечная память!
stories-fun-facts-died-3 = перед смертью. Погибнуть не зря - тоже достижение!
stories-fun-facts-died-to-1 = до гибели от [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Хорошая работа!
stories-fun-facts-died-to-2 = прежде чем пасть от [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Вечная память!
stories-fun-facts-died-to-3 = но всё же { $gender ->
    [female] погибла
   *[other] погиб
} от [bold][color={ -stories-ff-damage }]{ $cause }[/color][/bold]. Месть будет сладкой!

stories-fun-fact-kills = [bold]{ $name }[/bold] { $gender ->
    [female] совершила
   *[other] совершил
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]{ $value ->
    [one] убийство
    [few] убийства
   *[other] убийств
}[/color][/bold]
stories-fun-fact-damage-taken = [bold]{ $name }[/bold] { $gender ->
    [female] получила
   *[other] получил
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]урона[/color][/bold]
stories-fun-fact-damage-dealt = [bold]{ $name }[/bold] { $gender ->
    [female] нанесла
   *[other] нанёс
} врагам [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]урона[/color][/bold]
stories-fun-fact-friendly-damage = [bold]{ $name }[/bold] { $gender ->
    [female] нанесла
   *[other] нанёс
} [color={ -stories-ff-damage }]своим же союзникам[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-damage }]урона[/color][/bold]
stories-fun-fact-crit-time = [bold]{ $name }[/bold] { $gender ->
    [female] провела
   *[other] провёл
} [color={ -stories-ff-damage }]в критическом состоянии[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] минуту
    [few] минуты
   *[other] минут
}[/bold]
stories-fun-fact-revives = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]{ $gender ->
    [female] реанимировала
   *[other] реанимировал
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] человека
    [few] человека
   *[other] человек
}[/bold]
stories-fun-fact-times-revived = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]{ $gender ->
    [female] возвращалась
   *[other] возвращался
} с того света[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-evolutions = [bold]{ $name }[/bold] [color={ -stories-ff-xeno }]{ $gender ->
    [female] эволюционировала
   *[other] эволюционировал
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-shots = [bold]{ $name }[/bold] { $gender ->
    [female] выпустила
   *[other] выпустил
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] патрон
    [few] патрона
   *[other] патронов
}[/color][/bold] с меткостью [bold][color={ -stories-ff-number }]{ $accuracy }%[/color][/bold]
stories-fun-fact-healed = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]{ $gender ->
    [female] вылечила
   *[other] вылечил
}[/color] другим [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-heal }]урона[/color][/bold]
stories-fun-fact-surgeries = [bold]{ $name }[/bold] { $gender ->
    [female] провела
   *[other] провёл
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-heal }]{ $value ->
    [one] операцию
    [few] операции
   *[other] операций
}[/color][/bold]
stories-fun-fact-built = [bold]{ $name }[/bold] [color={ -stories-ff-build }]{ $gender ->
    [female] построила
   *[other] построил
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-build }]{ $value ->
    [one] укрепление
    [few] укрепления
   *[other] укреплений
}[/color][/bold]
stories-fun-fact-dogtags = [bold]{ $name }[/bold] { $gender ->
    [female] собрала
   *[other] собрал
} с павших [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] жетон
    [few] жетона
   *[other] жетонов
}[/color][/bold]
stories-fun-fact-grenades = [bold]{ $name }[/bold] { $gender ->
    [female] привела
   *[other] привёл
} в действие [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-fire }]{ $value ->
    [one] гранату
    [few] гранаты
   *[other] гранат
}[/color][/bold]
stories-fun-fact-ignited = [bold]{ $name }[/bold] [color={ -stories-ff-fire }]{ $gender ->
    [female] загоралась
   *[other] загорался
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-eaten = [bold]{ $name }[/bold] { $gender ->
    [female] съела
   *[other] съел
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-misc }]{ $value ->
    [one] блюдо
    [few] блюда
   *[other] блюд
}[/color][/bold]
stories-fun-fact-nested = [bold]{ $name }[/bold] { $gender ->
    [female] закрепила
   *[other] закрепил
} [color={ -stories-ff-xeno }]в гнёздах[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] носителя
    [few] носителя
   *[other] носителей
}[/bold]
stories-fun-fact-infected = [bold]{ $name }[/bold] [color={ -stories-ff-xeno }]{ $gender ->
    [female] заразила
   *[other] заразил
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] носителя
    [few] носителя
   *[other] носителей
}[/bold]
stories-fun-fact-resin = [bold]{ $name }[/bold] { $gender ->
    [female] выделила
   *[other] выделил
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]{ $value ->
    [one] постройку
    [few] постройки
   *[other] построек
} из смолы[/color][/bold]
stories-fun-fact-weeds = [bold]{ $name }[/bold] { $gender ->
    [female] посадила
   *[other] посадил
} [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]{ $value ->
    [one] узел
    [few] узла
   *[other] узлов
} травы[/color][/bold]
stories-fun-fact-acid = [bold]{ $name }[/bold] { $gender ->
    [female] применила
   *[other] применил
} [color={ -stories-ff-xeno }]кислоту[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-xeno-heals = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]{ $gender ->
    [female] лечила
   *[other] лечил
} сестёр[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-knockdowns = [bold]{ $name }[/bold] [color={ -stories-ff-damage }]{ $gender ->
    [female] сбивала
   *[other] сбивал
} врагов с ног[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold]
stories-fun-fact-rescues = [bold]{ $name }[/bold] [color={ -stories-ff-heal }]{ $gender ->
    [female] освободила
   *[other] освободил
} из гнёзд[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] человека
    [few] человека
   *[other] человек
}[/bold]
stories-fun-fact-deaths = [bold]{ $name }[/bold] [color={ -stories-ff-damage }]{ $gender ->
    [female] погибала
   *[other] погибал
}[/color] [bold][color={ -stories-ff-number }]{ $value }[/color] { $value ->
    [one] раз
    [few] раза
   *[other] раз
}[/bold] за раунд. Повторение - мать учения!
stories-fun-fact-larvae = На счету [bold]{ $name }[/bold] [bold][color={ -stories-ff-number }]{ $value }[/color] [color={ -stories-ff-xeno }]{ $value ->
    [one] личинка
    [few] личинки
   *[other] личинок
}[/color][/bold], вылупившихся из заражённых носителей!

cmd-funfacts-desc = Показывает текущую статистику забавных фактов по всем отслеживаемым телам и игрокам.
cmd-funfacts-help = Использование: funfacts
cmd-funfacts-empty = Статистика забавных фактов пока не собрана.
