command-description-mutiny-end = Завершает активный мятеж и удаляет все связанные с ним состояния.
command-description-mutiny-ismutineer = Возвращает true, если сущность является активным мятежником, иначе возвращает false.
command-description-mutiny-list = Выводит текущую фазу мятежа, лидеров, завербованных и участников.
command-description-mutiny-makemutineer = Добавляет завербованного либо делает сущность мятежником во время активного мятежа.
command-description-mutiny-removemutineer = Исключает завербованного либо делает активного мятежника некомбатантом.
command-description-mutiny-makemutineerleader = Создаёт общий мятеж либо присоединяет к нему сущность в качестве лидера.
command-description-mutiny-removemutineerleader = Снимает с сущности статус лидера мятежа, не меняя выбранную сторону.
command-description-mutiny-makeloyalist = Делает сущность лоялистом во время активного мятежа.
command-description-mutiny-makenoncombatant = Делает сущность некомбатантом во время активного мятежа.

ent-ActionMutineerRecruit = Вербовать мятежника
    .desc = Предложить подходящему морпеху присоединиться к мятежу, когда тот начнётся.
ent-ActionMutineerBegin = Начать мятеж
    .desc = Начать мятеж со всеми завербованными сторонниками.

mutineer-status-added = [bold][color=red]Теперь вы мятежник![/color][/bold]
    Перед участием ознакомьтесь с правилами проведения мятежей.
mutineer-status-removed = Вы больше не являетесь мятежником.
mutineer-leader-status-added = [bold][color=red]Вы стали лидером мятежа.[/color][/bold]
    Заручитесь поддержкой, а когда будете готовы, используйте действие «Начать мятеж».
mutineer-leader-status-removed = Вы больше не являетесь лидером мятежа.
rmc-mutiny-loyalist-status-added = [bold][color=red]Теперь вы лоялист![/color][/bold]
    Перед участием ознакомьтесь с правилами проведения мятежей.
rmc-mutiny-noncombatant-status-added = [bold][color=red]Теперь вы некомбатант![/color][/bold]
    Не участвуйте в бою. Вы можете лечить обе стороны, но не должны нападать сами, и никто не должен нападать на вас.

mutineer-invite-title = Приглашение присоединиться к мятежу
mutineer-invite-text = Вас приглашают присоединиться к мятежу.
    Перед принятием приглашения прочтите и усвойте правила проведения мятежей и бунтов («Основные правила» → «Мятежи, бунты»).
mutineer-invite-accept = Присоединиться
mutineer-invite-deny = Отказаться
rmc-mutiny-recruit-sent = Приглашение присоединиться к мятежу отправлено.
rmc-mutiny-recruit-accepted = Когда начнётся мятеж, вы станете мятежником. Готовьтесь, но до его начала никому не причиняйте вреда.

rmc-mutiny-begin-title = Начать мятеж?
rmc-mutiny-begin-text = Вы уверены, что хотите начать мятеж?
rmc-mutiny-begin-accept = Начать
rmc-mutiny-begin-deny = Отмена

rmc-mutiny-side-title = Выберите сторону
rmc-mutiny-side-text = Начался мятеж. На чьей вы стороне?
    Перед выбором стороны прочтите и усвойте правила проведения мятежей и бунтов («Основные правила» → «Мятежи, бунты»).
    Если вы закроете это окно или не сделаете выбор за 20 секунд, то откажетесь сражаться.
rmc-mutiny-side-mutineer = Мятежники
rmc-mutiny-side-loyalist = Лоялисты
rmc-mutiny-side-refuse = Отказаться сражаться

rmc-mutiny-announcement = ОПАСНОСТЬ: получено сообщение о происходящем мятеже. Код действий: задерживать, арестовывать, защищать.

rmc-mutiny-error-invalid-member = Цель не может участвовать в мятеже КМ США.
rmc-mutiny-error-invalid-recruit = Этого морпеха нельзя завербовать в мятежники.
rmc-mutiny-error-rule = Не удалось запустить игровое правило мятежа.
rmc-mutiny-error-other-rule = Разум цели уже участвует в другом мятеже.
rmc-mutiny-error-no-rule = Активного мятежа нет. Сначала назначьте лидера мятежа.
rmc-mutiny-error-not-active = Мятеж ещё не начался.
rmc-mutiny-error-not-recruiting = Набор участников мятежа уже завершён.
rmc-mutiny-error-not-leader = Цель не является лидером мятежа.
rmc-mutiny-error-not-mutineer = Цель не является мятежником или завербованным.
rmc-mutiny-error-leader-side = Перед назначением стороны снимите с цели статус лидера мятежа.
rmc-mutiny-error-remove-leader-first = Перед снятием статуса мятежника снимите с цели статус лидера мятежа.

rmc-mutiny-admin-leader-added = { $player } назначен лидером мятежа.
rmc-mutiny-admin-leader-removed = { $player } больше не является лидером мятежа.
rmc-mutiny-admin-recruit-accepted = { $target } принял приглашение присоединиться к мятежу от { $leader }.
rmc-mutiny-admin-begun = { $leader } начал мятеж.

rmc-mutiny-verb-make-leader = Назначить лидером мятежа
rmc-mutiny-verb-remove-leader = Снять статус лидера мятежа
rmc-mutiny-verb-make-mutineer = Сделать мятежником
rmc-mutiny-verb-remove-mutineer = Снять статус мятежника

rmc-mutiny-command-success = Состояние мятежа обновлено.
rmc-mutiny-command-list-none = Активного мятежа нет.
rmc-mutiny-command-list-header = Текущая фаза мятежа: { $phase }
rmc-mutiny-phase-recruiting = Набор участников
rmc-mutiny-phase-active = Активный мятеж
rmc-mutiny-side-name-mutineer = Мятежник
rmc-mutiny-side-name-loyalist = Лоялист
rmc-mutiny-side-name-noncombatant = Некомбатант
rmc-mutiny-command-list-recruit = Завербован
rmc-mutiny-command-list-unassigned = Сторона не выбрана
rmc-mutiny-command-list-leader = , лидер
rmc-mutiny-command-list-entry = - { $player }: { $state }{ $leader }
