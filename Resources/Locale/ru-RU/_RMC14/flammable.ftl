rmc-immune-to-ignition-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } { $direct ->
    [true] { "нельзя поджечь" }
    *[false] { "нельзя поджечь косвенно" }
}![/color]
rmc-immune-to-fire-tile-damage-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } не получает урона от огня на клетках![/color]
rmc-fire-armor-debuff-modifier-examine = [color=cyan]{ CAPITALIZE(SUBJECT($ent)) } теряет на { $percentage }% меньше брони, когда стоит в зелёном огне![/color]

rmc-flammable-molotov-examine = [color=cyan]You can turn this into a molotov with a piece of paper![/color]
rmc-flammable-pat-fire-user = You try to put out the fire on {$target}!
rmc-flammable-pat-fire-target = {$user} tries to put out the fire on you!
rmc-flammable-pat-fire-others = {$user} tries to put out the fire on {$target}!
rmc-flammable-empty = The {$item} is empty...
rmc-flammable-not-enough-liquid = There's not enough flammable liquid in the {$container}!
