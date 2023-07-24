﻿using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Logging;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using ImGuiScene;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using PluginLog = Dalamud.Logging.PluginLog;

namespace LazyLoot;

internal static class Roller
{
    unsafe delegate bool RollItemRaw(Loot* lootIntPtr, RollResult option, uint lootItemIndex);
    static RollItemRaw _rollItemRaw;

    static uint _itemId = 0, _index = 0;
    public static void Clear()
    {
        _itemId = _index = 0;
    }

    public static bool RollOneItem(RollResult option, ref int need, ref int greed, ref int pass)
    {
        if (!GetNextLootItem(out var index, out var loot)) return false;

        //Make option valid.
        option = ResultMerge(option, GetRestrictResult(loot), GetPlayerRestrict(loot));

        if (_itemId == loot.ItemId && index == _index)
        {
            PluginLog.Warning($"Item [{loot.ItemId}] roll {option} failed, please contract to the author or lower your delay.");
            switch (option)
            {
                case RollResult.Needed:
                    need--;
                    break;
                case RollResult.Greeded:
                    greed--;
                    break;
                default:
                    pass--;
                    break;
            }
            option = RollResult.Passed;
        }

        RollItem(option, index);
        _itemId = loot.ItemId;
        _index = index;

        switch (option)
        {
            case RollResult.Needed:
                need++;
                break;
            case RollResult.Greeded:
                greed++;
                break;
            default:
                pass++;
                break;
        }
        return true;
    }

    private static RollResult GetRestrictResult(LootItem loot)
    {
        //Checks what the max possible roll type on the item is
        var stateMax = loot.RollState switch
        {
            RollState.UpToNeed => RollResult.Needed,
            RollState.UpToGreed => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        if (LazyLoot.Config.DiagnosticsMode && loot.RollState == RollState.UpToPass)
            DuoLog.Debug($"{Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId).Name.RawString} can only be passed on. [RollState UpToPass]");

        //Checks what the player set loot rules are
        var ruleMax = loot.LootMode switch
        {
            LootMode.Normal => RollResult.Needed,
            LootMode.GreedOnly => RollResult.Greeded,
            _ => RollResult.Passed,
        };

        return ResultMerge(stateMax, ruleMax);
    }
    private unsafe static RollResult GetPlayerRestrict(LootItem loot)
    {
        var lootItem = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);
        if (lootItem == null)
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug($"Passing due to unknown item? Please give this ID to the developers: {loot.ItemId} [Unknown ID]");
            return RollResult.Passed;
        }

        if (lootItem.IsUnique && ItemCount(loot.ItemId) > 0)
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug($"{lootItem.Name.RawString} has been passed due to being unique and you already possess one. [Unique Item]");

