rmc-dropship-pre-flight-fueling = Шаттл все еще заправляется перед полетом и пока не может отправиться. Пожалуйста, подождите еще { $minutes } минут, прежде чем повторить попытку.
rmc-dropship-pre-hijack = Этот терминал не будет работать еще { $minutes } минут.
rmc-dropship-invalid-hijack = На терминале мигают огни, но вы не можете понять их значение.
rmc-dropship-terminal-clueless = Вы растерянно смотрите на { $terminal }.
rmc-dropship-no-destinations-nearby = Рядом с вами нет точек назначения для десантного корабля!
rmc-dropship-already-incoming = Сюда уже летит десантный корабль!
rmc-dropship-already-here = Десантный корабль уже здесь!
rmc-dropship-not-primary-shuttle = Шаттл не отвечает на команды. Похоже, это не основной шаттл.
rmc-dropship-call-to-location = Вы вызываете один из десантных кораблей к своему местоположению.
rmc-dropship-none-available = Нет доступных десантных кораблей! Подождите немного.
rmc-dropship-no-remote-control = У этого десантного корабля не включено дистанционное управление.
rmc-dropship-busy = Этот десантный корабль сейчас занят. Повторите попытку позже.
rmc-dropship-now-on-its-way = Этот десантный корабль уже в пути.

rmc-dropship-weapons-title = Оружейная консоль

rmc-dropship-weapons-main-screen-text = U.S.M.C.
  Система управления оружием корабля
  V 0.1

rmc-dropship-weapons-weapon-selected = { $weapon }
  Нет боеприпасов
rmc-dropship-weapons-weapon-selected-ammo = { $weapon }
  { $ammo }
  Боекомплект { $rounds } / { $maxRounds }

rmc-dropship-weapons-target-strike = Приобретение цели

  Ударный режим: { $mode }
  Режим цели: { $targetMode }

  Конфигурация удара:
  { $weapon }

  Цель выбрана: { $target }

  Смещение { $xOffset },{ $yOffset }

#  Attack Vector {$vector}

#  Offset 0,0

#  Guidance computer INCOMPLETE

rmc-dropship-weapons-equip-weapon-ammo = { $weapon }
  { $rounds } снарядов

rmc-dropship-weapons-equip = СНАР
rmc-dropship-weapons-fire-mission = ОЗ
rmc-dropship-weapons-target = ЦЕЛЬ
rmc-dropship-weapons-maps = КАРТА
rmc-dropship-weapons-cams = КАМЕРЫ
rmc-dropship-weapons-cancel = ОТМЕНА
rmc-dropship-weapons-exit = ВЫХОД
rmc-dropship-weapons-lock = ЗАХВАТ
rmc-dropship-weapons-clear = СБРОС
rmc-dropship-weapons-enable = ВКЛЮЧИТЬ
rmc-dropship-weapons-disable = ОТКЛЮЧИТЬ
rmc-dropship-weapons-deploy = РАЗВЕРТ
rmc-dropship-weapons-retract = СОБРАТЬ
rmc-dropship-weapons-auto-deploy = АВТО РАЗВЕРТ.

rmc-dropship-weapons-offset-calibration = Калибровка
  смещения
rmc-dropship-weapons-offset-calibration-does-not-affect-direct-bombardment = Не влияет на прямую бомбардировку!

rmc-dropship-weapons-fire = ОГОНЬ
rmc-dropship-weapons-strike = УДАР
rmc-dropship-weapons-vector = ВЕКТОР
rmc-dropship-weapons-quick = БЫСТР
# TODO RMC14 fire missions: restore these labels with fire mission vector controls.
# rmc-dropship-weapons-north = NORTH
# rmc-dropship-weapons-south = SOUTH
# rmc-dropship-weapons-east = EAST
# rmc-dropship-weapons-west = WEST
rmc-dropship-weapons-target-mode-quick = БЫСТР
rmc-dropship-weapons-target-mode-standard = СТАНД

rmc-dropship-weapons-night-vision-on = НВ-ВКЛ
rmc-dropship-weapons-night-vision-off = НВ-ВЫКЛ

