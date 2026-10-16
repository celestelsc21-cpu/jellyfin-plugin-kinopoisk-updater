# Jellyfin.Plugin.KinopoiskUpdater

Плагин Jellyfin 10.11.x: Кинопоиск как источник метаданных и изображений для фильмов,
сериалов, сезонов и эпизодов. Обновление — стандартной командой Jellyfin
«Обновить метаданные».

**Статус: этап 2 из 4** — каркас, настройки, клиент API Кинопоиска и маппинг данных. Провайдеры метаданных
появятся на этапе 3.

## Как будет работать

1. Медиатека → «Kinopoisk» включён в источниках метаданных и изображений и стоит первым.
2. Фильм / сериал / сезон / эпизод → «⋮» → **«Обновить метаданные»** →
   «Заменить все метаданные» (+ «Заменить существующие изображения» для постера).
   Обновление сериала затрагивает все его сезоны и эпизоды, сезона — его эпизоды.
3. Найден не тот фильм → «⋮» → **«Определить»** → вариант из Кинопоиска или id
   с kinopoiskapiunofficial / kinopoisk.ru.

Данные берутся из неофициального API `kinopoiskapiunofficial.tech`; нужен API-ключ
сервиса (вводится в настройках плагина).

## Установка

Панель управления → Плагины → Репозитории → добавить:
`https://raw.githubusercontent.com/celestelsc21-cpu/jellyfin-plugin-kinopoisk-updater/manifest/manifest.json`,
затем Каталог → Kinopoisk Updater → Установить и перезапустить Jellyfin.

## Требования

- Jellyfin Server **10.11.x** (собрано против 10.11.11, `targetAbi` 10.11.0.0).
- .NET SDK 9.0.200+ (нужен для `.slnx`).

## Сборка

```
dotnet test -c Release
dotnet publish Jellyfin.Plugin.KinopoiskUpdater/Jellyfin.Plugin.KinopoiskUpdater.csproj -c Release -o out
```

При push в `main` GitHub Actions собирает и тестирует плагин; если версии из
`build.yaml` ещё нет в релизах — публикует zip и обновляет `manifest.json`
в ветке `manifest`.

## Лицензия

GPL-3.0.
