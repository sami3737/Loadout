Your players can customize their starting kit loadout.

By default all items are blacklisted, you have to configure and add all your item into the config file using permission.

```json
"WhiteListItem": {
  "player": [
    "rifle.ak",   
    "rifle.bolt",   
    "syringe.medical",   
    "largemedkit",   
    "weapon.mod.holosight",    
    "weapon.mod.simplesight",   
    "black.raspberries",   
    "rifle.l96",   
    "weapon.mod.8x.scope",   
    "weapon.mod.flashlight",   
    "weapon.mod.muzzleboost",   
    "weapon.mod.muzzlebrake",    
    "weapon.mod.lasersight",   
    "weapon.mod.silencer",    
    "rocket.launcher",     
    "weapon.mod.small.scope",       
    "rifle.lr300",  
    "wall.external.high", 
    "smg.thompson"
  ],
  "vip2": [
    "rifle.l96"
  ]
}
```

You can also put a cooldown on the command and limit the item stack.

By default, there are 2 examples:

```json
"WhiteListItem": {
  "vip1": [
    "rifle.ak",
    "blueberries",
    "syringe.medical",
    "largemedkit",
    "pickaxe",
    "hatchet"
  ],
  "vip2": [
    "sulfur.ore",
    "hq.metal.ore",
    "metal.ore"
  ]
}
```



From this example, you can grant the following permission "loadout.vip1" & "loadout.vip2"

Command:

/loadout <save|reset>

Permission:

The permission system depends on the name of your package in your config file.

Item list here => [Rust Items list](https://www.corrosionhour.com/rust-item-list/)

[![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/wOFS4fWyGyw/0.jpg)](https://www.youtube.com/watch?v=wOFS4fWyGyw)
