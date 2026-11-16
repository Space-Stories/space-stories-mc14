# Give Commendation Command
cmd-rmcgivecommendation-desc = Вручает игроку медаль или желе.
cmd-rmcgivecommendation-help = Использование: rmcgivecommendation <giverName> <receiver> <receiverName> <type> <commendationType> <citation> [roundId]
  Аргументы:
  giverName: Кто вручает награду IC (необходимо использовать кавычки, если присутствуют пробелы)
  receiver: CKey или ID получателя
  receiverName: Имя персонажа (обязательно используйте кавычки, если имя содержит пробелы)
  type: медаль или желе
  commendationType: число (используйте автодополнение по клавише Tab, чтобы увидеть доступные типы)
  citation: Причина награждения (обязательно в кавычках)
  roundId: Номер раунда, по умолчанию — текущий раунд (необязательный аргумент)

  Примеры:
    rmcgivecommendation "КМП США Высшее Командование" PlayerName "John Doe" medal 1 "For exceptional bravery"
    rmcgivecommendation "Королева Мать" XenoPlayer "XX-Alpha" jelly 2 "For defending the hive"
    rmcgivecommendation "КМП США Высшее Командование" PlayerName "John Doe" medal 1 "For exceptional bravery" 42

# Errors
cmd-rmcgivecommendation-invalid-arguments = Неверное количество аргументов!
cmd-rmcgivecommendation-invalid-type = Неизвестный тип! Должен быть 'medal' или 'jelly'.
cmd-rmcgivecommendation-invalid-award-type = Неверный вид награды '{ $type }' type! Должен быть 1-{ $max }.
cmd-rmcgivecommendation-empty-citation = Должна быть причина!
cmd-rmcgivecommendation-player-not-found = Игрок '{ $player }' не найден.

# Success
cmd-rmcgivecommendation-success = { $award } выдана { $player }!
cmd-rmcgivecommendation-admin-announcement = { $admin } выдал { $type } "{ $award }"  { $receiver } (Имя: { $character }) for Round { $round }

# Completion hints
cmd-rmcgivecommendation-hint-giver = Имя персонажа выдающего (будьте внимательны при вводе)
cmd-rmcgivecommendation-hint-giver-highcommand = Стандартный награждающий для морпехов
cmd-rmcgivecommendation-hint-giver-queen-mother = Стандартный награждающий для ксеноморфов
cmd-rmcgivecommendation-hint-receiver = CKey или ID получателя
cmd-rmcgivecommendation-hint-receiver-name = Имя персонажа получателя (будьте внимательны при вводе)
cmd-rmcgivecommendation-hint-type = Тип (medal or jelly)
cmd-rmcgivecommendation-hint-type-medal = Наградить морского пехотинца медалью
cmd-rmcgivecommendation-hint-type-jelly = Наградить ксеноморфа королевским желе
cmd-rmcgivecommendation-hint-medal-type = Вид медали (1-{ $count })
cmd-rmcgivecommendation-hint-jelly-type = Вид желе (1-{ $count })
cmd-rmcgivecommendation-hint-invalid-type = Тип должен быть 'medal' или 'jelly'
cmd-rmcgivecommendation-hint-citation = Причина (будьте внимательны при вводе)
cmd-rmcgivecommendation-hint-round = ID раунда (необязательно)
cmd-rmcgivecommendation-hint-round-current = Текущий раунд
