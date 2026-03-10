Goob-Station/
├── Content.Goobstation.Shared/
│   └── DoomArcade/
│       ├── SharedDoomArcadeComponent.cs    # Компонент + UI enum
│       └── DoomArcadeMessages.cs           # Сетевые сообщения (счёт)
│
├── Content.Goobstation.Server/
│   └── DoomArcade/
│       └── DoomArcadeSystem.cs             # Серверная система (хранит рекорд)
│
├── Content.Goobstation.Client/
│   └── DoomArcade/
│       ├── DoomArcadeBui.cs                # BoundUserInterface (открытие/закрытие окна)
│       ├── DoomArcadeControl.cs            # UI-контрол с raycasting-рендером и вводом
│       ├── DoomArcadeGame.cs               # Вся игровая логика (карта, враги, физика)
│       ├── DoomArcadeMenu.xaml             # Разметка окна (счёт, HP, игровое поле)
│       └── DoomArcadeMenu.xaml.cs          # Код окна (обновление лейблов)
│
└── Resources/
    └── Prototypes/
        └── Entities/
            └── Structures/
                └── Machines/
                    └── Computers/
                        └── doom_arcade.yml     # Прототип энтити автомата
