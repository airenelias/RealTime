// <copyright file="BuildingAIPatch.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.Patches
{
    using System;
    using System.Linq;
    using System.Reflection;
    using ColossalFramework;
    using ColossalFramework.Math;
    using ColossalFramework.UI;
    using HarmonyLib;
    using ICities;
    using RealTime.Core;
    using RealTime.CustomAI;
    using RealTime.Simulation;
    using UnityEngine;

    /// <summary>
    /// A static class that provides the patch objects for the building AI game methods.
    /// </summary>
    ///
    [HarmonyPatch]
    internal static class BuildingAIPatch
    {
        /// <summary>Gets or sets the custom AI object for buildings.</summary>
        public static RealTimeBuildingAI RealTimeAI { get; set; }

        /// <summary>Gets or sets the weather information service.</summary>
        public static IWeatherInfo WeatherInfo { get; set; }

        [HarmonyPatch]
        private sealed class CommercialBuildingAI_SimulationStepActive
        {
            [HarmonyPatch(typeof(CommercialBuildingAI), "SimulationStepActive")]
            [HarmonyPrefix]
            private static bool Prefix(ref Building buildingData, ref byte __state)
            {
                __state = buildingData.m_outgoingProblemTimer;
                if (buildingData.m_customBuffer2 > 0)
                {
                    // Simulate some goods become spoiled; additionally, this will cause the buildings to never reach the 'stock full' state.
                    // In that state, no visits are possible anymore, so the building gets stuck
                    --buildingData.m_customBuffer2;
                }

                return true;
            }

            [HarmonyPatch(typeof(CommercialBuildingAI), "SimulationStepActive")]
            [HarmonyPostfix]
            private static void Postfix(CommercialBuildingAI __instance, ushort buildingID, ref Building buildingData, byte __state, ref int ___totalCount)
            {
                if (__state != buildingData.m_outgoingProblemTimer)
                {
                    RealTimeAI.ProcessBuildingProblems(buildingID, __state);
                }
                if(buildingData.Info.m_class.m_service == ItemClass.Service.Commercial && buildingData.Info.m_class.m_subService == ItemClass.SubService.CommercialTourist)
                {
                    buildingData.m_roomUsed = (ushort)___totalCount;
                    buildingData.m_roomMax = (ushort)__instance.CalculateVisitplaceCount(buildingData.Info.m_class.m_level, new Randomizer(buildingID), buildingData.Width, buildingData.Length);
                }
            }
        }

        [HarmonyPatch(typeof(ZonedBuildingWorldInfoPanel), "UpdateBindings")]
        public static class ZonedBuildingWorldInfoPanelPatch
        {
            private static UILabel s_hotelLabel;

            public static void Postfix()
            {
                // Currently selected building.
                ushort building = WorldInfoPanel.GetCurrentInstanceID().Building;

                // Create hotel label if it isn't already set up.
                if (s_hotelLabel == null)
                {
                    // Get info panel.
                    var infoPanel = UIView.library.Get<ZonedBuildingWorldInfoPanel>(typeof(ZonedBuildingWorldInfoPanel).Name);

                    // Add current visitor count label.
                    s_hotelLabel = AddLabel(infoPanel.component, 65f, 280f, "Rooms Ocuppied", textScale: 0.75f);
                    s_hotelLabel.textColor = new Color32(185, 221, 254, 255);
                    s_hotelLabel.font = Resources.FindObjectsOfTypeAll<UIFont>().FirstOrDefault((UIFont f) => f.name == "OpenSans-Regular");

                    // Position under existing Highly Educated workers count row in line with total workplace count label.
                    var situationLabel = infoPanel.Find("WorkSituation");
                    var workerLabel = infoPanel.Find("HighlyEducatedWorkers");
                    if (situationLabel != null && workerLabel != null)
                    {
                        s_hotelLabel.absolutePosition = new Vector2(situationLabel.absolutePosition.x, workerLabel.absolutePosition.y + 25f);
                    }
                    else
                    {
                        Debug.Log("couldn't find ZonedBuildingWorldInfoPanel components");
                    }
                }

                // Local references.
                var buildingBuffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                var buildingData = buildingBuffer[building];
                var buildingInfo = buildingData.Info;

                // Is this a hotel building?
                if (buildingInfo.GetAI() is CommercialBuildingAI && buildingInfo.m_class.m_service == ItemClass.Service.Commercial && buildingInfo.m_class.m_subService == ItemClass.SubService.CommercialTourist)
                {
                    // Hotel show the label
                    s_hotelLabel.Show();

                    // Display hotel rooms ocuppied count out of max hotel rooms.
                    s_hotelLabel.text = buildingData.m_roomUsed + " / " + buildingData.m_roomMax;

                }
                else
                {
                    // Not a hotel hide the label
                    s_hotelLabel.Hide();
                }
            }

            private static UILabel AddLabel(UIComponent parent, float xPos, float yPos, string text, float width = -1f, float textScale = 1.0f, UIHorizontalAlignment alignment = UIHorizontalAlignment.Left)
            {
                // Add label.
                var label = parent.AddUIComponent<UILabel>();

                // Set sizing options.
                if (width > 0f)
                {
                    // Fixed width.
                    label.autoSize = false;
                    label.width = width;
                    label.autoHeight = true;
                    label.wordWrap = true;
                }
                else
                {
                    // Autosize.
                    label.autoSize = true;
                    label.autoHeight = false;
                    label.wordWrap = false;
                }

                // Alignment.
                label.textAlignment = alignment;

                // Text.
                label.textScale = textScale;
                label.text = text;

                // Position (aligned to right if text alignment is set to right).
                label.relativePosition = new Vector2(alignment == UIHorizontalAlignment.Right ? xPos - label.width : xPos, yPos);

                return label;
            }
        }

        [HarmonyPatch]
        private sealed class MarketAI_SimulationStep
        {
            [HarmonyPatch(typeof(MarketAI), "SimulationStep")]
            [HarmonyPrefix]
            private static bool Prefix(ref Building buildingData, ref byte __state)
            {
                __state = buildingData.m_outgoingProblemTimer;
                if (buildingData.m_customBuffer2 > 0)
                {
                    // Simulate some goods become spoiled; additionally, this will cause the buildings to never reach the 'stock full' state.
                    // In that state, no visits are possible anymore, so the building gets stuck
                    --buildingData.m_customBuffer2;
                }

                return true;
            }

            [HarmonyPatch(typeof(MarketAI), "SimulationStep")]
            [HarmonyPostfix]
            private static void Postfix(ushort buildingID, ref Building buildingData, byte __state)
            {
                if (__state != buildingData.m_outgoingProblemTimer)
                {
                    RealTimeAI.ProcessBuildingProblems(buildingID, __state);
                }
            }
        }

        [HarmonyPatch]
        private sealed class PrivateBuildingAI_HandleWorkers
        {
            [HarmonyPatch(typeof(PrivateBuildingAI), "HandleWorkers")]
            [HarmonyPrefix]
            private static bool Prefix(ref Building buildingData, ref byte __state)
            {
                __state = buildingData.m_workerProblemTimer;
                return true;
            }

            [HarmonyPatch(typeof(PrivateBuildingAI), "HandleWorkers")]
            [HarmonyPostfix]
            private static void Postfix(ushort buildingID, ref Building buildingData, byte __state)
            {
                if (__state != buildingData.m_workerProblemTimer)
                {
                    RealTimeAI.ProcessWorkerProblems(buildingID, __state);
                }
            }
        }

        [HarmonyPatch]
        private sealed class PrivateBuildingAI_GetConstructionTime
        {
            [HarmonyPatch(typeof(PrivateBuildingAI), "GetConstructionTime")]
            [HarmonyPrefix]
            private static bool Prefix(ref int __result)
            {
                __result = RealTimeAI.GetConstructionTime();
                return false;
            }
        }

        [HarmonyPatch]
        private sealed class BuildingAI_CalculateUnspawnPosition
        {
            [HarmonyPatch(typeof(BuildingAI), "CalculateUnspawnPosition",
                new Type[] { typeof(ushort), typeof(Building), typeof(Randomizer), typeof(CitizenInfo), typeof(ushort), typeof(Vector3), typeof(Vector3), typeof(Vector2), typeof(CitizenInstance.Flags) },
                new ArgumentType[] { ArgumentType.Normal, ArgumentType.Ref, ArgumentType.Ref, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out, ArgumentType.Out })]
            [HarmonyPostfix]
            private static void Postfix(BuildingAI __instance, ushort buildingID, ref Building data, ref Randomizer randomizer, CitizenInfo info, ref Vector3 position, ref Vector3 target, ref CitizenInstance.Flags specialFlags)
            {
                if (!WeatherInfo.IsBadWeather || data.Info == null || data.Info.m_enterDoors == null)
                {
                    return;
                }

                var enterDoors = data.Info.m_enterDoors;
                bool doorFound = false;
                for (int i = 0; i < enterDoors.Length; ++i)
                {
                    var prop = enterDoors[i].m_finalProp;
                    if (prop == null)
                    {
                        continue;
                    }

                    if (prop.m_doorType == PropInfo.DoorType.Enter || prop.m_doorType == PropInfo.DoorType.Both)
                    {
                        doorFound = true;
                        break;
                    }
                }

                if (!doorFound)
                {
                    return;
                }

                __instance.CalculateSpawnPosition(buildingID, ref data, ref randomizer, info, out var spawnPosition, out var spawnTarget);

                position = spawnPosition;
                target = spawnTarget;
                specialFlags &= ~(CitizenInstance.Flags.HangAround | CitizenInstance.Flags.SittingDown);
            }
        }

        [HarmonyPatch]
        private sealed class PrivateBuildingAI_GetUpgradeInfo
        {
            [HarmonyPatch(typeof(PrivateBuildingAI), "GetUpgradeInfo")]
            [HarmonyPrefix]
            private static bool Prefix(ref BuildingInfo __result, ushort buildingID, ref Building data)
            {
                if (!RealTimeCore.ApplyBuildingPatch)
                {
                    return true;
                }

                if ((data.m_flags & Building.Flags.Upgrading) != 0)
                {
                    return true;
                }

                if (!RealTimeAI.CanBuildOrUpgrade(data.Info.GetService(), buildingID))
                {
                    __result = null;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch]
        private sealed class BuildingManager_CreateBuilding
        {
            [HarmonyPatch(typeof(BuildingManager), "CreateBuilding")]
            [HarmonyPrefix]
            private static bool Prefix(BuildingInfo info, ref bool __result)
            {
                if (!RealTimeCore.ApplyBuildingPatch)
                {
                    return true;
                }

                if (!RealTimeAI.CanBuildOrUpgrade(info.GetService()))
                {
                    __result = false;
                    return false;
                }

                return true;
            }

            [HarmonyPatch(typeof(BuildingManager), "CreateBuilding")]
            [HarmonyPostfix]
            private static void Postfix(bool __result, ref ushort building, BuildingInfo info)
            {
                if (!RealTimeCore.ApplyBuildingPatch)
                {
                    return;
                }

                if (__result)
                {
                    RealTimeAI.RegisterConstructingBuilding(building, info.GetService());
                }
            }
        }

        [HarmonyPatch]
        private sealed class PlayerBuildingAI_ProduceGoods
        {
            [HarmonyPatch(typeof(PlayerBuildingAI), "ProduceGoods")]
            [HarmonyPostfix]
            private static void Postfix(ushort buildingID, ref Building buildingData)
            {
                if ((buildingData.m_flags & Building.Flags.Active) != 0
                    && RealTimeAI.ShouldSwitchBuildingLightsOff(buildingID))
                {
                    buildingData.m_flags &= ~Building.Flags.Active;
                }
            }
        }

        [HarmonyPatch]
        private sealed class CommonBuildingAI_GetColor
        {
            [HarmonyPatch(typeof(CommonBuildingAI), "GetColor")]
            [HarmonyPostfix]
            private static void Postfix(ushort buildingID, InfoManager.InfoMode infoMode, ref Color __result)
            {
                var negativeColor = InfoManager.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.TrafficRoutes].m_negativeColor;
                var targetColor = InfoManager.instance.m_properties.m_modeProperties[(int)InfoManager.InfoMode.TrafficRoutes].m_targetColor;
                switch (infoMode)
                {
                    case InfoManager.InfoMode.TrafficRoutes:
                        __result = Color.Lerp(negativeColor, targetColor, RealTimeAI.GetBuildingReachingTroubleFactor(buildingID));
                        return;

                    case InfoManager.InfoMode.None:
                        if (RealTimeAI.ShouldSwitchBuildingLightsOff(buildingID))
                        {
                            __result.a = 0;
                        }

                        return;
                }
            }
        }

        [HarmonyPatch]
        private sealed class CommonBuildingAI_BuildingDeactivated
        {
            private delegate void EmptyBuildingDelegate(CommonBuildingAI __instance, ushort buildingID, ref Building data, CitizenUnit.Flags flags, bool onlyMoving);
            private static readonly EmptyBuildingDelegate EmptyBuilding = AccessTools.MethodDelegate<EmptyBuildingDelegate>(typeof(CommonBuildingAI).GetMethod("EmptyBuilding", BindingFlags.Instance | BindingFlags.NonPublic), null, false);

            [HarmonyPatch(typeof(CommonBuildingAI), "BuildingDeactivated")]
            [HarmonyPrefix]
            private static bool Prefix(CommonBuildingAI __instance, ushort buildingID, ref Building data)
            {
                if (RealTimeAI.ShouldSwitchBuildingLightsOff(buildingID))
                {
                    TransferManager.TransferOffer offer = default;
                    offer.Building = buildingID;
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Garbage, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Crime, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Sick, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Sick2, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Dead, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Fire, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Fire2, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.ForestFire, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Collapsed, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Collapsed2, offer);
                    Singleton<TransferManager>.instance.RemoveOutgoingOffer(TransferManager.TransferReason.Mail, offer);
                    Singleton<TransferManager>.instance.RemoveIncomingOffer(TransferManager.TransferReason.Worker0, offer);
                    Singleton<TransferManager>.instance.RemoveIncomingOffer(TransferManager.TransferReason.Worker1, offer);
                    Singleton<TransferManager>.instance.RemoveIncomingOffer(TransferManager.TransferReason.Worker2, offer);
                    Singleton<TransferManager>.instance.RemoveIncomingOffer(TransferManager.TransferReason.Worker3, offer);
                    data.m_flags &= ~Building.Flags.Active;
                    EmptyBuilding(__instance, buildingID, ref data, CitizenUnit.Flags.Created, onlyMoving: false);
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        private sealed class FishingHarborAI_TrySpawnBoat
        {
            [HarmonyPatch(typeof(FishingHarborAI), "TrySpawnBoat")]
            [HarmonyPrefix]
            private static bool Prefix(ref Building buildingData) => (buildingData.m_flags & Building.Flags.Active) != 0;
        }

        [HarmonyPatch]
        private sealed class CommonBuildingAI_HandleCommonConsumption
        {
            private delegate bool CanStockpileElectricityDelegate(CommonBuildingAI __instance, ushort buildingID, ref Building data, out int stockpileAmount, out int stockpileRate);
            private static readonly CanStockpileElectricityDelegate CanStockpileElectricity = AccessTools.MethodDelegate<CanStockpileElectricityDelegate>(typeof(CommonBuildingAI).GetMethod("CanStockpileElectricity", BindingFlags.Instance | BindingFlags.NonPublic), null, true);

            private delegate bool CanStockpileWaterDelegate(CommonBuildingAI __instance, ushort buildingID, ref Building data, out int stockpileAmount, out int stockpileRate);
            private static readonly CanStockpileWaterDelegate CanStockpileWater = AccessTools.MethodDelegate<CanStockpileWaterDelegate>(typeof(CommonBuildingAI).GetMethod("CanStockpileWater", BindingFlags.Instance | BindingFlags.NonPublic), null, true);

            private delegate bool CanSufferFromFloodDelegate(CommonBuildingAI __instance, out bool onlyCollapse);
            private static readonly CanSufferFromFloodDelegate CanSufferFromFlood = AccessTools.MethodDelegate<CanSufferFromFloodDelegate>(typeof(CommonBuildingAI).GetMethod("CanSufferFromFlood", BindingFlags.Instance | BindingFlags.NonPublic), null, true);

            private delegate int GetCollapseTimeDelegate(CommonBuildingAI __instance);
            private static readonly GetCollapseTimeDelegate GetCollapseTime = AccessTools.MethodDelegate<GetCollapseTimeDelegate>(typeof(CommonBuildingAI).GetMethod("GetCollapseTime", BindingFlags.Instance | BindingFlags.NonPublic), null, true);

            private delegate void RemovePeopleDelegate(CommonBuildingAI __instance, ushort buildingID, ref Building data, int killPercentage);
            private static readonly RemovePeopleDelegate RemovePeople = AccessTools.MethodDelegate<RemovePeopleDelegate>(typeof(CommonBuildingAI).GetMethod("RemovePeople", BindingFlags.Instance | BindingFlags.NonPublic), null, false);



            [HarmonyPatch(typeof(CommonBuildingAI), "HandleCommonConsumption")]
            [HarmonyPrefix]
            public static bool HandleCommonConsumption(CommonBuildingAI __instance, ushort buildingID, ref Building data, ref Building.Frame frameData, ref int electricityConsumption, ref int heatingConsumption, ref int waterConsumption, ref int sewageAccumulation, ref int garbageAccumulation, ref int mailAccumulation, int maxMail, DistrictPolicies.Services policies, ref int __result)
            {
                electricityConsumption /= 10;
                heatingConsumption /= 10;
                waterConsumption /= 10;
                sewageAccumulation /= 10;
                garbageAccumulation /= 10;
                mailAccumulation /= 10;
                int num = 100;
                var instance = Singleton<DistrictManager>.instance;
                var problemStruct = Notification.RemoveProblems(data.m_problems, Notification.Problem1.Electricity | Notification.Problem1.Water | Notification.Problem1.Sewage | Notification.Problem1.Flood | Notification.Problem1.Heating);
                bool flag = data.m_electricityProblemTimer != 0;
                bool flag2 = false;
                bool flag3 = false;
                int electricityUsage = 0;
                int heatingUsage = 0;
                int waterUsage = 0;
                int sewageUsage = 0;
                if (electricityConsumption != 0)
                {
                    electricityConsumption = UniqueFacultyAI.DecreaseByBonus(UniqueFacultyAI.FacultyBonus.Science, electricityConsumption);
                    int value = Mathf.RoundToInt((20f - Singleton<WeatherManager>.instance.SampleTemperature(data.m_position, ignoreWeather: false)) * 8f);
                    value = Mathf.Clamp(value, 0, 400);
                    int num2 = heatingConsumption;
                    heatingConsumption = (num2 * value + Singleton<SimulationManager>.instance.m_randomizer.Int32(100u)) / 100;
                    if ((policies & DistrictPolicies.Services.PowerSaving) != 0)
                    {
                        electricityConsumption = Mathf.Max(1, electricityConsumption * 90 / 100);
                        Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.PolicyCost, 32, __instance.m_info.m_class);
                    }
                    bool connected = false;
                    int num3 = heatingConsumption * 2 - data.m_heatingBuffer;
                    if (num3 > 0 && (policies & DistrictPolicies.Services.OnlyElectricity) == 0)
                    {
                        int num4 = Singleton<WaterManager>.instance.TryFetchHeating(data.m_position, heatingConsumption, num3, out connected);
                        data.m_heatingBuffer += (ushort)num4;
                    }
                    if (data.m_heatingBuffer < heatingConsumption)
                    {
                        if ((policies & DistrictPolicies.Services.NoElectricity) != 0)
                        {
                            flag3 = true;
                            data.m_heatingProblemTimer = (byte)Mathf.Min(255, data.m_heatingProblemTimer + 1);
                            if (data.m_heatingProblemTimer >= 65)
                            {
                                num = 0;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Heating | Notification.Problem1.MajorProblem);
                            }
                            else if (data.m_heatingProblemTimer >= 3)
                            {
                                num /= 2;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Heating);
                            }
                        }
                        else
                        {
                            value = ((value + 50) * (heatingConsumption - data.m_heatingBuffer) + heatingConsumption - 1) / heatingConsumption;
                            electricityConsumption += (num2 * value + Singleton<SimulationManager>.instance.m_randomizer.Int32(100u)) / 100;
                            if (connected)
                            {
                                flag3 = true;
                                data.m_heatingProblemTimer = (byte)Mathf.Min(255, data.m_heatingProblemTimer + 1);
                                if (data.m_heatingProblemTimer >= 3)
                                {
                                    problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Heating);
                                }
                            }
                        }
                        heatingUsage = data.m_heatingBuffer;
                        data.m_heatingBuffer = 0;
                    }
                    else
                    {
                        heatingUsage = heatingConsumption;
                        data.m_heatingBuffer -= (ushort)heatingConsumption;
                    }
                    if (CanStockpileElectricity(__instance, buildingID, ref data, out int stockpileAmount, out int stockpileRate))
                    {
                        num3 = stockpileAmount + electricityConsumption * 2 - data.m_electricityBuffer;
                        if (num3 > 0)
                        {
                            int num5 = electricityConsumption;
                            if (data.m_electricityBuffer < stockpileAmount)
                            {
                                num5 += Mathf.Min(stockpileRate, stockpileAmount - data.m_electricityBuffer);
                            }
                            int num6 = Singleton<ElectricityManager>.instance.TryFetchElectricity(data.m_position, num5, num3);
                            data.m_electricityBuffer += (ushort)num6;
                            if (num6 < num3 && num6 < num5)
                            {
                                flag2 = true;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Electricity);
                                if (data.m_electricityProblemTimer < 64)
                                {
                                    data.m_electricityProblemTimer = 64;
                                }
                            }
                        }
                    }
                    else
                    {
                        num3 = electricityConsumption * 2 - data.m_electricityBuffer;
                        if (num3 > 0)
                        {
                            int num7 = Singleton<ElectricityManager>.instance.TryFetchElectricity(data.m_position, electricityConsumption, num3);
                            data.m_electricityBuffer += (ushort)num7;
                        }
                    }
                    if (data.m_electricityBuffer < electricityConsumption)
                    {
                        flag2 = true;
                        data.m_electricityProblemTimer = (byte)Mathf.Min(255, data.m_electricityProblemTimer + 1);
                        if (data.m_electricityProblemTimer >= 65)
                        {
                            num = 0;
                            problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Electricity | Notification.Problem1.MajorProblem);
                        }
                        else if (data.m_electricityProblemTimer >= 3)
                        {
                            num /= 2;
                            problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Electricity);
                        }
                        electricityUsage = data.m_electricityBuffer;
                        data.m_electricityBuffer = 0;
                        if (Singleton<UnlockManager>.instance.Unlocked(ItemClass.Service.Electricity))
                        {
                            var properties = Singleton<GuideManager>.instance.m_properties;
                            if (properties != null)
                            {
                                int publicServiceIndex = ItemClass.GetPublicServiceIndex(ItemClass.Service.Electricity);
                                int electricityCapacity = instance.m_districts.m_buffer[0].GetElectricityCapacity();
                                int electricityConsumption2 = instance.m_districts.m_buffer[0].GetElectricityConsumption();
                                if (electricityCapacity >= electricityConsumption2)
                                {
                                    Singleton<GuideManager>.instance.m_serviceNeeded[publicServiceIndex].Activate(properties.m_serviceNeeded2, ItemClass.Service.Electricity);
                                }
                                else
                                {
                                    Singleton<GuideManager>.instance.m_serviceNeeded[publicServiceIndex].Activate(properties.m_serviceNeeded, ItemClass.Service.Electricity);
                                }
                            }
                        }
                    }
                    else
                    {
                        electricityUsage = electricityConsumption;
                        data.m_electricityBuffer -= (ushort)electricityConsumption;
                    }
                }
                else
                {
                    heatingConsumption = 0;
                }
                if (!flag2)
                {
                    data.m_electricityProblemTimer = 0;
                }
                if (flag != flag2)
                {
                    Singleton<BuildingManager>.instance.UpdateBuildingColors(buildingID);
                }
                if (!flag3)
                {
                    data.m_heatingProblemTimer = 0;
                }
                bool flag4 = false;
                sewageAccumulation = UniqueFacultyAI.DecreaseByBonus(UniqueFacultyAI.FacultyBonus.Engineering, sewageAccumulation);
                int num8 = sewageAccumulation;
                if (waterConsumption != 0)
                {
                    waterConsumption = UniqueFacultyAI.DecreaseByBonus(UniqueFacultyAI.FacultyBonus.Engineering, waterConsumption);
                    if ((policies & DistrictPolicies.Services.WaterSaving) != 0)
                    {
                        waterConsumption = Mathf.Max(1, waterConsumption * 85 / 100);
                        if (sewageAccumulation != 0)
                        {
                            sewageAccumulation = Mathf.Max(1, sewageAccumulation * 85 / 100);
                        }
                        Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.PolicyCost, 32, __instance.m_info.m_class);
                    }
                    if (CanStockpileWater(__instance, buildingID, ref data, out int stockpileAmount2, out int stockpileRate2))
                    {
                        int num9 = stockpileAmount2 + waterConsumption * 2 - data.m_waterBuffer;
                        if (num9 > 0)
                        {
                            int num10 = waterConsumption;
                            if (data.m_waterBuffer < stockpileAmount2)
                            {
                                num10 += Mathf.Min(stockpileRate2, stockpileAmount2 - data.m_waterBuffer);
                            }
                            int num11 = Singleton<WaterManager>.instance.TryFetchWater(data.m_position, num10, num9, ref data.m_waterPollution);
                            data.m_waterBuffer += (ushort)num11;
                            if (num11 < num9 && num11 < num10)
                            {
                                flag4 = true;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Water);
                                if (data.m_waterProblemTimer < 64)
                                {
                                    data.m_waterProblemTimer = 64;
                                }
                            }
                        }
                    }
                    else
                    {
                        int num12 = waterConsumption * 2 - data.m_waterBuffer;
                        if (num12 > 0)
                        {
                            int num13 = Singleton<WaterManager>.instance.TryFetchWater(data.m_position, waterConsumption, num12, ref data.m_waterPollution);
                            data.m_waterBuffer += (ushort)num13;
                        }
                    }
                    if (data.m_waterBuffer < waterConsumption)
                    {
                        flag4 = true;
                        data.m_waterProblemTimer = (byte)Mathf.Min(255, data.m_waterProblemTimer + 1);
                        if (data.m_waterProblemTimer >= 65)
                        {
                            num = 0;
                            problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Water | Notification.Problem1.MajorProblem);
                        }
                        else if (data.m_waterProblemTimer >= 3)
                        {
                            num /= 2;
                            problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Water);
                        }
                        num8 = sewageAccumulation * (waterConsumption + data.m_waterBuffer) / (waterConsumption << 1);
                        waterUsage = data.m_waterBuffer;
                        data.m_waterBuffer = 0;
                        if (Singleton<UnlockManager>.instance.Unlocked(ItemClass.Service.Water))
                        {
                            var properties2 = Singleton<GuideManager>.instance.m_properties;
                            if (properties2 != null)
                            {
                                int publicServiceIndex2 = ItemClass.GetPublicServiceIndex(ItemClass.Service.Water);
                                int waterCapacity = instance.m_districts.m_buffer[0].GetWaterCapacity();
                                int waterConsumption2 = instance.m_districts.m_buffer[0].GetWaterConsumption();
                                if (waterCapacity >= waterConsumption2)
                                {
                                    Singleton<GuideManager>.instance.m_serviceNeeded[publicServiceIndex2].Activate(properties2.m_serviceNeeded2, ItemClass.Service.Water);
                                }
                                else
                                {
                                    Singleton<GuideManager>.instance.m_serviceNeeded[publicServiceIndex2].Activate(properties2.m_serviceNeeded, ItemClass.Service.Water);
                                }
                            }
                        }
                    }
                    else
                    {
                        num8 = sewageAccumulation;
                        waterUsage = waterConsumption;
                        data.m_waterBuffer -= (ushort)waterConsumption;
                    }
                }
                if (CanStockpileWater(__instance, buildingID, ref data, out int stockpileAmount3, out int stockpileRate3))
                {
                    int num14 = Mathf.Max(0, stockpileAmount3 + num8 * 2 - data.m_sewageBuffer);
                    if (num14 < num8)
                    {
                        if (!flag4 && (data.m_problems & Notification.Problem1.Water).IsNone)
                        {
                            flag4 = true;
                            data.m_waterProblemTimer = (byte)Mathf.Min(255, data.m_waterProblemTimer + 1);
                            if (data.m_waterProblemTimer >= 65)
                            {
                                num = 0;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Sewage | Notification.Problem1.MajorProblem);
                            }
                            else if (data.m_waterProblemTimer >= 3)
                            {
                                num /= 2;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Sewage);
                            }
                        }
                        sewageUsage = num14;
                        data.m_sewageBuffer = (ushort)(stockpileAmount3 + num8 * 2);
                    }
                    else
                    {
                        sewageUsage = num8;
                        data.m_sewageBuffer += (ushort)num8;
                    }
                    int num15 = num8 + Mathf.Max(num8, stockpileRate3);
                    num14 = Mathf.Min(num15, data.m_sewageBuffer);
                    if (num14 > 0)
                    {
                        int num16 = Singleton<WaterManager>.instance.TryDumpSewage(data.m_position, num15, num14);
                        data.m_sewageBuffer -= (ushort)num16;
                        if (num16 < num15 && num16 < num14 && !flag4 && (data.m_problems & Notification.Problem1.Water).IsNone)
                        {
                            flag4 = true;
                            problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Sewage);
                            if (data.m_waterProblemTimer < 64)
                            {
                                data.m_waterProblemTimer = 64;
                            }
                        }
                    }
                }
                else if (num8 != 0)
                {
                    int num17 = Mathf.Max(0, num8 * 2 - data.m_sewageBuffer);
                    if (num17 < num8)
                    {
                        if (!flag4 && (data.m_problems & Notification.Problem1.Water).IsNone)
                        {
                            flag4 = true;
                            data.m_waterProblemTimer = (byte)Mathf.Min(255, data.m_waterProblemTimer + 1);
                            if (data.m_waterProblemTimer >= 65)
                            {
                                num = 0;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Sewage | Notification.Problem1.MajorProblem);
                            }
                            else if (data.m_waterProblemTimer >= 3)
                            {
                                num /= 2;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Sewage);
                            }
                        }
                        sewageUsage = num17;
                        data.m_sewageBuffer = (ushort)(num8 * 2);
                    }
                    else
                    {
                        sewageUsage = num8;
                        data.m_sewageBuffer += (ushort)num8;
                    }
                    num17 = Mathf.Min(num8 * 2, data.m_sewageBuffer);
                    if (num17 > 0)
                    {
                        int num18 = Singleton<WaterManager>.instance.TryDumpSewage(data.m_position, num8 * 2, num17);
                        data.m_sewageBuffer -= (ushort)num18;
                    }
                }
                if (!flag4)
                {
                    data.m_waterProblemTimer = 0;
                }
                garbageAccumulation = UniqueFacultyAI.DecreaseByBonus(UniqueFacultyAI.FacultyBonus.Environment, garbageAccumulation);
                if (garbageAccumulation != 0)
                {
                    int num19 = 65535 - data.m_garbageBuffer;
                    if (num19 <= garbageAccumulation)
                    {
                        num /= 2;
                        data.m_garbageBuffer = ushort.MaxValue;
                    }
                    else
                    {
                        data.m_garbageBuffer += (ushort)garbageAccumulation;
                    }
                }
                if (garbageAccumulation != 0)
                {
                    int garbageBuffer = data.m_garbageBuffer;
                    if (garbageBuffer >= 200 && Singleton<SimulationManager>.instance.m_randomizer.Int32(5u) == 0 && Singleton<UnlockManager>.instance.Unlocked(ItemClass.Service.Garbage))
                    {
                        int count = 0;
                        int cargo = 0;
                        int capacity = 0;
                        int outside = 0;
                        __instance.CalculateGuestVehicles(buildingID, ref data, TransferManager.TransferReason.Garbage, ref count, ref cargo, ref capacity, ref outside);
                        garbageBuffer -= capacity - cargo;
                        if (garbageBuffer >= 200)
                        {
                            TransferManager.TransferOffer offer = default;
                            offer.Priority = garbageBuffer / 1000;
                            offer.Building = buildingID;
                            offer.Position = data.m_position;
                            offer.Amount = 1;
                            Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Garbage, offer);
                        }
                    }
                }
                if (mailAccumulation != 0)
                {
                    if ((policies & DistrictPolicies.Services.FreeWifi) != 0)
                    {
                        mailAccumulation = (mailAccumulation * 17 + Singleton<SimulationManager>.instance.m_randomizer.Int32(80u)) / 80;
                        if ((buildingID & (Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8) & (true ? 1u : 0u)) != 0)
                        {
                            Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.PolicyCost, 13, __instance.m_info.m_class);
                        }
                        else
                        {
                            Singleton<EconomyManager>.instance.FetchResource(EconomyManager.Resource.PolicyCost, 12, __instance.m_info.m_class);
                        }
                    }
                    else
                    {
                        mailAccumulation = mailAccumulation + Singleton<SimulationManager>.instance.m_randomizer.Int32(4u) >> 2;
                    }
                }
                if (mailAccumulation != 0)
                {
                    int num20 = Mathf.Min(maxMail, 65535) - data.m_mailBuffer;
                    if (num20 <= mailAccumulation)
                    {
                        data.m_mailBuffer = (ushort)Mathf.Min(maxMail, 65535);
                    }
                    else
                    {
                        data.m_mailBuffer += (ushort)mailAccumulation;
                    }
                }
                if (Singleton<LoadingManager>.instance.SupportsExpansion(Expansion.Industry) && Singleton<UnlockManager>.instance.Unlocked(ItemClass.SubService.PublicTransportPost) && mailAccumulation != 0 && maxMail != 0)
                {
                    int mailBuffer = data.m_mailBuffer;
                    if (mailBuffer >= maxMail / 8 && Singleton<SimulationManager>.instance.m_randomizer.Int32(5u) == 0)
                    {
                        int count2 = 0;
                        int cargo2 = 0;
                        int capacity2 = 0;
                        int outside2 = 0;
                        __instance.CalculateGuestVehicles(buildingID, ref data, TransferManager.TransferReason.Mail, ref count2, ref cargo2, ref capacity2, ref outside2);
                        mailBuffer -= capacity2 - cargo2;
                        if (mailBuffer >= maxMail / 8)
                        {
                            TransferManager.TransferOffer offer2 = default;
                            offer2.Priority = mailBuffer * 8 / maxMail;
                            offer2.Building = buildingID;
                            offer2.Position = data.m_position;
                            offer2.Amount = 1;
                            Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.Mail, offer2);
                        }
                    }
                }
                if (CanSufferFromFlood(__instance, out bool onlyCollapse))
                {
                    float num21 = Singleton<TerrainManager>.instance.WaterLevel(VectorUtils.XZ(data.m_position));
                    if (num21 > data.m_position.y)
                    {
                        bool flag5 = num21 > data.m_position.y + Mathf.Max(4f, __instance.m_info.m_collisionHeight) && (data.m_flags & Building.Flags.Untouchable) == 0;
                        if ((!onlyCollapse || flag5) && (data.m_flags & Building.Flags.Flooded) == 0 && data.m_fireIntensity == 0)
                        {
                            var instance2 = Singleton<DisasterManager>.instance;
                            ushort disasterIndex = instance2.FindDisaster<FloodBaseAI>(data.m_position);
                            if (disasterIndex == 0)
                            {
                                var disasterInfo = DisasterManager.FindDisasterInfo<GenericFloodAI>();
                                if (disasterInfo != null && instance2.CreateDisaster(out disasterIndex, disasterInfo))
                                {
                                    instance2.m_disasters.m_buffer[disasterIndex].m_intensity = 10;
                                    instance2.m_disasters.m_buffer[disasterIndex].m_targetPosition = data.m_position;
                                    disasterInfo.m_disasterAI.StartNow(disasterIndex, ref instance2.m_disasters.m_buffer[disasterIndex]);
                                }
                            }
                            if (disasterIndex != 0)
                            {
                                InstanceID srcID = default;
                                InstanceID dstID = default;
                                srcID.Disaster = disasterIndex;
                                dstID.Building = buildingID;
                                Singleton<InstanceManager>.instance.CopyGroup(srcID, dstID);
                                var info = instance2.m_disasters.m_buffer[disasterIndex].Info;
                                info.m_disasterAI.ActivateNow(disasterIndex, ref instance2.m_disasters.m_buffer[disasterIndex]);
                                if ((instance2.m_disasters.m_buffer[disasterIndex].m_flags & DisasterData.Flags.Significant) != 0)
                                {
                                    instance2.DetectDisaster(disasterIndex, located: false);
                                    instance2.FollowDisaster(disasterIndex);
                                }
                            }
                            data.m_flags |= Building.Flags.Flooded;
                        }
                        if (flag5)
                        {
                            frameData.m_constructState = (byte)Mathf.Max(0, frameData.m_constructState - 1088 / GetCollapseTime(__instance));
                            data.SetFrameData(Singleton<SimulationManager>.instance.m_currentFrameIndex, frameData);
                            InstanceID id = default;
                            id.Building = buildingID;
                            var group = Singleton<InstanceManager>.instance.GetGroup(id);
                            if (group != null)
                            {
                                ushort disaster = group.m_ownerInstance.Disaster;
                                if (disaster != 0)
                                {
                                    Singleton<DisasterManager>.instance.m_disasters.m_buffer[disaster].m_collapsedCount++;
                                }
                            }
                            if (frameData.m_constructState == 0)
                            {
                                Singleton<InstanceManager>.instance.SetGroup(id, null);
                            }
                            data.m_levelUpProgress = 0;
                            data.m_fireIntensity = 0;
                            data.m_garbageBuffer = 0;
                            data.m_flags = (data.m_flags & (Building.Flags.ContentMask | Building.Flags.IncomingOutgoing | Building.Flags.CapacityFull | Building.Flags.Created | Building.Flags.Deleted | Building.Flags.Original | Building.Flags.CustomName | Building.Flags.Untouchable | Building.Flags.FixedHeight | Building.Flags.RateReduced | Building.Flags.HighDensity | Building.Flags.RoadAccessFailed | Building.Flags.Evacuating | Building.Flags.Completed | Building.Flags.Active | Building.Flags.Abandoned | Building.Flags.Demolishing | Building.Flags.ZonesUpdated | Building.Flags.Downgrading | Building.Flags.Collapsed | Building.Flags.Upgrading | Building.Flags.SecondaryLoading | Building.Flags.Hidden | Building.Flags.EventActive | Building.Flags.Flooded | Building.Flags.Filling)) | Building.Flags.Collapsed;
                            num = 0;
                            RemovePeople(__instance, buildingID, ref data, 90);
                            __instance.BuildingDeactivated(buildingID, ref data);
                            if (__instance.m_info.m_hasParkingSpaces != 0)
                            {
                                Singleton<BuildingManager>.instance.UpdateParkingSpaces(buildingID, ref data);
                            }
                            Singleton<BuildingManager>.instance.UpdateBuildingRenderer(buildingID, updateGroup: true);
                            Singleton<BuildingManager>.instance.UpdateBuildingColors(buildingID);
                            var properties3 = Singleton<GuideManager>.instance.m_properties;
                            if (properties3 != null)
                            {
                                Singleton<BuildingManager>.instance.m_buildingFlooded.Deactivate(buildingID, soft: false);
                                Singleton<BuildingManager>.instance.m_buildingFlooded2.Deactivate(buildingID, soft: false);
                            }
                            if (data.m_subBuilding != 0 && data.m_parentBuilding == 0)
                            {
                                int num22 = 0;
                                ushort subBuilding = data.m_subBuilding;
                                while (subBuilding != 0)
                                {
                                    var info2 = Singleton<BuildingManager>.instance.m_buildings.m_buffer[subBuilding].Info;
                                    info2.m_buildingAI.CollapseBuilding(subBuilding, ref Singleton<BuildingManager>.instance.m_buildings.m_buffer[subBuilding], group, testOnly: false, demolish: false, 0);
                                    subBuilding = Singleton<BuildingManager>.instance.m_buildings.m_buffer[subBuilding].m_subBuilding;
                                    if (++num22 > 49152)
                                    {
                                        CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (!onlyCollapse)
                        {
                            if ((data.m_flags & Building.Flags.RoadAccessFailed) == 0)
                            {
                                int count3 = 0;
                                int cargo3 = 0;
                                int capacity3 = 0;
                                int outside3 = 0;
                                __instance.CalculateGuestVehicles(buildingID, ref data, TransferManager.TransferReason.FloodWater, ref count3, ref cargo3, ref capacity3, ref outside3);
                                if (count3 == 0)
                                {
                                    TransferManager.TransferOffer offer3 = default;
                                    offer3.Priority = 5;
                                    offer3.Building = buildingID;
                                    offer3.Position = data.m_position;
                                    offer3.Amount = 1;
                                    Singleton<TransferManager>.instance.AddOutgoingOffer(TransferManager.TransferReason.FloodWater, offer3);
                                }
                            }
                            if (num21 > data.m_position.y + 1f)
                            {
                                num = 0;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Flood | Notification.Problem1.MajorProblem);
                            }
                            else
                            {
                                num /= 2;
                                problemStruct = Notification.AddProblems(problemStruct, Notification.Problem1.Flood);
                            }
                            var properties4 = Singleton<GuideManager>.instance.m_properties;
                            if (properties4 != null)
                            {
                                if (Singleton<LoadingManager>.instance.SupportsExpansion(Expansion.NaturalDisasters) && Singleton<UnlockManager>.instance.Unlocked(UnlockManager.Feature.WaterPumping))
                                {
                                    Singleton<BuildingManager>.instance.m_buildingFlooded2.Activate(properties4.m_buildingFlooded2, buildingID);
                                }
                                else
                                {
                                    Singleton<BuildingManager>.instance.m_buildingFlooded.Activate(properties4.m_buildingFlooded, buildingID);
                                }
                            }
                        }
                    }
                    else if ((data.m_flags & Building.Flags.Flooded) != 0)
                    {
                        InstanceID id2 = default;
                        id2.Building = buildingID;
                        Singleton<InstanceManager>.instance.SetGroup(id2, null);
                        data.m_flags &= ~Building.Flags.Flooded;
                    }
                }
                byte district = instance.GetDistrict(data.m_position);
                instance.m_districts.m_buffer[district].AddUsageData(electricityUsage, heatingUsage, waterUsage, sewageUsage);
                data.m_problems = problemStruct;
                __result = num;
                return false;
            }
        }

        [HarmonyPatch]
        private sealed class FishFarmAI_GetColor
        {
            [HarmonyPatch(typeof(FishFarmAI), "GetColor")]
            [HarmonyPrefix]
            public static bool GetColor(FishFarmAI __instance, ushort buildingID, ref Building data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, ref Color __result)
            {
                if(infoMode == InfoManager.InfoMode.Fishing)
                {
                    if(data.m_productionRate > 0)
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                    }
                    else
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        private sealed class FishingHarborAI_GetColor
        {
            [HarmonyPatch(typeof(FishingHarborAI), "GetColor")]
            [HarmonyPrefix]
            public static bool GetColor(FishingHarborAI __instance, ushort buildingID, ref Building data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, ref Color __result)
            {
                if (infoMode == InfoManager.InfoMode.Fishing)
                {
                    if (data.m_productionRate > 0)
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                    }
                    else
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        private sealed class SchoolAI_GetColor
        {
            [HarmonyPatch(typeof(SchoolAI), "GetColor")]
            [HarmonyPrefix]
            public static bool GetColor(SchoolAI __instance, ushort buildingID, ref Building data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, ref Color __result)
            {
                if (infoMode == InfoManager.InfoMode.Education)
                {
                    var level = ItemClass.Level.None;
                    switch (subInfoMode)
                    {
                        case InfoManager.SubInfoMode.Default:
                            level = ItemClass.Level.Level1;
                            break;
                        case InfoManager.SubInfoMode.WaterPower:
                            level = ItemClass.Level.Level2;
                            break;
                        case InfoManager.SubInfoMode.WindPower:
                            level = ItemClass.Level.Level3;
                            break;
                    }
                    if (level == __instance.m_info.m_class.m_level && __instance.m_info.m_class.m_service == ItemClass.Service.Education)
                    {
                        if (data.m_productionRate > 0)
                        {
                            __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                        }
                        else
                        {
                            __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                        }
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch]
        private sealed class LibraryAI_GetColor
        {
            [HarmonyPatch(typeof(LibraryAI), "GetColor")]
            [HarmonyPrefix]
            public static bool GetColor(LibraryAI __instance, ushort buildingID, ref Building data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, ref Color __result)
            {
                if (infoMode == InfoManager.InfoMode.Education && subInfoMode == InfoManager.SubInfoMode.LibraryEducation)
                {
                    if (data.m_productionRate > 0)
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                    }
                    else
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                    }
                    return false;
                }
                if (infoMode == InfoManager.InfoMode.Entertainment && subInfoMode == InfoManager.SubInfoMode.PipeWater)
                {
                    if (data.m_productionRate > 0)
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                    }
                    else
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        private sealed class ParkAI_GetColor
        {
            [HarmonyPatch(typeof(ParkAI), "GetColor")]
            [HarmonyPrefix]
            public static bool GetColor(ParkAI __instance, ushort buildingID, ref Building data, InfoManager.InfoMode infoMode, InfoManager.SubInfoMode subInfoMode, ref Color __result)
            {
                if (infoMode == InfoManager.InfoMode.Entertainment)
                {
                    if(subInfoMode == InfoManager.SubInfoMode.WaterPower)
                    {
                        if (data.m_productionRate > 0)
                        {
                            __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                        }
                        else
                        {
                            __result = Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
                        }
                    }
                    else
                    {
                        __result = Singleton<InfoManager>.instance.m_properties.m_neutralColor;
                    }
                    return false;
                }
                return true;
            }
        }



    }
}
