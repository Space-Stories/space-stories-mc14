rmc-police-document-header =
    {""}[color=#8c0000][bold]КОРПУС МОРСКОЙ ПЕХОТЫ / ВОЕННАЯ ПОЛИЦИЯ[/bold][/color]
    {""}[head=2]{$title}[/head]
    {""}[bold]Форма:[/bold] [mono]{$form}[/mono]
    {""}[color=#8c0000]================================[/color]
rmc-police-document-record-title = Выписка из личного дела
rmc-police-document-section = [color=#8c0000][head=3]{$title}[/head][/color]
rmc-police-document-entry = [bold]{$number}. {$name}[/bold]
rmc-police-document-comment =
    {""}[bold]{$author}[/bold] [italic](Время операции: {$time})[/italic]
    {$text}
rmc-police-document-sentence-title = Приговор
rmc-police-document-sentence = [color=#8c0000][bold]Общий приговор: {$sentence}[/bold][/color]
rmc-police-document-fingerprint-count = [bold]Исследовано образцов:[/bold] {$count}
rmc-police-document-fingerprint-results = Результаты анализа
rmc-police-document-footer =

    {""}[color=#8c0000]================================[/color]
    {""}[italic]Для служебного пользования. Документ военной полиции.[/italic]
rmc-police-record-print =
    {""}[bold]Запись личного состава:[/bold] №{$record}

    {""}[color=#8c0000][head=3]Личные данные[/head][/color]
    {""}[bold]Имя:[/bold] {$name}
    {""}[bold]Должность:[/bold] {$rank}
    {""}[bold]Отряд:[/bold] {$squad}
    {""}[bold]Возраст:[/bold] {$age}
    {""}[bold]Пол:[/bold] {$gender}
    {""}[bold]Отпечаток пальца:[/bold] [mono]{$id}[/mono]

    {""}[color=#8c0000][head=3]Криминальные сведения[/head][/color]
    {""}[bold]Криминальный статус:[/bold] {$status}
    {""}[bold]Физическое состояние:[/bold] {$physical}
    {""}[bold]Психическое состояние:[/bold] {$mental}
    {""}[bold]Зарегистрированные нарушения:[/bold]
    {$crimes}
rmc-police-incident-print =
    {""}[bold]Отчёт:[/bold] [mono]{$report}[/mono]
    {""}[bold]Запись личного состава:[/bold] {$record}
    {""}[bold]Фигурант:[/bold] {$name}

    {""}[color=#8c0000][head=3]Описание происшествия[/head][/color]
    {$summary}
rmc-police-fingerprint-details =
    Должность: {$rank}
    Отряд: {$squad}
    Отпечаток пальца: {$fingerprint}
    Описание следа: {$description}
ent-RMCPoliceRecordPaper = криминальная запись
    .desc = Официальная выписка из базы личного состава военной полиции.
