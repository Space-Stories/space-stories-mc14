rmc-mortar-deploy-start = Вы начинаете развертывание { $mortar }.
rmc-mortar-deploy-end-not-planet = Вы развертываете переносной комплект миномета M402. Это плохая идея.
rmc-mortar-shell-busy = Кто-то еще в настоящее время использует { $mortar }
rmc-mortar-not-aimed = { CAPITALIZE($mortar) } должен быть сначала нацелен.
rmc-mortar-covered = Возможно, вам не стоит разворачивать { $mortar } в помещении.
rmc-mortar-target-invalid = Вы не можете выстрелить { $mortar } в эту цель.
rmc-mortar-target-not-area = Эта территория запрещена!
rmc-mortar-target-covered = Вы не можете попасть в цель. Возможно, она находится под землей.
rmc-mortar-target-is-lz = Нельзя бомбить посадочную площадку!
rmc-mortar-bad-idea = Вы понимаете, насколько это плохая идея, и быстро останавливаетесь.
rmc-mortar-cant-insert = Вы не можете поместить { $shell } в { $mortar }!
rmc-mortar-not-deployed = Сначала нужно развернуть { $mortar }!
rmc-mortar-fire-cooldown = { CAPITALIZE($mortar) } Ствол все еще горячий. Подождите несколько секунд что бы продолжить стрельбу.
rmc-mortar-less-accurate-with-range = [color=red]Он будет тем менее точным, чем дальше цель![/color]
rmc-mortar-target-start-self = Вы начинаете корректировать угол и расстояние стрельбы { $mortar } в соответствии с новыми координатами.
rmc-mortar-target-start-others = { $user } начинает корректировать угол и дистанцию стрельбы { $mortar }.
rmc-mortar-target-finish-self = Вы завершаете настройку угла и дистанции стрельбы { $mortar } в соответствии с новыми координатами.
rmc-mortar-target-finish-others = { $user } завершает настройку угла и дистанции стрельбы { $mortar }.
rmc-mortar-dial-start-self = Вы начинаете набирать угол и дистанцию стрельбы { $mortar }, чтобы они соответствовали новым координатам.
rmc-mortar-dial-start-others = { $user } начинает набирать { $mortar } угол обстрела и дистанцию.
rmc-mortar-dial-finish-self = Вы завершаете набор { $mortar } угла и дистанции стрельбы, чтобы они соответствовали новым координатам.
rmc-mortar-dial-finish-others = { $user } завершает набор { $mortar } угла и дистанции стрельбы.

rmc-mortar-shell-load-start-self = Вы начинаете загружать { $shell } в { $mortar }.
rmc-mortar-shell-load-start-others = { $user } начинает загрузку { $shell } в { $mortar }.
rmc-mortar-shell-load-finish-self = Вы загружаете { $shell } в { $mortar }.
rmc-mortar-shell-load-finish-others = { $user } загружает { $shell } в { $mortar }.
rmc-mortar-shell-fire = { CAPITALIZE($mortar) } стреляет!
rmc-mortar-shell-warning = К ВАШЕМУ { $direction } приближается снаряд.
rmc-mortar-shell-warning-above = ПРЯМО НАД ВАМИ ПАДАЕТ СНАРЯД.
rmc-mortar-shell-impact-warning = СНАРЯД ВОТ-ВОТ УДАРИТ РЯДОМ { $direction }
rmc-mortar-shell-impact-warning-above = СНАРЯД ВОТ-ВОТ УПАДЕТ ПРЯМО НАД ВАМИ.

rmc-mortar-interface = Интерфейс миномета
rmc-mortar-target-title = Координаты цели
rmc-mortar-offset-title = Смещение цели

rmc-mortar-target-x = Цель X:
rmc-mortar-target-y = Цель Y:
rmc-mortar-target-set = Установить цель
rmc-mortar-target-too-close = Вы не можете прицелиться по этой координате, она находится слишком близко к вашему миномету.
rmc-mortar-target-too-far = Вы не можете прицелиться по этой координате, она находится слишком близко к вашему миномету.

rmc-mortar-offset-x = X Смещение:
rmc-mortar-offset-y = Y Смещение:
rmc-mortar-offset-set = Откалибровать смещение
rmc-mortar-offset-too-far = Вы не можете прицелиться по этой координате, она находится слишком близко к вашему миномету.
rmc-mortar-offset-too-close = Вы не можете прицелиться по этой координате, она находится слишком близко к вашему миномету.
rmc-mortar-offset-max = Максимальный набор
  смещение: { $max }

rmc-mortar-view-camera = Посмотреть
  Камеру

rmc-mortar-camera-title = Интерфейс камеры миномета
rmc-mortar-camera-name = Паракам ({ $x }):({ $y })

# Targeting mode
rmc-mortar-toggle-mode = Переключить режим прицеливания
rmc-mortar-toggle-mode-message = Переключение между режимами координатного и лазерного наведения

rmc-mortar-coordinates-mode-switched-self = Вы переключаете { THE($mortar) } в режим прицеливания по координатам.
rmc-mortar-laser-mode-switched-self = Вы переключаете { THE($mortar) } в режим лазерного прицеливания.

rmc-mortar-coordinates-mode-switched-others = { $user } переключает { THE($mortar) } в режим наведения по координатам.
rmc-mortar-laser-mode-switched-others = { $user } переключает { THE($mortar) } в режим лазерного прицеливания.

rmc-mortar-linking-start = Вы начинаете связывать { THE($laserDesignator) } с { THE($mortar) }.
rmc-mortar-laser-linked-self = Вы успешно связали { THE($laserDesignator) } с { THE($mortar) }.
rmc-mortar-laser-linked-others = { $user } успешно связывает { THE($laserDesignator) } с { THE($mortar) }.
rmc-mortar-already-linking = { CAPITALIZE(THE($mortar)) } уже связан с лазерным целеуказателем.

rmc-mortar-no-laser-target = { CAPITALIZE(THE($mortar)) } не имеет лазерной наводки для стрельбы!
rmc-mortar-no-laser-designator = { CAPITALIZE(THE($mortar)) } не имеет связанного лазерного целеуказателя!

rmc-mortar-in-coordinates-mode = { CAPITALIZE(THE($mortar)) } сейчас установлен в режим наведения по координатам.
rmc-mortar-in-laser-mode = { CAPITALIZE(THE($mortar)) } сейчас установлен в режим лазерного наведения.

rmc-mortar-laser-aimed = { CAPITALIZE(THE($mortar)) } наведен на цель и готов к стрельбе!

rmc-mortar-toggle-mode-hint = [color=cyan]Нажмите Alt + ЛКМ, чтобы переключить режимы наведения.[/color]

rmc-mortar-dial-coordinates = Если { CAPITALIZE(THE($mortar)) } установлен в режим лазерного наведения, переключитесь в режим координатного наведения, чтобы ввести координаты!

rmc-mortar-beeping = пищит!
rmc-mortar-beeping-warning = нехорошо пищит!
rmc-mortar-targeting = { CAPITALIZE(THE($mortar)) } все ещё нацелен.
