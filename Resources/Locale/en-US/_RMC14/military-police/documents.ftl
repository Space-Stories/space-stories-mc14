rmc-police-document-header =
    {""}[color=#8c0000][bold]MARINE CORPS / MILITARY POLICE[/bold][/color]
    {""}[head=2]{$title}[/head]
    {""}[bold]Form:[/bold] [mono]{$form}[/mono]
    {""}[color=#8c0000]================================[/color]
rmc-police-document-record-title = Personnel record extract
rmc-police-document-section = [color=#8c0000][head=3]{$title}[/head][/color]
rmc-police-document-entry = [bold]{$number}. {$name}[/bold]
rmc-police-document-comment =
    {""}[bold]{$author}[/bold] [italic](Operation time: {$time})[/italic]
    {$text}
rmc-police-document-sentence-title = Sentence
rmc-police-document-sentence = [color=#8c0000][bold]Total sentence: {$sentence}[/bold][/color]
rmc-police-document-fingerprint-count = [bold]Samples analysed:[/bold] {$count}
rmc-police-document-fingerprint-results = Analysis results
rmc-police-document-footer =

    {""}[color=#8c0000]================================[/color]
    {""}[italic]For official use. Military police records.[/italic]
rmc-police-record-print =
    {""}[bold]Personnel record:[/bold] #{$record}

    {""}[color=#8c0000][head=3]Personal data[/head][/color]
    {""}[bold]Name:[/bold] {$name}
    {""}[bold]Position:[/bold] {$rank}
    {""}[bold]Squad:[/bold] {$squad}
    {""}[bold]Age:[/bold] {$age}
    {""}[bold]Gender:[/bold] {$gender}
    {""}[bold]Fingerprint:[/bold] [mono]{$id}[/mono]

    {""}[color=#8c0000][head=3]Security data[/head][/color]
    {""}[bold]Criminal status:[/bold] {$status}
    {""}[bold]Physical status:[/bold] {$physical}
    {""}[bold]Mental status:[/bold] {$mental}
    {""}[bold]Reported offences:[/bold]
    {$crimes}
rmc-police-incident-print =
    {""}[bold]Report:[/bold] [mono]{$report}[/mono]
    {""}[bold]Personnel record:[/bold] {$record}
    {""}[bold]Subject:[/bold] {$name}

    {""}[color=#8c0000][head=3]Incident summary[/head][/color]
    {$summary}
rmc-police-fingerprint-details =
    Position: {$rank}
    Squad: {$squad}
    Fingerprint: {$fingerprint}
    Trace description: {$description}
ent-RMCPoliceRecordPaper = security record
    .desc = An official extract from the military police personnel database.
