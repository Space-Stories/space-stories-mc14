rmc-xeno-failed-cant-infect = Вы не можете заразить { $target }!
rmc-xeno-failed-cant-reach = Вы не можете достать { $target }, они должны лежать!
rmc-xeno-failed-target-dead = Вы не можете заразить мертвого!
rmc-xeno-infect-success = Лицехват врезается в маску { $target } и срывает ее!
rmc-xeno-infect-fail = Лицехват разбивается о { $target } { $clothing }!
rmc-xeno-failed-parasite-dead = Мы не можем заразить мертвого лицехвата!
rmc-xeno-cant-throw = Мы не можем бросить { $target }!

rmc-xeno-parasite-dead = { CAPITALIZE(SUBJECT($parasite)) } { CONJUGATE-BE($parasite) } не двигается.
rmc-xeno-parasite-announce-infect = Мы чувствуем, что { $xeno } заразил носителя в { $location }!

rmc-xeno-parasite-player-pickup = { CAPITALIZE($parasite) } может обрабатывать { REFLEXIVE($parasite) }!
rmc-xeno-parasite-nonplayer-pull = Вытаскивание { $parasite } может повредить { OBJECT($parasite) }!

rmc-xeno-parasite-ai-active = { CAPITALIZE(SUBJECT($parasite)) } кажется активным.
rmc-xeno-parasite-ai-idle = { CAPITALISE(SUBJECT($parasite)) } { CONJUGATE-BE($parasite) } отдых.
rmc-xeno-parasite-ai-dying = [color=red]{ CAPITALIZE(SUBJECT($parasite)) } должен вернуться в безопасное место![/color]

rmc-xeno-parasite-ai-eaten = { CAPITALIZE($parasite) } яростно поедается другими лицехватами, находящимися поблизости!

rmc-xeno-parasite-ghost-carrier-none = { $xeno } не имеет укрытых лицехватов!
rmc-xeno-parasite-ghost-carrier-reserved = { $xeno } зарезервировал оставшихся лицехватов для { REFLEXIVE($xeno) }.
rmc-xeno-parasite-ghost-carrier-dead = { $xeno } мертв, и все { POSS_ADJ($xeno) } умерли вместе с { OBJECT($xeno) }.

rmc-xeno-parasite-carrier-death = Щебечущая масса крошечных инопланетян пытается спастись { $xeno }!

rmc-xeno-parasite-thrower-on-fire = Retrieving a stored parasite while we're on fire would burn it!
rmc-xeno-parasite-touch-on-fire = Touching the parasite while you're on fire would burn it!
rmc-xeno-parasite-hits-left = It can take [color={$color}]{$remaining}[/color] more {$remaining ->
    [one] hit
    *[other] hits
}.
rmc-xeno-infected-examine-xeno = This one is hosting a sister! She will emerge in time.
rmc-xeno-infected-examine-ghost = This creature is infected.