rmc-dropship-weapons-weapon = ОРУЖ

rmc-dropship-weapons-previous = ▲
rmc-dropship-weapons-next = ▼

rmc-dropship-weapons-fire-no-weapon = Оружие не выбрано.
rmc-dropship-weapons-fire-not-flying = Шаттлы могут вести огонь только в полете.
rmc-dropship-weapons-fire-not-skilled = У вас нет подготовки, чтобы стрелять из этого оружия!
rmc-dropship-weapons-fire-no-ammo = У { $weapon } нет патронов.
rmc-dropship-weapons-fire-cooldown = { $weapon } только что выстрелило, подождите, пока оно остынет.

rmc-dropship-attached = В него загружено { $attachment }.

rmc-dropship-weapons-point-ammo = У него заряжено { $ammo }.
rmc-dropship-weapons-rounds-left = У него осталось { $current } из { $max } патронов.

rmc-dropship-utility-activate-not-flying = Утилитарные системы могут активироваться только в полете.
rmc-dropship-utility-not-flyby = { $utility } можно использовать только во время полета.
rmc-dropship-utility-not-skilled = У вас нет подготовки, чтобы использовать эту систему!
rmc-dropship-utility-cooldown = { $utility } была только что использована, вам нужно немного подождать, прежде чем использовать ее снова.

rmc-dropship-flyby-no-skill = У вас нет навыков для выполнения пролета.

rmc-dropship-fabricator-title = Изготовитель деталей
rmc-dropship-fabricator-points = Баллы: { $points }
rmc-dropship-fabricator-equipment = [bold]Снаряжение[/bold]
rmc-dropship-fabricator-ammo = [bold]Патроны[/bold]
rmc-dropship-fabricator-fabricate = Изготовить ({ $cost })
rmc-dropship-fabricator-busy = Изготовитель деталей для дропшиппинга занят. Пожалуйста, подождите завершения предыдущей операции.
rmc-dropship-fabricator-current = [bold]Сейчас:[/bold] { $item }
rmc-dropship-fabricator-idle = [bold]Сейчас:[/bold] ожидание
rmc-dropship-fabricator-queue = [bold]Очередь:[/bold] { $count }/{ $max }
rmc-dropship-fabricator-queue-empty = Нет ожидающих заказов.
rmc-dropship-fabricator-queue-entry = { $position }. { $item } ({ $cost })
rmc-dropship-fabricator-cancel = Отмена
rmc-dropship-fabricator-queue-full = Очередь изготовителя деталей десантного корабля заполнена.
rmc-dropship-fabricator-insufficient-points = У вас недостаточно очков для изготовления.

rmc-dropship-firemission-warning = Десантный корабль ведет огонь в направлении { $direction }.
rmc-dropship-firemission-warning-above = Десантный корабль ведет огонь прямо над вами!

rmc-dropship-paradrop-target-screen-text = Система развертывания десанта HPU-1
    { $hasTarget }

rmc-dropship-paradrop-target-screen-target-none = Цель не захвачена.
    Высадка невозможна.

rmc-dropship-paradrop-target-screen-target-targeting = Захвачена цель { $dropTarget }.
    Высадка возможна.

rmc-dropship-paradrop-lock-no-target = Цель не выбрана.
rmc-dropship-paradrop-lock-target-not-flying = Модуль десантирования можно активировать только в полете.

rmc-dropship-paradrop-failed = Ваша подвесная система застряла и не позволяет вам совершить прыжок.

rmc-dropship-medevac-system-screen-text = Система медицинской эвакуации РМУ-4М

rmc-dropship-fulton-system-screen-text = Система подъёма РМУ-19 Фултон

