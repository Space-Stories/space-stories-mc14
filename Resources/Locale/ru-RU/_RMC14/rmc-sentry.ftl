rmc-sentry-need-open-area = Чтобы построить { $sentry }, вам нужно свободное пространство, но что-то преграждает путь перед вами!
rmc-sentry-on = { CAPITALIZE($sentry) } гудит и издает несколько звуковых сигналов.
rmc-sentry-off = { CAPITALIZE($sentry) } выключается и замолкает.
rmc-sentry-magazine-invalid = { CAPITALIZE($item) } не является подходящим магазином!
rmc-sentry-magazine-swap-not-empty = Вы не сможете заменить магазин, пока он не опустеет.
rmc-sentry-magazine-swap-start-user = Вы начинаете заменять новый { $magazine } на { $sentry }.
rmc-sentry-magazine-swap-start-others = { $user } начинает заряжать { $magazine } в { $sentry }.
rmc-sentry-magazine-swap-finish-user = Вы заменяете { $magazine } на { $sentry }.
rmc-sentry-magazine-swap-finish-others = { $user } меняет { $magazine } на { $sentry }.
rmc-sentry-limited-rotation = [color=red]При стрельбе он может повернуться максимум на { $degrees } градусов.[/color]
rmc-sentry-disassembled-with-multitool = Его можно разобрать с помощью [color=cyan]мультиинструмента[/color].
rmc-sentry-rotate-with-screwdriver = Его можно поворачивать с помощью [color=cyan]отвертки[/color].
rmc-sentry-too-close = Это слишком близко к { INDEFINITE($defense) } { $defense }!
rmc-sentry-active-norot = { CAPITALIZE($sentry) } в настоящее время активен. Моторы не позволят вам безопасно вращать его.
rmc-sentry-item-norot = Вы не можете вращать его так!
rmc-sentry-magazine-does-not-fit = { CAPITALIZE($magazine) } не вписывается в { $sentry }!

rmc-sentry-upgrade-not-item = Перед обновлением необходимо разобрать { $sentry } с помощью мультиинструмента!
rmc-sentry-upgrade-not-holding = Чтобы обновить { $sentry }, вам нужно держать в активной руке набор для обновления!
rmc-sentry-upgrade-not-upgradeable = Вы не можете обновить { $sentry }!

rmc-sentry-spikes-self = Стальные клыки турели вонзаются в вас!
rmc-sentry-spikes-others = Стальные клыки турели вонзаются в { $target }!

rmc-sentry-rotate-self = Вы поворачиваете { $sentry }.
rmc-sentry-rotate-others = { $user } вращает { $sentry }.

rmc-sentry-disassemble-start-self = Вы начинаете разбирать { $sentry }.
rmc-sentry-disassemble-start-others = { $user } начинает разбирать { $sentry }.
rmc-sentry-disassemble-finish-self = Вы разобрали { $sentry }.
rmc-sentry-disassemble-finish-others = { $user } разобрал { $sentry }.

rmc-sentry-unanchor-is-on = { CAPITALIZE($sentry) } в настоящее время активен. Моторы не позволят вам безопасно разобрать её.

rmc-sentry-not-emergency = { CAPITALIZE($deployer) } можно активировать только в чрезвычайных ситуациях.
rmc-sentry-deploy = Вы развертываете { $spawned }

rmc-sentry-laptop-title = Sentry Network Control
rmc-sentry-laptop-header = [color=#88C7FA][font size=16][bold]SENTRY NETWORK - {$count}/{$max} LINKED[/bold][/font][/color]
rmc-sentry-laptop-status-none = Status: No sentries linked
rmc-sentry-laptop-status-count = Status: {$online}/{$total} Online
rmc-sentry-laptop-card-default-name = [color=#88C7FA][bold]UN 571-C Sentry Gun[/bold][/color]
rmc-sentry-laptop-status-online = [color=#229132][ONLINE][/color]
rmc-sentry-laptop-status-offline = [color=#CED22B][OFFLINE][/color]
rmc-sentry-laptop-status-packed = [color=#A42625][OFFLINE][/color]
rmc-sentry-laptop-name = NAME:
rmc-sentry-laptop-name-placeholder = Enter custom name...
rmc-sentry-laptop-health = HEALTH
rmc-sentry-laptop-ammo = AMMO
rmc-sentry-laptop-mode = MODE
rmc-sentry-laptop-mode-active = [color=#229132]Active[/color]
rmc-sentry-laptop-mode-standby = [color=#CED22B]Standby[/color]
rmc-sentry-laptop-mode-packed = [color=#A42625]Packed[/color]
rmc-sentry-laptop-location = LOCATION
rmc-sentry-laptop-unknown = Unknown
rmc-sentry-laptop-target = TARGET
rmc-sentry-laptop-target-none = [color=#B0B0B0]None[/color]
rmc-sentry-laptop-iff-controls = IFF TARGETING CONTROLS
rmc-sentry-laptop-reset-default = RESET TO DEFAULT
rmc-sentry-laptop-toggle-power = TOGGLE POWER
rmc-sentry-laptop-view-cam = VIEW CAM
rmc-sentry-laptop-unlink = UNLINK
rmc-sentry-laptop-select-all = Select All
rmc-sentry-laptop-deselect-all = Deselect All
rmc-sentry-laptop-global-iff = GLOBAL IFF
rmc-sentry-laptop-refresh = REFRESH
rmc-sentry-laptop-unlink-all = UNLINK ALL
rmc-sentry-laptop-search = SEARCH:
rmc-sentry-laptop-search-placeholder = Search sentries by name or location...
rmc-sentry-laptop-global-iff-controls = GLOBAL IFF TARGETING CONTROLS
rmc-sentry-laptop-apply-all = APPLY TO ALL
rmc-sentry-laptop-reset-all-default = RESET ALL TO DEFAULT
rmc-sentry-laptop-power-all-on = POWER ALL ON
rmc-sentry-laptop-power-all-off = POWER ALL OFF
rmc-sentry-laptop-camera-view = [color=#88C7FA][bold]CAMERA VIEW[/bold][/color]
rmc-sentry-laptop-camera-title = [color=#88C7FA][bold]CAMERA: {$name} (Range: {$range} tiles, FOV: {$fov})[/bold][/color]
rmc-sentry-laptop-close = CLOSE
rmc-sentry-laptop-place-on-table = Place the laptop on a table first!
rmc-sentry-laptop-must-be-open = The laptop must be opened first!
rmc-sentry-laptop-max-sentries = The laptop can only control {$max} sentries at once!
rmc-sentry-laptop-linked = Successfully linked {$sentry} to the laptop.
rmc-sentry-laptop-alert-low-ammo = {$sentry}: LOW AMMO ({$ammo}/{$maxAmmo})
rmc-sentry-laptop-alert-critical-damage = {$sentry}: CRITICAL DAMAGE
rmc-sentry-laptop-alert-damaged = {$sentry}: Taking damage! ({$percent}% health)
rmc-sentry-laptop-alert-engaging = {$sentry}: Engaging {$target}