            return RollResult.Passed;
        }

        if (IsItemUnlocked(loot.ItemId))
        {
            if (LazyLoot.Config.RestrictionIgnoreItemUnlocked)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on all items already unlocked"" enabled. [Pass All Unlocked]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreMounts && lootItem.ItemAction?.Value.Type == 1322)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Mounts"" enabled. [Pass Mounts]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreMinions && lootItem.ItemAction?.Value.Type == 853)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Minions"" enabled. [Pass Minions]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreBardings
                && lootItem.ItemAction?.Value.Type == 1013)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Bardings"" enabled. [Pass Bardings]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreEmoteHairstyle
                && lootItem.ItemAction?.Value.Type == 2633)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Emotes and Hairstyles"" enabled. [Pass Emotes/Hairstyles]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreTripleTriadCards
                && lootItem.ItemAction?.Value.Type == 3357)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Triple Triad cards"" enabled. [Pass TTCards]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreOrchestrionRolls
                && lootItem.ItemAction?.Value.Type == 25183)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Orchestrion Rolls"" enabled. [Pass Orchestrion]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionIgnoreFadedCopy
                && lootItem.Icon == 25958)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to being unlocked and you have ""Pass on unlocked Faded Copies"" enabled. [Pass Faded Copies]");

                return RollResult.Passed;
            }
        }

        if (lootItem.EquipSlotCategory.Row != 0)
        {
            // Check if the loot item level is below the average level of the user job
            if (LazyLoot.Config.RestrictionLootLowerThanJobIlvl && loot.RollState == RollState.UpToNeed)
            {
                if (lootItem.LevelItem.Row < Utils.GetPlayerIlevel() - LazyLoot.Config.RestrictionLootLowerThanJobIlvlTreshold)
                {
                    return LazyLoot.Config.RestrictionLootLowerThanJobIlvlRollState == 0 ? RollResult.Greeded : RollResult.Passed;
                }
            }

            // Check if the loot item is an actual upgrade to the user (has a higher ilvl)
            if (LazyLoot.Config.RestrictionLootIsJobUpgrade && loot.RollState == RollState.UpToNeed)
            {
                var lootItemSlot = lootItem.EquipSlotCategory.Row;
                var itemsToVerify = new List<uint>();
                InventoryContainer* equippedItems = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
                for (int i = 0; i < equippedItems->Size; i++)
                {
                    InventoryItem* equippedItem = equippedItems->GetInventorySlot(i);
                    Item equippedItemData = Svc.Data.GetExcelSheet<Item>().GetRow(equippedItem->ItemID);
                    if (equippedItemData == null) continue;
                    if (equippedItemData.EquipSlotCategory.Row != lootItemSlot) continue;
                    // We gather all the iLvls of the equipped items in the same slot (if any)
                    itemsToVerify.Add(equippedItemData.LevelItem.Row);
                }
                // And we check if from the items gathered, if the lowest is higher than the droped item, we follow the rules defined by the user
                if (itemsToVerify.Count > 0 && itemsToVerify.Min() > lootItem.LevelItem.Row)
                {
                    return LazyLoot.Config.RestrictionLootIsJobUpgradeRollState == 0 ? RollResult.Greeded : RollResult.Passed;
                }
            }

            if (LazyLoot.Config.RestrictionIgnoreItemLevelBelow
                && lootItem.LevelItem.Row < LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to having ""Pass on items with an item level below"" enabled and {lootItem.LevelItem.Row} is less than {LazyLoot.Config.RestrictionIgnoreItemLevelBelowValue}. [Pass Item Level]");

                return RollResult.Passed;
            }

            if (LazyLoot.Config.RestrictionOtherJobItems
                && loot.RollState != RollState.UpToNeed)
            {
                if (LazyLoot.Config.DiagnosticsMode)
                    DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job]");

                return RollResult.Passed;
            }
        }

        //PLD set.
        if (LazyLoot.Config.RestrictionOtherJobItems
            && lootItem.ItemAction?.Value.Type == 29153
            && !(Player.Object?.ClassJob?.Id is 1 or 19))
        {
            if (LazyLoot.Config.DiagnosticsMode)
                DuoLog.Debug($@"{lootItem.Name.RawString} has been passed due to having ""Pass on items I can't use with current job"" and this item cannot be used with your current job. [Pass Other Job (PLD Sets)]");

            return RollResult.Passed;
        }

        return RollResult.Needed;
    }

    private static RollResult ResultMerge(params RollResult[] results)
       => results.Max() switch
       {
           RollResult.Needed => RollResult.Needed,
           RollResult.Greeded => RollResult.Greeded,
           _ => RollResult.Passed,
       };


    private static unsafe bool GetNextLootItem(out uint i, out LootItem loot)
    {
        var span = Loot.Instance()->ItemArraySpan;
        for (i = 0; i < span.Length; i++)
        {
            loot = span[(int)i];
            if (loot.ChestObjectId is 0 or GameObject.InvalidGameObjectId) continue;
            if ((RollResult)loot.RollResult != RollResult.UnAwarded) continue;
            if (loot.RollState is RollState.Rolled or RollState.Unavailable or RollState.Unknown) continue;
            if (loot.ItemId == 0) continue;
            if (loot.LootMode is LootMode.LootMasterGreedOnly or LootMode.Unavailable) continue;
            if (LazyLoot.Config.RestrictionWeeklyLockoutItems)
            {
                var contentFinderInfo = Svc.Data.GetExcelSheet<ContentFinderCondition>().GetRow(GameMain.Instance()->CurrentContentFinderConditionId);
                var instanceInfo = Svc.Data.GetExcelSheet<InstanceContent>().GetRow(contentFinderInfo.Content);

                if (instanceInfo.RowId is 30133 or 30131 or 30129 or 30127) continue;

                if (instanceInfo.WeekRestriction == 1 && instanceInfo.RowId != 30125)
                {
                    var item = Svc.Data.GetExcelSheet<Item>().GetRow(loot.ItemId);

                    if (item.ItemAction?.Value.Type != 853 &&
                        item.ItemAction?.Value.Type != 1013 &&
                        item.ItemAction?.Value.Type != 2633 &&
                        item.ItemAction?.Value.Type != 3357 &&
                        item.ItemAction?.Value.Type != 25183 &&
                        item.Icon != 25958)
                    {
                        continue;
                    }
                }
            }


            return true;
        }

        loot = default;
        return false;
    }

    private static unsafe void RollItem(RollResult option, uint index)
    {
        try
        {
            _rollItemRaw ??= Marshal.GetDelegateForFunctionPointer<RollItemRaw>(Svc.SigScanner.ScanText("41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08"));
            _rollItemRaw?.Invoke(Loot.Instance(), option, index);
        }
        catch (Exception ex)
        {
            PluginLog.Warning(ex, "Warning at roll");
        }
    }

    private static unsafe int ItemCount(uint itemId)
        => InventoryManager.Instance()->GetInventoryItemCount(itemId);

    private static unsafe bool IsItemUnlocked(uint itemId)
        => UIState.Instance()->IsItemActionUnlocked(ExdModule.GetItemRowById(itemId)) == 1;
}
