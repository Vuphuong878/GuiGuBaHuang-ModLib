﻿using EBattleTypeData;
using EGameTypeData;
using MOD_nE7UL2.Const;
using MOD_nE7UL2.Enum;
using ModLib.Enum;
using ModLib.Mod;
using ModLib.Object;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MOD_nE7UL2.Mod
{
    [Cache(ModConst.MAP_BUILD_PROPERTY_EVENT)]
    public class MapBuildPropertyEvent : ModEvent
    {
        public static MapBuildPropertyEvent Instance { get; set; }

        public const float FIXING_RATE = 5.00f;
        public const int TOWN_MASTER_LUCK_ID = 420041111;
        public const int TOWN_GUARDIAN_LUCK_ID = 420041112;
        public const string TOWN_COUCIL_LUCK_DESC = "townmaster420041110desc";
        public const int BECOME_TOWN_MASTER_DRAMA = 420041113;

        public Dictionary<string, float> TaxRate { get; set; } = new Dictionary<string, float>();
        public Dictionary<string, long> Budget { get; set; } = new Dictionary<string, long>();
        public Dictionary<string, List<string>> TownMasters { get; set; } = new Dictionary<string, List<string>>();
        public List<string> PayTaxTown { get; } = new List<string>();

        public static bool isShowHirePeopleUI = false;
        public static bool isShowManageTeamUI1 = false;
        public static bool isShowManageTeamUI2 = false;

        public override void OnLoadGame()
        {
            base.OnLoadGame();

            foreach (var town in g.world.build.GetBuilds<MapBuildTown>())
            {
                //default tax rate
                if (!TaxRate.ContainsKey(town.buildData.id))
                {
                    TaxRate.Add(town.buildData.id, 1f);
                }
                //default budget
                if (!Budget.ContainsKey(town.buildData.id))
                {
                    AddBuildProperty(town, Math.Pow(2, town.gridData.areaBaseID).Parse<long>() * 300);
                }
                //town master & guardians
                if (!TownMasters.ContainsKey(town.buildData.id))
                {
                    TownMasters.Add(town.buildData.id, new List<string>());

                    var aroundWUnits = UnitHelper.GetUnitsAround(town.GetOrigiPoint(), 4, false, true).ToArray().Where(x => IsMatchCondWUnit(x)).ToList();
                    if (aroundWUnits.Count > 0)
                    {
                        var master = aroundWUnits.GetFamousWUnit();
                        Hire(town, master, TOWN_MASTER_LUCK_ID);
                    }
                }
            }
            foreach (var school in g.world.build.GetBuilds<MapBuildSchool>())
            {
                //default tax rate
                if (!TaxRate.ContainsKey(school.buildData.id))
                {
                    TaxRate.Add(school.buildData.id, 1f);
                }
            }
        }

        public override void OnOpenUIEnd(OpenUIEnd e)
        {
            base.OnOpenUIEnd(e);

            if (e.uiType.uiName == UIType.Town.uiName)
            {
                var town = g.world.playerUnit.GetMapBuild<MapBuildTown>();
                if (town != null && !PayTaxTown.Contains(town.buildData.id) && !IsTownGuardian(town, g.world.playerUnit))
                {
                    var tax = GetTax(town, g.world.playerUnit);
                    if (g.world.playerUnit.GetUnitMoney() > tax)
                    {
                        AddBuildProperty(town, tax);
                        g.world.playerUnit.AddUnitMoney(-tax);
                        PayTaxTown.Add(town.buildData.id);
                    }
                    else
                    {
                        g.ui.CloseUI(e.ui);
                        g.ui.MsgBox("Town", $"You dont have enough {tax:#,##0} Spirit Stones to pay!");
                    }
                }
            }
            else
            if (e.uiType.uiName == UIType.SkyTip.uiName)
            {
                var ui = g.ui.GetUI<UISkyTip>(UIType.SkyTip);
                if (ui.ptextTip.text == TOWN_COUCIL_LUCK_DESC)
                {
                    var a = g.ui.GetUI<UINPCInfo>(UIType.NPCInfo);
                    var b = g.ui.GetUI<UIPlayerInfo>(UIType.PlayerInfo);
                    ui.ptextTip.text = GetGuardTown(a?.unit ?? b?.unit)?.name;
                }
            }
            else
            if (e.uiType.uiName == UIType.NPCInfo.uiName)
            {
                var ui = g.ui.GetUI<UINPCInfo>(e.uiType);
                if (ui.unit.GetUnitId() != g.world.playerUnit.GetUnitId())
                {
                    var uiCover = new UICover<UINPCInfo>(e.ui);
                    if (IsTownGuardian(uiCover.UI.unit))
                    {
                        var town = GetGuardTown(uiCover.UI.unit);
                        if (IsTownGuardian(town, uiCover.UI.unit) && uiCover.UI.unit.data.unitData.relationData.GetIntim(g.world.playerUnit) < 200)
                            uiCover.AddText(0, 0, $"{GetRequiredSpiritStones(town, uiCover.UI.unit):#,##0} Spirit Stones/year").Align().Format(Color.white).Pos(uiCover.UI.uiProperty.textInTrait1.transform, 0f, 0.5f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                        uiCover.AddText(0, 0, $"Town: {GetGuardTownInfoStr(uiCover.UI.unit)}").Align().Format(Color.white).Pos(uiCover.UI.uiProperty.textInTrait1.transform, 0f, 0.25f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                        if (isShowManageTeamUI1)
                        {
                            uiCover.AddButton(0, 0, () =>
                            {
                                g.ui.MsgBox("Town", "Are you sure about dismissing this person?", MsgBoxButtonEnum.YesNo, () =>
                                {
                                    Leave(uiCover.UI.unit);
                                    g.ui.CloseUI(e.ui);
                                });
                            }, "Dismiss").Format(Color.black).Size(100, 40).Pos(uiCover.UI.uiProperty.textInTrait1.transform, -1.1f, 0.4f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                        }
                    }
                    else
                    if (isShowHirePeopleUI)
                    {
                        var town = uiCover.UI.unit.GetMapBuild<MapBuildTown>();
                        uiCover.AddText(0, 0, $"{GetRequiredSpiritStones(town, uiCover.UI.unit):#,##0} Spirit Stones/year").Align().Format(Color.white).Pos(uiCover.UI.uiProperty.textInTrait1.transform, 0f, 0.5f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                        uiCover.AddText(0, 0, $"{GetRequiredReputations(town, uiCover.UI.unit):#,##0} Reputations").Align().Format(Color.white).Pos(uiCover.UI.uiProperty.textInTrait1.transform, 0f, 0.25f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                        uiCover.AddButton(0, 0, () =>
                        {
                            var requiredSpiritStones = GetRequiredSpiritStones(town, uiCover.UI.unit);
                            if (g.world.playerUnit.GetUnitMoney() < requiredSpiritStones)
                            {
                                g.ui.MsgBox("Town", $"Require {requiredSpiritStones:#,##0} Spirit Stones");
                                return;
                            }

                            var requiredReputations = GetRequiredReputations(town, uiCover.UI.unit);
                            if (g.world.playerUnit.GetDynProperty(UnitDynPropertyEnum.Reputation).value < requiredReputations)
                            {
                                g.ui.MsgBox("Town", $"Require {requiredReputations:#,##0} Reputations");
                                return;
                            }

                            g.ui.MsgBox("Town", "Are you sure about adding this person?", MsgBoxButtonEnum.YesNo, () =>
                            {
                                Hire(town, uiCover.UI.unit, TOWN_GUARDIAN_LUCK_ID);
                                AddBuildProperty(town, -requiredSpiritStones);
                                g.ui.CloseUI(e.ui);
                            });
                        }, "Hire").Format(Color.black).Size(100, 40).Pos(uiCover.UI.uiProperty.textInTrait1.transform, -1.1f, 0.4f).SetParentTransform(uiCover.UI.uiProperty.textInTrait1.transform);
                    }
                    uiCover.UpdateUI();
                }
            }
        }

        public override void OnCloseUIStart(CloseUIStart e)
        {
            base.OnCloseUIStart(e);
            if (e.uiType.uiName == UIType.NPCSearch.uiName)
            {
                if (isShowHirePeopleUI)
                    isShowHirePeopleUI = false;
                else if (isShowManageTeamUI1)
                    isShowManageTeamUI1 = false;
                else if (isShowManageTeamUI2)
                    isShowManageTeamUI2 = false;
            }
        }

        public override void OnMonthly()
        {
            base.OnMonthly();
            //clear lst pay tax town
            PayTaxTown.Clear();
            //fix bug every month
            foreach (var t in TownMasters)
            {
                foreach (var wunitId in t.Value.ToArray())
                {
                    var wunit = g.world.unit.GetUnit(wunitId);
                    if (wunit == null || wunit.isDie || !IsTownGuardian(wunit) || IsErrorLuckId(wunit))
                    {
                        RemoveFromTownGuardians(wunitId, wunit);
                    }
                }
            }
        }

        public override void OnMonthlyForEachWUnit(WorldUnitBase wunit)
        {
            base.OnMonthlyForEachWUnit(wunit);

            var wunitId = wunit.GetUnitId();

            //fix bug every month
            if (IsTownGuardian(wunit) && TownMasters.All(x => !x.Value.Contains(wunitId)))
            {
                RemoveFromTownGuardians(wunitId, wunit);
            }

            //town tax
            var town = wunit.GetMapBuild<MapBuildTown>();
            if (town != null)
            {
                //pay town tax
                if (TownMasters[town.buildData.id].Contains(wunit.GetUnitId()))
                {
                    wunit.AddProperty<int>(UnitPropertyEnum.Hp, wunit.GetDynProperty(UnitDynPropertyEnum.HpMax).value);
                    wunit.AddProperty<int>(UnitPropertyEnum.Mp, wunit.GetDynProperty(UnitDynPropertyEnum.MpMax).value);
                    wunit.AddProperty<int>(UnitPropertyEnum.Sp, wunit.GetDynProperty(UnitDynPropertyEnum.SpMax).value);
                }
                else if (!wunit.IsPlayer())
                {
                    var tax = GetTax(town, wunit);
                    if (wunit.GetUnitMoney() > tax)
                    {
                        wunit.AddUnitMoney(-tax);
                        AddBuildProperty(town, tax);

                        //use inn
                        wunit.AddProperty<int>(UnitPropertyEnum.Hp, wunit.GetDynProperty(UnitDynPropertyEnum.HpMax).value);
                        wunit.AddProperty<int>(UnitPropertyEnum.Mp, wunit.GetDynProperty(UnitDynPropertyEnum.MpMax).value);
                        wunit.AddProperty<int>(UnitPropertyEnum.Sp, wunit.GetDynProperty(UnitDynPropertyEnum.SpMax).value);
                    }
                    else
                    {
                        //get out
                        wunit.data.unitData.relationData.AddHate(GetTownMaster(town).GetUnitId(), 1);
                        wunit.SetUnitRandomPos(wunit.GetUnitPos());
                    }
                }
            }

            //school tax
            var school = wunit.GetMapBuild<MapBuildSchool>();
            if (school != null && !IsSchoolMember(school, wunit))
            {
                //pay school tax
                var tax = GetTax(school, wunit);
                wunit.AddUnitMoney(-tax);
                school.buildData.money += tax;
            }
        }

        public override void OnYearly()
        {
            base.OnYearly();

            foreach (var town in g.world.build.GetBuilds<MapBuildTown>())
            {
                if (!TownMasters.ContainsKey(town.buildData.id))
                    TownMasters.Add(town.buildData.id, new List<string>());
                var townMasterData = TownMasters[town.buildData.id];

                //choose new town master
                var townCouncilWUnits = GetTownGuardians(town);
                var master = GetTownMaster(town);
                if (master == null || master.isDie)
                {
                    master = townCouncilWUnits.GetFamousWUnit();
                    var masterId = master.GetUnitId();
                    master.DelLuck(TOWN_GUARDIAN_LUCK_ID);
                    master.AddLuck(TOWN_MASTER_LUCK_ID);

                    if (master.IsPlayer())
                        DramaTool.OpenDrama(BECOME_TOWN_MASTER_DRAMA);
                }

                if (master.GetUnitId() != g.world.playerUnit.GetUnitId())
                {
                    //random tax rate
                    TaxRate[town.buildData.id] = CommonTool.Random(0.70f, 4.00f);

                    //hire more people
                    if (GetBuildProperty(town) > GetRequiredSpiritStones(town, master.GetGradeLvl()))
                    {
                        var aroundWUnits = UnitHelper.GetUnitsAround(town.GetOrigiPoint(), 4, false, true).ToArray().Where(x => IsMatchCondWUnit(x)).ToList();
                        if (aroundWUnits.Count > 0)
                        {
                            var newGuard = aroundWUnits.GetFamousWUnit();
                            if (newGuard.IsPlayer())
                            {
                                g.ui.MsgBox("Town", $"You received a invitation from {town.name}' Town Master for guardian position?{Environment.NewLine}Do you wanna become a Town Guardian?", MsgBoxButtonEnum.YesNo, () =>
                                {
                                    Hire(town, newGuard, TOWN_GUARDIAN_LUCK_ID);
                                });
                            }
                            else
                            {
                                Hire(town, newGuard, TOWN_GUARDIAN_LUCK_ID);
                            }
                        }
                    }
                }

                //budget inc yearly
                AddBuildProperty(town, Math.Pow(2, town.gridData.areaBaseID).Parse<long>() * 200);
                
                //budget inc from auction
                var auction = town.GetBuildSub<MapBuildTownAuction>();
                if (auction != null)
                {
                    AddBuildProperty(town, Math.Pow(2, town.gridData.areaBaseID).Parse<long>() * 150);
                }

                //guardians
                foreach (var wunit in townCouncilWUnits)
                {
                    if (wunit != null && !wunit.isDie)
                    {
                        if (IsTownMaster(wunit))
                            continue;

                        //payment
                        var profit = GetRequiredSpiritStones(town, wunit);
                        if (Budget[town.buildData.id] > profit)
                        {
                            AddBuildProperty(town, -profit);
                            wunit.AddUnitMoney(profit);
                        }
                        else
                        {
                            Leave(wunit);
                            wunit.data.unitData.relationData.AddHate(GetTownMaster(town).GetUnitId(), 100);
                        }

                        if (wunit.IsPlayer())
                            continue;
                        //come back the town
                        wunit.SetUnitPos(town.GetOrigiPoint());
                    }
                    else
                    {
                        RemoveFromTownGuardians(wunit?.GetUnitId(), wunit);
                    }
                }

                //master
                if (!master.IsPlayer())
                {
                    var profit = GetRequiredSpiritStones(town, master) + (GetBuildProperty(town) / 100).FixValue(0, int.MaxValue).Parse<int>();
                    AddBuildProperty(town, -profit);
                    master.AddUnitMoney(profit);
                    master.SetUnitPos(town.GetOrigiPoint());
                }
            }

            foreach (var school in g.world.build.GetBuilds<MapBuildSchool>())
            {
                //school budget inc yearly
                school.buildData.money += Math.Pow(2, school.gridData.areaBaseID).Parse<long>() * 300;
            }
        }

        public override void OnBattleUnitDie(UnitDie e)
        {
            base.OnBattleUnitDie(e);

            if (g.world.battle.data.isRealBattle &&
                e?.hitData?.attackUnit != null && e.hitData.attackUnit.IsWorldUnit() &&
                e?.unit != null && e.unit.IsWorldUnit())
            {
                var killer = e.hitData.attackUnit.GetWorldUnit();
                var killerId = killer.GetUnitId();

                var town = killer.GetMapBuild<MapBuildTown>();
                if (town != null && TownMasters.ContainsKey(town.buildData.id))
                {
                    if (TownMasters[town.buildData.id].Contains(killerId))
                    {
                        //blamed, power-hara
                    }
                    else
                    {
                        foreach (var councilWUnit in TownMasters[town.buildData.id].Select(x => g.world.unit.GetUnit(x)))
                        {
                            if (councilWUnit != null)
                                councilWUnit.data.unitData.relationData.AddHate(killerId, 50f);
                        }
                    }
                }

                var dieUnit = e.unit.GetWorldUnit();
                var dieUnitId = dieUnit.GetUnitId();
                if (TownMasters.Any(x => x.Value.Contains(dieUnitId)))
                {
                    var dieUnitCouncil = TownMasters.First(x => x.Value.Contains(dieUnitId));
                    RemoveFromTownGuardians(dieUnitId, dieUnit);
                    foreach (var councilWUnit in dieUnitCouncil.Value.Select(x => g.world.unit.GetUnit(x)))
                    {
                        if (councilWUnit != null && killerId != councilWUnit.GetUnitId())
                            councilWUnit.data.unitData.relationData.AddHate(killerId, 200f);
                    }
                }
            }
        }

        public static void Hire(MapBuildTown town, WorldUnitBase member, int luckId)
        {
            Instance.TownMasters[town.buildData.id].Add(member.GetUnitId());
            member.AddLuck(luckId);
        }

        public static int GetBaseTax(MapBuildBase buildbase)
        {
            //default tax rate
            if (!Instance.TaxRate.ContainsKey(buildbase.buildData.id))
                Instance.TaxRate[buildbase.buildData.id] = 1f;
            //base on area
            return SMLocalConfigsEvent.Instance.Calculate(Convert.ToInt32(InflationaryEvent.CalculateInflationary((
                Math.Pow(2, buildbase.gridData.areaBaseID) * FIXING_RATE * Instance.TaxRate[buildbase.buildData.id]
            ).Parse<int>())), SMLocalConfigsEvent.Instance.Configs.AddTaxRate).Parse<int>();
        }

        public static int GetTax(MapBuildBase buildbase, WorldUnitBase wunit)
        {
            if (wunit == null || wunit.isDie)
                return 0;
            var tax = GetBaseTax(buildbase);
            var school = buildbase.TryCast<MapBuildSchool>();
            if (school != null)
            {
                //if school -> *effect-count
                tax *= school.schoolData.allEffects.Count;
            }
            var town = buildbase.TryCast<MapBuildTown>();
            if (town != null)
            {
                //if town -> double
                if (town.buildTownData.isMainTown)
                    tax *= 2;
                //add alternative tax rate
                tax = (tax * Instance.TaxRate[town.buildData.id]).Parse<int>();
            }
            //if merchant, add more tax
            if (UnitTypeEvent.GetUnitTypeEnum(wunit) == UnitTypeEnum.Merchant)
            {
                tax *= (1.00f + UnitTypeLuckEnum.Merchant.CustomEffects[ModConst.UTYPE_LUCK_EFX_SELL_VALUE].Value0.Parse<float>()).Parse<int>();
            }
            tax *= (1.00f + MerchantLuckEnum.Merchant.GetCurLevel(wunit) * MerchantLuckEnum.Merchant.IncSellValueEachLvl).Parse<int>();
            return tax;
        }

        public static long GetBuildProperty(MapBuildBase build)
        {
            var town = build.TryCast<MapBuildTown>();
            if (town != null)
            {
                if (!Instance.Budget.ContainsKey(town.buildData.id))
                    Instance.Budget.Add(town.buildData.id, 0);
                return Instance.Budget[town.buildData.id];
            }

            var school = build.TryCast<MapBuildSchool>();
            if (school != null)
            {
                return school.buildData.money;
            }

            return 0;
        }

        public static void AddBuildProperty(MapBuildBase build, long add)
        {
            var town = build.TryCast<MapBuildTown>();
            if (town != null)
            {
                if (!Instance.Budget.ContainsKey(town.buildData.id))
                    Instance.Budget.Add(town.buildData.id, 0);
                Instance.Budget[town.buildData.id] += add;
                return;
            }

            var school = build.TryCast<MapBuildSchool>();
            if (school != null)
            {
                school.buildData.money += add;
                return;
            }
        }

        public static bool IsErrorLuckId(WorldUnitBase wunit)
        {
            return wunit.GetLuck(TOWN_GUARDIAN_LUCK_ID) != null && wunit.GetLuck(TOWN_MASTER_LUCK_ID) != null;
        }

        public static bool IsTownGuardian(WorldUnitBase wunit)
        {
            return wunit != null && (wunit.GetLuck(TOWN_GUARDIAN_LUCK_ID) != null || wunit.GetLuck(TOWN_MASTER_LUCK_ID) != null);
        }

        public static bool IsTownMaster(WorldUnitBase wunit)
        {
            return wunit != null && wunit.GetLuck(TOWN_MASTER_LUCK_ID) != null;
        }

        public static bool IsTownGuardian(MapBuildTown town, WorldUnitBase wunit)
        {
            if (town == null || wunit == null)
                return false;
            return Instance.TownMasters[town.buildData.id].Contains(wunit.GetUnitId());
        }

        public static bool IsTownMaster(MapBuildTown town, WorldUnitBase wunit)
        {
            return IsTownGuardian(town, wunit) && IsTownMaster(wunit);
        }

        public static bool IsSchoolMember(WorldUnitBase wunit)
        {
            return wunit.data.school?.schoolNameID != null;
        }

        public static bool IsSchoolMember(MapBuildSchool school, WorldUnitBase wunit)
        {
            if (school == null)
                return false;
            return school.schoolNameID == wunit.data.school?.schoolNameID;
        }

        public static long GetUpgrade2CityCost(MapBuildTown town)
        {
            return (Math.Pow(3, town.gridData.areaBaseID) * 1000000).Parse<long>();
        }

        public static void Upgrade2City(MapBuildTown town)
        {
            var budget = Instance.Budget[town.buildData.id];
            var cost = GetUpgrade2CityCost(town);
            if (budget > cost)
            {
                town.buildTownData.isMainTown = true;
                Instance.Budget[town.buildData.id] -= cost;
            }
            else
            {
                g.ui.MsgBox("Town", "Town's Budget is not enough!");
            }
        }

        public static void Deposit()
        {
            var uiPropSelectCount = g.ui.OpenUI<UIPropSelectCount>(UIType.PropSelectCount);
            uiPropSelectCount.minCount = 0;
            uiPropSelectCount.maxCount = g.world.playerUnit.GetUnitMoney();
            uiPropSelectCount.oneCost = 1;
            uiPropSelectCount.btnOK.onClick.RemoveAllListeners();
            uiPropSelectCount.btnOK.onClick.AddListener((UnityAction)(() =>
            {
                var town = g.world.playerUnit.GetMapBuild<MapBuildTown>();
                AddBuildProperty(town, uiPropSelectCount.curSelectCount);
                g.world.playerUnit.AddUnitMoney(-uiPropSelectCount.curSelectCount);
                g.ui.CloseUI(uiPropSelectCount);
                UIHelper.UpdateAllUI();
            }));
            uiPropSelectCount.textGrade.text = string.Empty;
            uiPropSelectCount.textName.text = string.Empty;
            uiPropSelectCount.ptextInfo.text = string.Empty;
            uiPropSelectCount.textTitle.text = $"Deposit";
        }

        public static void Withdraw()
        {
            var town = g.world.playerUnit.GetMapBuild<MapBuildTown>();
            var uiPropSelectCount = g.ui.OpenUI<UIPropSelectCount>(UIType.PropSelectCount);
            uiPropSelectCount.minCount = 0;
            uiPropSelectCount.maxCount = Instance.Budget[town.buildData.id].FixValue(0, int.MaxValue).Parse<int>();
            uiPropSelectCount.oneCost = 1;
            uiPropSelectCount.btnOK.onClick.RemoveAllListeners();
            uiPropSelectCount.btnOK.onClick.AddListener((UnityAction)(() =>
            {
                AddBuildProperty(town, -uiPropSelectCount.curSelectCount);
                g.world.playerUnit.AddUnitMoney(uiPropSelectCount.curSelectCount);
                g.ui.CloseUI(uiPropSelectCount);
                UIHelper.UpdateAllUI();
            }));
            uiPropSelectCount.textGrade.text = string.Empty;
            uiPropSelectCount.textName.text = string.Empty;
            uiPropSelectCount.ptextInfo.text = string.Empty;
            uiPropSelectCount.textTitle.text = $"Withdraw";
        }

        public static void OpenUIHirePeople()
        {
            var ui = g.ui.OpenUI<UINPCSearch>(UIType.NPCSearch);
            ui.InitData(new Vector2Int(0, 0));
            ui.units = GetHirablePeople().ToIl2CppList();
            ui.UpdateUI();
            isShowHirePeopleUI = true;

            var town = GetGuardTown(g.world.playerUnit);
            UIHelper.GetUICustomBase(UIType.NPCSearch).Dispose();

            var i = -1;
            var uiCover = new UICover<UINPCSearch>(ui);
            {
                var col = uiCover.MidCol - 11;
                var row = uiCover.MidRow;
                uiCover.AddText(col, row + i++, $"Town: {town.name}　／　Master: {GetTownMaster(town).data.unitData.propertyData.GetName()}").Format().Align();
                uiCover.AddText(col, row + i++, "Budget: {0}").Format().Align().SetWork(new UIItemWork
                {
                    Formatter = (ibase) => new object[] { GetBuildProperty(town).ToString("#,##0") },
                });
                uiCover.AddText(col, row + i++, "Payment: {0}/year").Format().Align().SetWork(new UIItemWork
                {
                    Formatter = (ibase) => new object[] { GetTotalMonthlyPayment(town).ToString("#,##0") },
                });
            }
            uiCover.UpdateUI();
        }

        public static void OpenUITownManage(MapBuildTown town)
        {
            var ui = g.ui.OpenUI<UINPCSearch>(UIType.NPCSearch);
            ui.InitData(new Vector2Int(0, 0));
            ui.units = GetTownGuardians(town).ToIl2CppList();
            ui.UpdateUI();
            isShowManageTeamUI1 = true;

            var i = -1;
            var uiCover = new UICover<UINPCSearch>(ui);
            {
                var col = uiCover.MidCol - 11;
                var row = uiCover.MidRow;
                uiCover.AddText(col, row + i++, $"Town: {town.name}　／　Master: {GetTownMaster(town).data.unitData.propertyData.GetName()}").Format().Align();
                uiCover.AddText(col, row + i++, "Budget: {0}").Format().Align().SetWork(new UIItemWork
                {
                    Formatter = (ibase) => new object[] { GetBuildProperty(town).ToString("#,##0") },
                });
                uiCover.AddText(col, row + i++, "Payment: {0}/year").Format().Align().SetWork(new UIItemWork
                {
                    Formatter = (ibase) => new object[] { GetTotalMonthlyPayment(town).ToString("#,##0") },
                });
                uiCover.AddCompositeSlider(col, row + i++, $"Base Tax:", 0.50f, 10.00f, Instance.TaxRate[town.buildData.id], "{0}/month").SetWork(new UIItemWork
                {
                    Formatter = (ibase) => new object[] { GetBaseTax(town) },
                    ChangeAct = (ibase, value) => Instance.TaxRate[town.buildData.id] = value.Parse<float>(),
                });

                i++;
                var c = 0;
                foreach (var em in BuildingCostEnum.GetAllEnums<BuildingCostEnum>())
                {
                    if (BuildingArrangeEvent.IsBuildable(town, em))
                    {
                        var cost = BuildingArrangeEvent.GetBuildingCost(town, em);
                        uiCover.AddText(col + (c % 2 * 11), uiCover.MidRow + i + (c / 2 * 2), $"{GameTool.LS(em.BuildingName)}　／　Cost: {cost:#,##0}").Format().Align();
                        uiCover.AddButton(col - 3 + (c % 2 * 11), uiCover.MidRow + i + (c / 2 * 2), () =>
                        {
                            if (GetBuildProperty(town) > cost)
                            {
                                BuildingArrangeEvent.Build(town, em);
                                g.ui.CloseUI(ui);
                                g.ui.MsgBox("Town", $"Built {GameTool.LS(em.BuildingName)}!");
                            }
                            else
                            {
                                g.ui.MsgBox("Town", $"You cant build this building with current budget!{Environment.NewLine}{GetBuildProperty(town):#,##0}");
                            }
                        }, "Build").Format().Align(TextAnchor.MiddleCenter);
                        c++;
                    }
                }

                if (IsTownMaster(town, g.world.playerUnit) && !town.buildTownData.isMainTown)
                    uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 20, () => Upgrade2City(town), $"Upgrade to City{Environment.NewLine}{GetUpgrade2CityCost(town):#,##0}").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
                uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 17, Deposit, "Deposit").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
                uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 14, Withdraw, "Withdraw").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
                uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 11, OpenUIHirePeople, "Hire People").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
                uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 8, () =>
                {
                    g.ui.MsgBox("Town", "Are you sure about leaving?", MsgBoxButtonEnum.YesNo, () =>
                    {
                        Leave(g.world.playerUnit);
                        g.ui.CloseUI(UIType.NPCSearch);
                        g.ui.CloseUI(UIType.Town);
                    });
                }, "Dismiss").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
            }
            uiCover.UpdateUI();
        }

        public static void OpenUITownGuardians(MapBuildTown town)
        {
            var ui = g.ui.OpenUI<UINPCSearch>(UIType.NPCSearch);
            ui.InitData(new Vector2Int(0, 0));
            ui.units = GetTownGuardians(town).ToIl2CppList();
            ui.UpdateUI();
            isShowManageTeamUI2 = true;

            if (IsTownMaster(g.world.playerUnit) && !IsTownGuardian(town, g.world.playerUnit))
            {
                var uiCover = new UICover<UINPCSearch>(ui);
                {
                    uiCover.AddButton(uiCover.LastCol - 10, uiCover.LastRow - 8, () =>
                    {
                        g.ui.MsgBox("Town", "Are you sure about declaring war with this town?", MsgBoxButtonEnum.YesNo, () =>
                        {
                            MapBuildBattleEvent.TownWar(town, GetGuardTown(g.world.playerUnit), true);
                        });
                    }, "Declare War").Format(Color.black, 17).Align(TextAnchor.MiddleCenter).Size(300, 64);
                }
                uiCover.UpdateUI();
            }
        }

        public static bool IsMatchCondWUnit(WorldUnitBase wunit)
        {
            return !IsTownGuardian(wunit) && !HirePeopleEvent.IsHired(wunit);
        }

        public static List<WorldUnitBase> GetHirablePeople()
        {
            var rs = new List<WorldUnitBase>();
            var town = g.world.playerUnit.GetMapBuild<MapBuildTown>();
            foreach (var wunit in g.world.playerUnit.GetUnitsAround(4, false, false))
            {
                if (IsMatchCondWUnit(wunit) && wunit.GetGradeLvl() < g.world.playerUnit.GetGradeLvl() + 2 &&
                    wunit.GetMapBuild<MapBuildTown>()?.buildData.id == town.buildData.id &&
                    !rs.Any((x) => x.GetUnitId() == wunit.GetUnitId()))
                {
                    rs.Add(wunit);
                }
            }
            return rs;
        }

        public static List<WorldUnitBase> GetTownGuardians(MapBuildTown town)
        {
            var rs = new List<WorldUnitBase>();
            if (town == null || !Instance.TownMasters.ContainsKey(town.buildData.id))
                return rs;
            foreach (var wunitId in Instance.TownMasters[town.buildData.id])
            {
                var wunit = g.world.unit.GetUnit(wunitId);
                if (wunit == null)
                    continue;
                rs.Add(wunit);
            }
            return rs;
        }

        public static WorldUnitBase GetTownMaster(MapBuildTown town)
        {
            if (town == null || !Instance.TownMasters.ContainsKey(town.buildData.id))
                return null;
            foreach (var wunitId in Instance.TownMasters[town.buildData.id])
            {
                var wunit = g.world.unit.GetUnit(wunitId);
                if (wunit == null)
                    continue;
                if (IsTownMaster(wunit))
                    return wunit;
            }
            return null;
        }

        public static MapBuildTown GetGuardTown(WorldUnitBase wunit)
        {
            var wunitId = wunit.GetUnitId();
            if (Instance.TownMasters.Any(x => x.Value.Contains(wunitId)))
            {
                var townId = Instance.TownMasters.First(x => x.Value.Contains(wunitId)).Key;
                return g.world.build.GetBuild<MapBuildTown>(townId);
            }
            return null;
        }

        public static string GetGuardTownInfoStr(WorldUnitBase wunit)
        {
            return GetGuardTown(wunit)?.name;
        }

        public static void RemoveFromTownGuardians(string wunitId, WorldUnitBase wunit)
        {
            if (!string.IsNullOrEmpty(wunitId))
            {
                foreach (var data in Instance.TownMasters)
                {
                    if (data.Value.Contains(wunitId))
                    {
                        data.Value.RemoveAll(x => x == wunitId);
                        break;
                    }
                }
            }
            wunit?.DelLuck(TOWN_MASTER_LUCK_ID);
            wunit?.DelLuck(TOWN_GUARDIAN_LUCK_ID);
        }

        public static void Leave(WorldUnitBase wunit)
        {
            if (wunit == null)
                return;
            RemoveFromTownGuardians(wunit.GetUnitId(), wunit);
        }

        public static int GetTotalMonthlyPayment(MapBuildTown town)
        {
            var rs = 0;
            foreach (var guardian in GetTownGuardians(town))
            {
                if (!IsTownMaster(guardian))
                    rs += GetRequiredSpiritStones(town, guardian);
            }
            return rs;
        }

        public static int GetRequiredSpiritStones(MapBuildTown town, int gradeLvl)
        {
            var k = (Instance.TownMasters.ContainsKey(town.buildData.id) ? Instance.TownMasters[town.buildData.id].Count : 1) + 1;
            return (Math.Pow(3, gradeLvl) * 1000 * k).Parse<int>();
        }

        public static int GetRequiredSpiritStones(MapBuildTown town, WorldUnitBase wunit)
        {
            return GetRequiredSpiritStones(town, wunit.GetGradeLvl());
        }

        public static int GetRequiredReputations(MapBuildTown town, WorldUnitBase wunit)
        {
            var k = (Instance.TownMasters.ContainsKey(town.buildData.id) ? Instance.TownMasters[town.buildData.id].Count : 1) + 1;
            return (Math.Pow(2, k) * 100 + Math.Pow(3, wunit.GetGradeLvl()) * 10).Parse<int>();
        }
    }
}
