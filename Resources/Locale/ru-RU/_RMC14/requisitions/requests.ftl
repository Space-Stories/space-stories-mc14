rmc-requisitions-request-window-title = Консоль заявок снабжения
rmc-requisitions-contents-item = { $amount }× { $name }
rmc-requisitions-contents-random = { $item } (случайный выбор)
rmc-requisitions-contents-unspecified = Содержимое зависит от поставляемого предмета или указано в его описании.
rmc-requisitions-request-paper-overflow = Не показано позиций: { $count }. Полный список доступен в консоли снабжения.
rmc-requisitions-request-pack-contents = [color=#59604c]Состав позиции: { $contents }[/color]
rmc-requisitions-request-paper-item = [bold]{ $amount }×[/bold] { $name }: { $cost } $
rmc-requisitions-request-paper-items = [mono size=10]{ $contents }[/mono]
rmc-requisitions-request-paper-details = [mono size=11]{ $contents }[/mono]
rmc-requisitions-request-paper-reference = Заявка №{ $number }. Подробности доступны в консоли снабжения.
rmc-requisitions-request-paper-item-issued = [color=#59604c]{ $amount }× { $name } [bold](выдано)[/bold][/color]
rmc-requisitions-request-paper-completed = [color=#59604c][bold]ЗАЯВКА ВЫПОЛНЕНА[/bold][/color]
rmc-requisitions-request-paper-closed = [color=#86652f][bold]ЗАЯВКА ЗАКРЫТА[/bold][/color]
rmc-requisitions-request-paper-unissued-total = [bold]Не выдано:[/bold] { $amount } ед. / { $cost } $
rmc-requisitions-request-paper-issued-total = [bold]Выдано:[/bold] { $amount } ед.
rmc-requisitions-request-paper-remaining-total = [bold]К выдаче:[/bold] { $amount } ед. / { $cost } $
rmc-requisitions-request-no-access = Нет
rmc-requisitions-tab-requests = Заявки
rmc-requisitions-tab-requests-count = Заявки ({ $count })
rmc-requisitions-request-reason = Причина заказа:
rmc-requisitions-request-reason-placeholder = Объясните, зачем нужны эти припасы.
rmc-requisitions-request-reason-required = Укажите причину перед печатью заявки.
rmc-requisitions-request-submit = Распечатать заявку
rmc-requisitions-request-submit-confirm = Распечатать бланк?
rmc-requisitions-request-approve = Одобрить
rmc-requisitions-request-approve-confirm = Выполнить выбранное?
rmc-requisitions-request-deny = Закрыть заявку
rmc-requisitions-request-deny-confirm = Закрыть остаток заявки?
rmc-requisitions-request-title = Заявка №{ $number }
rmc-requisitions-request-requester = Заказчик: { $name } ({ $rank })
rmc-requisitions-request-reason-text = Причина: { $reason }
rmc-requisitions-request-item = { $amount }× { $name }: { $cost } $
rmc-requisitions-request-list-empty = Подходящих заявок нет. Вставьте бланк заявки, чтобы загрузить её в консоль.
rmc-requisitions-request-unavailable = Счёт снабжения недоступен. Корзина сохранена.
rmc-requisitions-request-not-allowed = Отправка заявок недоступна. Корзина сохранена.
rmc-requisitions-request-no-elevator = Грузовой лифт недоступен. Заявка остаётся в очереди.
rmc-requisitions-request-invalid-reason = Укажите причину допустимой длины.
rmc-requisitions-request-queue-full = Очередь заявок заполнена. Обратитесь в снабжение.
rmc-requisitions-request-invalid-items = В заявке указаны недоступные товары или превышено допустимое количество. Корзина сохранена.
rmc-requisitions-request-unknown-rank = Должность не указана
rmc-requisitions-request-submitted = Бланк заявки №{ $number } распечатан. Передайте его в отдел снабжения для загрузки в консоль.
rmc-requisitions-request-cannot-approve = Нечего выдавать или закупать. Проверьте выбранное количество и запасы. Заявка остаётся в очереди.
rmc-requisitions-request-paper-name = Бланк заявки снабжения №{ $number }
rmc-requisitions-request-paper =
    { "" }[color=#86652f][bold]▌ ОТДЕЛ СНАБЖЕНИЯ[/bold][/color]
    { "" }[color=#b18644]________________________________[/color]
    { "" }[head=2]Заявка на снабжение[/head]
    { "" }[mono size=10]ОП-103 / №{ $number }[/mono]{ $completion }

    { "" }[bold]Заказчик:[/bold] { $requester }
    { "" }[bold]Должность:[/bold] { $rank }
    { "" }[bold]Обоснование:[/bold] { $reason }

    { "" }[color=#86652f][bold]ЗАПРАШИВАЕМЫЕ ПРИПАСЫ[/bold][/color]
    { $contents }

    { $summary }
    { "" }[bold]Доступ к ящикам:[/bold] { $access }
    { "" }[color=#b18644]________________________________[/color]
    { "" }[mono size=10]Отметка отдела снабжения / печать:

    { "" }[color=#666666]Печать на бланке не подтверждает закупку.[/color][/mono]
rmc-requisitions-request-invoice-header = [head=2]{ $details }[/head]
rmc-requisitions-request-manifest =
    Заявка №{ $number }
    Заказчик: { $requester }
    Одобрил: { $approver }
