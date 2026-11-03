# {$entity} available for GENDER function purposes
rmc-latejoin-arrival-announcement = { $character } ({ $job }) { GENDER($entity) ->
    [male] пробудился
    [female] пробудилась
    [epicene] пробудились
   *[other] пробудился
} от гиперсна!
rmc-latejoin-arrival-announcement-special = Внимание всему экипажу, { $character } на борту!

rmc-earlyleave-cryo-announcement = { $character } ({ $job }) { GENDER($entity) ->
    [male] помещён
    [female] помещена
    [epicene] помещены
   *[other] помещён
} в криогенное хранилище!
