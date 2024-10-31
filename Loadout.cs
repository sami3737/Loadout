using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Loadout", "Sami37", "1.0.3")]
    [Description("Save you inventory to respawn with it.")]
    class Loadout : RustPlugin
    {
        Dictionary<string, List<Dictionary<string, object>>> PlayerData = new Dictionary<string, List<Dictionary<string, object>>>();
        Dictionary<string, List<Dictionary<string, object>>> PlayerDataWear = new Dictionary<string, List<Dictionary<string, object>>>();
        List<string> CanRedeem = new List<string>();
        private Dictionary<string, object> AutorizedItemList = new Dictionary<string, object>();
		private int Cooldown;
        private Dictionary<string, Timer> time = new Dictionary<string, Timer>();
        private Dictionary<string, object> stackSize = new Dictionary<string, object>();

        #region ConfigFunction

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator,
            (from val in list select val.ToString()).Skip(first).ToArray());

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            stringArgs.RemoveAt(args.Length - 1);
            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        T GetConfig<T>(T defaultVal, params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList();
            if (Config.Get(stringArgs.ToArray()) == null)
            {
                PrintError(
                    $"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin.");
                return defaultVal;
            }

            return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T));
        }

        #endregion

        void LoadData()
        {
            PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<Dictionary<string, object>>>>(Name);
            PlayerDataWear = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<Dictionary<string, object>>>>(Name + "_Wear");
        }

        void LoadDefaultConfig()
        {
            AutorizedItemList = new Dictionary<string, object>
            {
                {
                    "vip1", new List<string>
                        { "sulfur.ore"}
                },
                {
                    "vip2", new List<string>
                    {"sulfur.ore", "hq.metal.ore", "metal.ore"}
                }
            };
            foreach (var item in ItemManager.itemList)
            {
                if(!stackSize.ContainsKey(item.shortname))
                    stackSize.Add(item.shortname, item.stackable);
            }
            SetConfig("WhiteListItem", AutorizedItemList);
            SetConfig("StackSize", stackSize);
            SetConfig("Cooldown", Cooldown);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            AutorizedItemList = GetConfig(AutorizedItemList, "WhiteListItem");
            Cooldown = GetConfig(Cooldown, "Cooldown");
            stackSize = GetConfig(stackSize, "StackSize");

            LoadData();
            foreach (var VARIABLE in AutorizedItemList)
            {
                permission.RegisterPermission("loadout." + VARIABLE.Key, this);
            }

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPerm", "You don't have the perm to use this command." },
                {"Saved", "Loadout saved" },
                {"Reset", "Loadout reset" },
                {"NotNow", "You can't use this command, you must wait." }
            }, this);
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, PlayerData);
            Interface.Oxide.DataFileSystem.WriteObject(Name + "_Wear", PlayerDataWear);
        }

        void Unload()
        {
            SaveData();
        }

        void OnPlayerRespawned(BasePlayer player)
        {
            if (!PlayerData.ContainsKey(player.UserIDString) && !PlayerDataWear.ContainsKey(player.UserIDString)) return;
            foreach (var VARIABLE in AutorizedItemList)
            {
                if (permission.UserHasPermission(player.UserIDString, "loadout." + VARIABLE.Key))
                    if (!CanRedeem.Contains(player.UserIDString))
                    {
                        CanRedeem.Add(player.UserIDString);
                    }
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!PlayerData.ContainsKey(player.UserIDString) && !PlayerDataWear.ContainsKey(player.UserIDString)) return;
            foreach (var VARIABLE in AutorizedItemList)
            {
                if (permission.UserHasPermission(player.UserIDString, "loadout." + VARIABLE.Key))
                    if (CanRedeem.Contains(player.UserIDString))
                    {
                        Cleainventory(player);
                        CanRedeem.Remove(player.UserIDString);
                    }
            }
        }

        void Cleainventory(BasePlayer player)
        {
            if (!PlayerData.ContainsKey(player.UserIDString) && !PlayerDataWear.ContainsKey(player.UserIDString)) return;
            for (int i = 0; i < player.inventory.containerBelt.capacity; i++)
            {
                var existingItem = player.inventory.containerBelt.GetSlot(i);
                if (existingItem != null)
                {
                    existingItem.RemoveFromContainer();
                    existingItem.Remove(0f);
                }
            }
            for (int i = 0; i < player.inventory.containerWear.capacity; i++)
            {
                var existingItem = player.inventory.containerWear.GetSlot(i);
                if (existingItem != null)
                {
                    existingItem.RemoveFromContainer();
                    existingItem.Remove(0f);
                }
            }

            RestoreInventory(player);
        }

        void RestoreInventory(BasePlayer player)
        {
            if (!PlayerData.ContainsKey(player.UserIDString) && !PlayerDataWear.ContainsKey(player.UserIDString)) return;
            foreach (var item in PlayerData[player.UserIDString])
            {
                var data = item;
                if (data == null) continue;
                var itemid = Convert.ToInt32(data["id"]);
                var itemamount = Convert.ToInt32(data["amount"]);
                var itemskin = ulong.Parse(data["skinid"].ToString());
                var itemcondition = Convert.ToSingle(data["condition"]);
                var slot = Convert.ToInt32(data["slot"]);

                var itemCreated = ItemManager.CreateByItemID(itemid, itemamount, itemskin);
                if (itemCreated != null)
                {
                    itemCreated.condition = itemcondition;
                    if (data.ContainsKey("magazine"))
                    {
                        Dictionary<string, object> magazine;
                        if (!(data["magazine"] is Dictionary<string, object>))
                            magazine = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["magazine"]
                                .ToString());
                        else
                            magazine = data["magazine"] as Dictionary<string, object>;
                        if (magazine == null) continue;
                        var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
                        var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());
                        var heldent = itemCreated.GetHeldEntity();
                        if (heldent != null)
                        {
                            var projectiles = heldent.GetComponent<BaseProjectile>();
                            if (projectiles != null)
                            {
                                projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                                projectiles.primaryMagazine.contents = ammoamount;
                            }
                        }
                    }

                    if (data.ContainsKey("items"))
                    {
                        List<object> mod;
                        if (!(data["items"] is List<object>))
                            mod = JsonConvert.DeserializeObject<List<object>>(data["items"]
                                .ToString());
                        else
                            mod = data["items"] as List<object>;
                        if (mod == null) continue;
                        foreach (var moddata in mod)
                        {
                            Dictionary<string, object> modata;
                            if (!(moddata is Dictionary<string, object>))
                                modata = JsonConvert.DeserializeObject<Dictionary<string, object>>(moddata.ToString());
                            else
                                modata = moddata as Dictionary<string, object>;
                            if (modata == null) continue;
                            var modid = Convert.ToInt32(modata["id"]);
                            var modamount = Convert.ToInt32(modata["amount"]);
                            if(itemCreated.contents == null)
                                itemCreated.contents = new ItemContainer();
                            itemCreated.contents.AddItem(ItemManager.FindItemDefinition(modid), modamount);
                        }
                    }

                    itemCreated.MoveToContainer(player.inventory.containerBelt, slot);
                }
            }
            foreach (var item in PlayerDataWear[player.UserIDString])
            {
                var data = item;
                if (data == null) continue;
                var itemid = Convert.ToInt32(data["id"]);
                var itemamount = Convert.ToInt32(data["amount"]);
                var itemskin = ulong.Parse(data["skinid"].ToString());
                var itemcondition = Convert.ToSingle(data["condition"]);
                var slot = Convert.ToInt32(data["slot"]);

                var itemCreated = ItemManager.CreateByItemID(itemid, itemamount, itemskin);
                if (itemCreated != null)
                {
                    itemCreated.condition = itemcondition;
                    if (data.ContainsKey("magazine"))
                    {
                        Dictionary<string, object> magazine;
                        if (!(data["magazine"] is Dictionary<string, object>))
                            magazine = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["magazine"]
                                .ToString());
                        else
                            magazine = data["magazine"] as Dictionary<string, object>;
                        if (magazine == null) continue;
                        var ammotype = int.Parse(magazine.Keys.ToArray()[0]);
                        var ammoamount = int.Parse(magazine[ammotype.ToString()].ToString());
                        var heldent = itemCreated.GetHeldEntity();
                        if (heldent != null)
                        {
                            var projectiles = heldent.GetComponent<BaseProjectile>();
                            if (projectiles != null)
                            {
                                projectiles.primaryMagazine.ammoType = ItemManager.FindItemDefinition(ammotype);
                                projectiles.primaryMagazine.contents = ammoamount;
                            }
                        }
                    }

                    if (data.ContainsKey("items"))
                    {
                        List<object> mod;
                        if (!(data["items"] is List<object>))
                            mod = JsonConvert.DeserializeObject<List<object>>(data["items"]
                                .ToString());
                        else
                            mod = data["items"] as List<object>;
                        if (mod == null) continue;
                        foreach (var moddata in mod)
                        {
                            Dictionary<string, object> modata;
                            if (!(moddata is Dictionary<string, object>))
                                modata = JsonConvert.DeserializeObject<Dictionary<string, object>>(moddata.ToString());
                            else
                                modata = moddata as Dictionary<string, object>;
                            if (modata == null) continue;
                            var modid = Convert.ToInt32(modata["id"]);
                            var modamount = Convert.ToInt32(modata["amount"]);
                            if(itemCreated.contents == null)
                                itemCreated.contents = new ItemContainer();
                            itemCreated.contents.AddItem(ItemManager.FindItemDefinition(modid), modamount);
                        }
                    }

                    itemCreated.MoveToContainer(player.inventory.containerWear, slot);
                }
            }
        }

        [ChatCommand("loadout")]
        void cmdChatLoadout(BasePlayer player, string cmd, string[] args)
        {
            bool canCOmmand = false;
            foreach (var VARIABLE in AutorizedItemList)
            {
                if (permission.UserHasPermission(player.UserIDString, "loadout." + VARIABLE.Key))
                    canCOmmand = true;
            }
            if (!canCOmmand)
            {
                SendReply(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }

            if (args == null)
            {
                SendReply(player, "Syntax: /loadout <save|reset>");
                return;
            }

            if (args.Length != 1)
            {
                SendReply(player, "Syntax: /loadout <save|reset>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "save":
                    if (time.ContainsKey(player.UserIDString))
                    {
                        SendReply(player, lang.GetMessage("NotNow", this, player.UserIDString));
                        return;
                    }
                    var itemlist = new List<Dictionary<string, object>>();
                    var itemlistwear = new List<Dictionary<string, object>>();
                    foreach (Item item in player.inventory.containerBelt.itemList)
                    {
                        foreach (var VARIABLE in AutorizedItemList)
                        {
                            if (permission.UserHasPermission(player.UserIDString, "loadout." + VARIABLE.Key))
                            {
                                var td = VARIABLE.Value as List<object>;
                                if (td != null && td.Contains(item.info.shortname))
                                {
                                    var itemdata = new Dictionary<string, object>
                                    {
                                        {"condition", item.condition.ToString()},
                                        {"id", item.info.itemid},
                                        {"amount", stackSize.ContainsKey(item.info.shortname) && (int)stackSize[item.info.shortname] < item.amount ? stackSize[item.info.shortname] : item.amount},
                                        {"skinid", item.skin},
                                        {"slot", item.position}
                                    };
                                    var heldEnt = item.GetHeldEntity();
                                    if (heldEnt != null)
                                    {
                                        var projectiles = heldEnt.GetComponent<BaseProjectile>();
                                        if (projectiles != null)
                                        {
                                            var magazine = projectiles.primaryMagazine;
                                            if (magazine != null)
                                            {
                                                itemdata.Add("magazine",
                                                    new Dictionary<string, object>
                                                        {{magazine.ammoType.itemid.ToString(), magazine.contents}});
                                            }
                                        }
                                    }

                                    if (item.contents?.itemList != null)
                                    {
                                        var contents = new List<object>();
                                        foreach (Item item2 in item.contents.itemList)
                                        {
                                            contents.Add(new Dictionary<string, object>
                                            {
                                                {"condition", item2.condition.ToString()},
                                                {"id", item2.info.itemid},
                                                {"amount", item2.amount},
                                                {"skinid", item2.skin},
                                                {"items", new List<object>()}
                                            });
                                        }

                                        itemdata["items"] = contents;
                                    }

                                    itemlist.Add(itemdata);
                                }
                            }
                        }
                    }

                    foreach (Item item in player.inventory.containerWear.itemList)
                    {
                        foreach (var VARIABLE in AutorizedItemList)
                        {
                            if (permission.UserHasPermission(player.UserIDString, "loadout." + VARIABLE.Key))
                            {
                                var td = VARIABLE.Value as List<object>;
                                if (td != null && td.Contains(item.info.shortname))
                                {
                                    var itemdata = new Dictionary<string, object>
                                    {
                                        {"condition", item.condition.ToString()},
                                        {"id", item.info.itemid},
                                        {"amount", stackSize.ContainsKey(item.info.shortname) && (int)stackSize[item.info.shortname] < item.amount ? stackSize[item.info.shortname] : item.amount},
                                        {"skinid", item.skin},
                                        {"slot", item.position}
                                    };
                                    var heldEnt = item.GetHeldEntity();
                                    if (heldEnt != null)
                                    {
                                        var projectiles = heldEnt.GetComponent<BaseProjectile>();
                                        if (projectiles != null)
                                        {
                                            var magazine = projectiles.primaryMagazine;
                                            if (magazine != null)
                                            {
                                                itemdata.Add("magazine",
                                                    new Dictionary<string, object>
                                                        {{magazine.ammoType.itemid.ToString(), magazine.contents}});
                                            }
                                        }
                                    }

                                    if (item.contents?.itemList != null)
                                    {
                                        var contents = new List<object>();
                                        foreach (Item item2 in item.contents.itemList)
                                        {
                                            contents.Add(new Dictionary<string, object>
                                            {
                                                {"condition", item2.condition.ToString()},
                                                {"id", item2.info.itemid},
                                                {"amount", item2.amount},
                                                {"skinid", item2.skin},
                                                {"items", new List<object>()}
                                            });
                                        }

                                        itemdata["items"] = contents;
                                    }

                                    itemlistwear.Add(itemdata);
                                }
                            }
                        }
                    }

                    if (PlayerData.ContainsKey(player.UserIDString))
                        PlayerData.Remove(player.UserIDString);
                    PlayerData.Add(player.UserIDString, itemlist);
                    if (PlayerDataWear.ContainsKey(player.UserIDString))
                        PlayerDataWear.Remove(player.UserIDString);
                    PlayerDataWear.Add(player.UserIDString, itemlistwear);
                    time.Add(player.UserIDString, timer.Once(Cooldown, () => { time.Remove(player.UserIDString); }));
                    SendReply(player, lang.GetMessage("Saved", this, player.UserIDString));
                    SaveData();
                    break;
                case "reset":
                    PlayerData.Remove(player.UserIDString);
                    PlayerDataWear.Remove(player.UserIDString);
                    SendReply(player, lang.GetMessage("Reset", this, player.UserIDString));
                    break;
            }
        }
    }
}