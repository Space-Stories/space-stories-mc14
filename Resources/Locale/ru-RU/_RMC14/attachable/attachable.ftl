rmc-attachable-holder-strip-ui-title = обвесы
rmc-attachable-holder-strip-ui-empty-slot = ничего

rmc-verb-strip-attachables = Просмотреть обвесы

rmc-aslot-barrel = Дуло
rmc-bslot-barrel = Дуло Матебы
rmc-aslot-rail = Рельса
rmc-aslot-stock = Приклад
rmc-aslot-underbarrel = Подствольное
rmc-aslot-harness = Упряжь

rmc-attachable-activation-fail-not-wielded = { CAPITALIZE($holder) } должен быть взят в руки, чтобы активировать { $attachable }!
rmc-attachable-activation-fail-not-held = { CAPITALIZE($holder) } необходимо удерживать, чтобы активировать { $attachable }!
rmc-attachable-activation-fail-not-owned = { CAPITALIZE($holder) } должен находиться у вас в руках или быть экипирован, чтобы активировать { $attachable }!
rmc-attachable-activation-fail-on-vehicle = Вы не можете развернуть { $attachable }, находясь на транспорте!

rmc-attachable-shoot-fail-not-wielded = { CAPITALIZE($holder) } должен быть взят в руки, чтобы выстрелить { $attachable }!

rmc-attachable-verb-toggle = Переключить { $attachable }

attachable-popup-activate-generic = Вы активируете { $attachable }.
attachable-popup-deactivate-generic = Вы деактивируете { $attachable }.

attachable-popup-activate-deploy-on-generic = Вы размещаете { $attachable } на { $surface }.
attachable-popup-activate-deploy-on-ground = Вы размещаете { $attachable } на земле.
attachable-popup-deactivate-retract = Вы убираете { $attachable }.

attachable-popup-activate-unfold = Вы разворачиваете { $attachable }.
attachable-popup-deactivate-collapse = Вы сворачиваете { $attachable }.

attachable-popup-activate-lock = Вы запираете { $attachable }.
attachable-popup-deactivate-unlock = Вы разблокируете { $attachable }.

attachable-popup-switch-to-generic = Вы переключаетесь на использование { $attachable }.
attachable-popup-switch-from-generic = Вы перестаете использовать { $attachable }.

rmc-attachable-examinable-verb-text = Модификаторы прикрепляемого предмета
rmc-attachable-examinable-verb-message = Изучите модификаторы, применяемые этим прикрепляемым объектом.

rmc-attachable-examine-condition-always = [bold]Всегда:[/bold]
rmc-attachable-examine-condition-when = Когда
rmc-attachable-examine-condition-wielded = держатель [bold]держит оружие двумя руками[/bold]
rmc-attachable-examine-condition-unwielded = Держатель [bold]не владеет[/bold].
rmc-attachable-examine-condition-active = { $attachable } является [bold]активным[/bold].
rmc-attachable-examine-condition-inactive = { $attachable } является [bold]неактивным[/bold].

rmc-attachable-examine-condition-whitelist-comps = держатель [bold]имеет { $compNumber }[/bold] следующих компонентов: [bold]{ $comps }[/bold]
rmc-attachable-examine-condition-whitelist-sizes = держатель [bold]имеет[/bold] один из следующих размеров: [bold]{ $sizes }[/bold]
rmc-attachable-examine-condition-whitelist-tags = держатель [bold]имеет { $tagNumber }[/bold] следующих тегов: [bold]{ $tags }[/bold]

rmc-attachable-examine-condition-blacklist-comps = держатель [bold]не хватает { $compNumber }[/bold] следующих компонентов: [bold]{ $comps }[/bold]
rmc-attachable-examine-condition-blacklist-sizes = держатель [bold]не имеет[/bold] одного из следующих размеров: [bold]{ $sizes }[/bold]
rmc-attachable-examine-condition-blacklist-tags = держатель [bold]отсутствия { $tagNumber }[/bold] следующих тегов: [bold]{ $tags }[/bold]

rmc-attachable-examine-ranged-accuracy = [color={ $colour }]{ $sign }{ $accuracy }[/color] множитель точности.
rmc-attachable-examine-ranged-scatter = [color={ $colour }]{ $sign }{ $scatter }[/color] степени разброса.
rmc-attachable-examine-ranged-burst-scatter = [color={ $colour }]{ $sign }{ $burstScatterMult }[/color] множитель разброса разрывов.
rmc-attachable-examine-ranged-shots-per-burst = [color={ $colour }]{ $sign }{ $shots }[/color] выстрелы из разрыва.
rmc-attachable-examine-ranged-fire-delay = [color={ $colour }]{ $sign }{ TOSTRING($fireDelay, "F2") }[/color] секунды задержки выстрела.
rmc-attachable-examine-ranged-recoil = [color={ $colour }]{ $sign }{ $recoil }[/color] отдача.
rmc-attachable-examine-ranged-damage = [color={ $colour }]{ $sign }{ $damage }[/color] множитель урона от дальнего боя.
rmc-attachable-examine-ranged-projectile-speed = [color={ $colour }]{ $sign }{ $projectileSpeed }[/color] скорость снаряда.
rmc-attachable-examine-ranged-damage-falloff = [color={ $colour }]{ $sign }{ $falloff }[/color] множитель падения.
rmc-attachable-examine-ranged-range = [color={ $colour }]{ $sign }{ $falloff }[/color] дальность полета снаряда.
rmc-attachable-examine-ranged-projectile-stun-duration = [color={ $colour }]{ $sign }{ $stunDurationMult }[/color] множитель длительности оглушения.

rmc-attachable-examine-melee-damage = [color={ $colour }]{ $sign }{ $damage }[/color] урон в ближнем бою.

rmc-attachable-examine-size = [color={ $colour }]{ $sign }{ $size }[/color] размер элемента.

rmc-attachable-examine-speed-walk = [color={ $colour }]{ $sign }{ TOSTRING($speed, "F2") }[/color] множитель скорости ходьбы.
rmc-attachable-examine-speed-sprint = [color={ $colour }]{ $sign }{ TOSTRING($speed, "F2") }[/color] множитель скорости бега.

rmc-attachable-examine-wield-delay = [color={ $colour }]{ $sign }{ $delay }[/color] секунд задержки взмаха.

rmc-attachable-examine-verb-text = Просмотреть совместимые обвесы
rmc-attachable-examine-window-title = Совместимые обвесы
rmc-attachable-examine-no-slots = У этого оружия нет слотов для обвесов.
rmc-attachable-examine-header = [bold]Слоты для обвесов:[/bold]
rmc-attachable-examine-slot-filled = • [bold]{ $slot }:[/bold] { $attachment }
rmc-attachable-examine-slot-empty = • [bold]{ $slot }:[/bold] [color=gray]пусто[/color]