rmc-dropship-locked = Эта птица теперь наша на { $minutes } минут.
rmc-dropship-locked-out = Шаттл не отвечает, попробуйте еще раз через { $minutes } минут.
rmc-dropship-locked-out-bypass = Вы частично обошли блокировку, попробуйте еще раз!
rmc-dropship-locked-out-bypass-complete = Вы успешно сняли блокировку!
rmc-dropship-terminal-xeno-clueless = Вы растерянно смотрите на { $terminal }.
rmc-dropship-terminal-no-nearby-destinations = Рядом с вами нет точек назначения для десантного корабля!
rmc-dropship-terminal-dropship-inbound = Сюда уже летит десантный корабль!
rmc-dropship-terminal-dropship-present = Десантный корабль уже здесь!
rmc-dropship-terminal-not-primary = Шаттл не отвечает на команды; похоже, это не основной шаттл.
rmc-dropship-terminal-call-down-success = Вы вызываете один из десантных кораблей к своему местоположению.
rmc-dropship-terminal-no-available-dropships = Нет доступных десантных кораблей! Подождите немного.
rmc-dropship-terminal-unknown-location = ???
rmc-dropship-terminal-remote-control-disabled = У этого десантного корабля не включено дистанционное управление.
rmc-dropship-terminal-dropship-busy = Этот десантный корабль сейчас занят. Повторите попытку позже.
rmc-dropship-terminal-dropship-routed = Этот десантный корабль уже в пути.
rmc-dropship-destination-unavailable = Эта точка назначения больше недоступна.
rmc-dropship-planetside-only = Этот шаттл может следовать только к наземным посадочным зонам.
rmc-dropship-destination-reserved = Эта точка назначения зарезервирована для шаттлов с ограниченным доступом.
rmc-dropship-destination-not-configured = Эта точка назначения не настроена для шаттлов с ограниченным доступом.
rmc-dropship-destination-invalid-class = Эта точка назначения не может принять шаттл данного класса.
rmc-dropship-destination-invalid-tags = Эта точка назначения не соответствует текущим правилам маршрутизации шаттла.
rmc-dropship-destination-blocked-tags = Эта точка назначения запрещена текущими правилами маршрутизации шаттла.
rmc-dropship-destination-occupied = Эта точка назначения уже занята.
rmc-dropship-invalid-docking-footprint = У шаттла отсутствует допустимый стыковочный контур.
rmc-dropship-no-compatible-docking-port = Для этого шаттла нет совместимого стыковочного порта.
rmc-dropship-clearance-port = Шаттл выходит за левую границу посадочной зоны (требуется {$shuttleSize}, доступно {$destinationSize}).
rmc-dropship-clearance-starboard = Шаттл выходит за правую границу посадочной зоны (требуется {$shuttleSize}, доступно {$destinationSize}).
rmc-dropship-clearance-aft = Шаттл выходит за кормовую границу посадочной зоны (требуется {$shuttleSize}, доступно {$destinationSize}).
rmc-dropship-clearance-forward = Шаттл выходит за носовую границу посадочной зоны (требуется {$shuttleSize}, доступно {$destinationSize}).

rmc-dropship-equipment-deployer-text = { $deployName }
rmc-dropship-equipment-deployer-health = Статус: { $status }
rmc-dropship-equipment-deployer-ammo = Боезапас: { $ammoCount } / { $totalAmmoCount }
rmc-dropship-equipment-deployer-status = Состояние: { $deployed }
rmc-dropship-equipment-deployer-auto-deploy = Автомат. разверт.: { $autoDeploy }

rmc-dropship-equipment-enabled = ВКЛЮЧЕНО
rmc-dropship-equipment-disabled = ВЫКЛЮЧЕНО
rmc-dropship-equipment-deployed = РАЗВЕРНУТО
rmc-dropship-equipment-undeployed = НЕ РАЗВЕРНУТО
rmc-dropship-equipment-operational = В СТРОЮ
rmc-dropship-equipment-damaged = ПОВРЕЖДЕНО
rmc-dropship-equipment-destroyed = УНИЧТОЖЕНО

rmc-dropship-launch-bay-fire-not-flying = Вы можете запускать оборудование только во время полёта.
rmc-dropship-launch-bay-screen-text = Внутренняя пусковая установка сторожевого корабля LAG-14
rmc-dropship-launch-bay-screen-text-loaded = Внутренняя пусковая установка сторожевого корабля LAG-14

    Заряжено: { $loaded }

    Боеприпасы: { $current } / { $max }

rmc-dropship-launch-alarm-xeno-shutdown = Мы рубим { $console }, заглушая его визг!
