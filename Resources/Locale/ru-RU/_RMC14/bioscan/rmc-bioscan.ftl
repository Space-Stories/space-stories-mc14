rmc-bioscan-ares-announcement = [color=white][font size=16][bold]Статус биосканера ARES v3.2[/bold][/font][/color][color=red][font size=14][bold] { $message }[/bold][/font][/color]

rmc-bioscan-ares = Биосканирование завершено.

    Датчики показывают { $shipUncontained ->
    [0] отсутствие
    *[other] { $shipUncontained }
} неизвестых форм жизни { $shipUncontained ->
    [0] { "" }
    [1] сигнатура
    *[other] сигнатур
} на корабле{ $shipLocation ->
    [none] { "" }
    *[other] , в том числе в{ $shipLocation },
} и { $onPlanet ->
    [0] ноль
    *[other] около { $onPlanet }
} { $onPlanet ->
    [0] сигнатур
    [1] сигнатуру
    *[other] сигнатур
} расположенных в другом месте{ $planetLocation ->
    [none] .
    *[other] , в том числе в { $planetLocation }
}

rmc-bioscan-xeno-announcement = [color=#318850][font size=14][bold]Королева-мать проникает в ваше сознание из разных миров. { $message }[/bold][/font][/color]

rmc-bioscan-xeno = Моим детям и их королеве: Я чувствую { $onShip ->
    [0] отсутствие носителей
    [1] примерно одного носителя
    *[other] примерно { $onShip } носителей
} в металлическом улье{ $shipLocation ->
    [none] { "" }
    *[other] , в том числе в { $shipLocation },
} и { $onPlanet ->
    [0] никого
    [1] одного
    *[other] { $onPlanet }
} разбросанных в других местах{ $planetLocation ->
    [none] .
    *[other] , в том числе в { $planetLocation }
}
