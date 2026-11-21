cm-gun-unskilled = Похоже, вы не умеете пользоваться { $gun }
cm-gun-no-ammo-message = У вас не осталось патронов!
cm-gun-use-delay = Вам нужно подождать { $seconds } секунд, прежде чем стрелять снова!
cm-gun-pump-examine = [bold]Нажмите вашу [color=cyan]уникальную клавишу действия[/color] (по умолчанию пробел), чтобы накачать перед стрельбой![/bold]
cm-gun-pump-first-with = Сначала нужно накачать оружие с помощью { $key }!
cm-gun-pump-first = Сначала нужно накачать оружие!

rmc-breech-loaded-open-shoot-attempt = Сначала нужно закрыть затвор!
rmc-breech-loaded-not-ready-to-shoot = Сначала нужно открыть и закрыть затвор!
rmc-breech-loaded-closed-load-attempt = Сначала нужно открыть затвор!
rmc-breech-loaded-closed-extract-attempt = Сначала нужно открыть затвор!
rmc-breech-loaded-toggle-attempt-cooldown = Вы должны подождать перед тем, как снова { $action } камеру!
rmc-breech-loaded-open = открыть
rmc-breech-loaded-close = закрыть

rmc-wield-use-delay = Вам нужно подождать { $seconds } секунд, прежде чем использовать { $wieldable }!
rmc-shoot-use-delay = Вам нужно подождать { $seconds } секунд, прежде чем снять { $wieldable }!

rmc-shoot-harness-required = Требуется разгрузка
rmc-wear-smart-gun-required = Чтобы надеть это, вы должны быть оснащены умным оружием.
rmc-gun-arc-blocked = Стрельба за пределами зоны обстрела оружия запрещена.

rmc-shoot-id-lock-unauthorized = Спуск заблокирован. Неавторизованный пользователь.
rmc-id-lock-unauthorized = Действие отклонено. Неавторизованный пользователь.
rmc-id-lock-authorization = Вы поднимаете { $gun }, регистрируя себя как его владельца.
rmc-id-lock-authorization-combat = Оружие { $gun } издаёт звуковой сигнал, регистрируя себя как его владельца.
rmc-id-lock-toggle-lock = Вы { $action } блокируете идентификатор на { $gun }.

rmc-id-lock-color-unauthorized = красный
rmc-id-lock-color-authorized = шартрез
rmc-id-lock-toggle-on = закрыт
rmc-id-lock-toggle-off = открыт

rmc-iff-toggle = Вы { $action } IFF на { $gun }.
rmc-iff-toggle-off = отключаете
rmc-iff-toggle-on = включаете

rmc-revolver-spin = Вы вращаете цилиндр.

rmc-examine-text-weapon-accuracy = Текущий множитель точности — [color={ $colour }]{ TOSTRING($accuracy, "F2") }[/color].

rmc-examine-text-scatter-max = Текущий максимальный разброс составляет [color={ $colour }]{ TOSTRING($scatter, "F1") }[/color] градусов.
rmc-examine-text-scatter-min = Текущий минимальный разброс составляет [color={ $colour }]{ TOSTRING($scatter, "F1") }[/color] градусов.
rmc-examine-text-shots-to-max-scatter = Для достижения максимального разброса требуется [color={ $colour }]{ $shots }[/color] выстрелов.
rmc-examine-text-iff = [color=cyan]Это оружие будет игнорировать союзников и стрелять сквозь них![/color]
rmc-examine-text-id-lock-no-user = [color=chartreuse]Оно ни на кого не зарегистрировано. Поднимите его, чтобы зарегистрировать себя как его владельца.[/color]
rmc-examine-text-id-lock = [color=chartreuse]Оно зарегистрировано на [/color][color={ $color }]{ $name }[/color][color=chartreuse].[/color]
rmc-examine-text-id-lock-unlocked = [color=chartreuse]Оно зарегистрировано на [/color][color={ $color }]{ $name }[/color][color=chartreuse], но ограничения на огонь сняты.[/color]
rmc-examine-text-execute = [color=red]Из этого оружия можно казнить людей при наличии нужного навыка![/color]

rmc-gun-rack-examine = [bold]Нажмите клавишу [color=cyan]уникального действия[/color] (по умолчанию пробел), чтобы зафиксировать стойку перед выстрелом.[/bold]
rmc-gun-rack-first-with = Сначала нужно вставить пистолет в { $key }!
rmc-gun-rack-first = Сначала нужно поставить пистолет на предохранитель!

rmc-assisted-reload-fail-angle = Вы должны стоять позади { $target }, чтобы перезарядить { POSS-ADJ($target) } оружие!
rmc-assisted-reload-fail-full = { CAPITALIZE(POSS-ADJ($target)) } { $weapon } уже заряжено.
rmc-assisted-reload-fail-mismatch = Патроны { $ammo } не могут быть загружены в оружие { $weapon }!
rmc-assisted-reload-start-user = Вы начинаете перезаряжать { $target } { $weapon }! Не двигайтесь...
rmc-assisted-reload-start-target = { $reloader } начинает перезаряжать ваше { $weapon } патронами { $ammo }! Не двигайтесь...

rmc-gun-stacks-hit-single = В яблочко!
rmc-gun-stacks-hit-multiple = В яблочко! { $hits } попаданий подряд!
rmc-gun-stacks-reset = Оружие { $weapon } издаёт звуковой сигнал, теряя данные о наведении на цель, и возвращается к обычному режиму стрельбы.

rmc-gun-shoot-air-self = ВЫ СТРЕЛЯЕТЕ ИЗ СВОЕГО { CAPITALIZE($weapon) } В ВОЗДУХ!
rmc-gun-shoot-air-other = { CAPITALIZE(THE($user)) } СТРЕЛЯЕТ ИЗ { CAPITALIZE(THE($weapon)) } В ВОЗДУХ!
rmc-gun-shoot-air-blocked = Крыша над вами слишком плотная.
rmc-gun-shoot-air-examine = [bold]Нажмите клавишу [color=cyan]уникального действия[/color] (Пробел по умолчанию){ $harm ->
    [true] { " в режиме атаки" }
    *[false] { "" }
} чтобы выстрелить в воздух.[/bold]

rmc-flare-gun-examine = Последняя выпущенная сигнальная ракета имеет обозначение: [color=#ad3b98][bold]{ $id }[/bold][/color]

expendable-light-starshell-ash-empty-name = Пепел погасшей звёздной шашки
expendable-light-starshell-ash-empty-desc = Сгоревшие остатки звёздной шашки
rmc-heavy-revolver-buildup = Нам с трудом удаётся устоять на ногах!
rmc-heavy-revolver-knockdown = Мощный удар выбивает нас из равновесия!
