﻿// <copyright file="RealTimeResidentAI.Visit.cs" company="dymanoid">
// Copyright (c) dymanoid. All rights reserved.
// </copyright>

namespace RealTime.AI
{
    using ColossalFramework;

    internal static partial class RealTimeResidentAI
    {
        private static bool ProcessCitizenVisit(Arguments args, uint citizenID, ref Citizen data)
        {
            if (data.Dead)
            {
                if (data.m_visitBuilding == 0)
                {
                    args.CitizenMgr.ReleaseCitizen(citizenID);
                }
                else
                {
                    if (data.m_homeBuilding != 0)
                    {
                        data.SetHome(citizenID, 0, 0u);
                    }

                    if (data.m_workBuilding != 0)
                    {
                        data.SetWorkplace(citizenID, 0, 0u);
                    }

                    if (data.m_vehicle != 0)
                    {
                        return false;
                    }

                    if (args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].Info.m_class.m_service == ItemClass.Service.HealthCare)
                    {
                        return false;
                    }

                    if (FindHospital(args.ResidentAI, citizenID, data.m_visitBuilding, TransferManager.TransferReason.Dead))
                    {
                        return false;
                    }
                }

                return true;
            }

            if (data.Arrested)
            {
                if (data.m_visitBuilding == 0)
                {
                    data.Arrested = false;
                }
            }
            else if (!data.Collapsed)
            {
                if (data.Sick)
                {
                    if (data.m_visitBuilding == 0)
                    {
                        data.CurrentLocation = Citizen.Location.Home;
                        return false;
                    }

                    if (data.m_vehicle != 0)
                    {
                        return false;
                    }

                    ItemClass.Service service = args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].Info.m_class.m_service;
                    if (service == ItemClass.Service.HealthCare)
                    {
                        return false;
                    }

                    if (service == ItemClass.Service.Disaster)
                    {
                        return false;
                    }

                    if (FindHospital(args.ResidentAI, citizenID, data.m_visitBuilding, TransferManager.TransferReason.Sick))
                    {
                        return false;
                    }

                    return true;
                }

                ItemClass.Service service2 = ItemClass.Service.None;
                if (data.m_visitBuilding != 0)
                {
                    service2 = args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].Info.m_class.m_service;
                }

                switch (service2)
                {
                    case ItemClass.Service.HealthCare:
                    case ItemClass.Service.PoliceDepartment:
                        if (data.m_homeBuilding != 0 && data.m_vehicle == 0)
                        {
                            data.m_flags &= ~Citizen.Flags.Evacuating;
                            args.ResidentAI.StartMoving(citizenID, ref data, data.m_visitBuilding, data.m_homeBuilding);
                            data.SetVisitplace(citizenID, 0, 0u);
                        }

                        return false;
                    case ItemClass.Service.Disaster:
                        if ((args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].m_flags & Building.Flags.Downgrading) != 0 && data.m_homeBuilding != 0 && data.m_vehicle == 0)
                        {
                            data.m_flags &= ~Citizen.Flags.Evacuating;
                            args.ResidentAI.StartMoving(citizenID, ref data, data.m_visitBuilding, data.m_homeBuilding);
                            data.SetVisitplace(citizenID, 0, 0u);
                        }

                        return false;
                    default:
                        if (data.m_visitBuilding == 0)
                        {
                            data.CurrentLocation = Citizen.Location.Home;
                        }
                        else if ((args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].m_flags & Building.Flags.Evacuating) != 0)
                        {
                            if (data.m_vehicle == 0)
                            {
                                FindEvacuationPlace(args.ResidentAI, citizenID, data.m_visitBuilding, GetEvacuationReason(args.ResidentAI, data.m_visitBuilding));
                            }
                        }
                        else if ((data.m_flags & Citizen.Flags.NeedGoods) != 0)
                        {
                            BuildingInfo info = args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].Info;
                            int num7 = -100;
                            info.m_buildingAI.ModifyMaterialBuffer(data.m_visitBuilding, ref args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding], TransferManager.TransferReason.Shopping, ref num7);
                        }
                        else
                        {
                            ushort eventIndex2 = args.BuildingMgr.m_buildings.m_buffer[data.m_visitBuilding].m_eventIndex;
                            if (eventIndex2 != 0)
                            {
                                if ((Singleton<EventManager>.instance.m_events.m_buffer[eventIndex2].m_flags & (EventData.Flags.Preparing | EventData.Flags.Active | EventData.Flags.Ready)) == EventData.Flags.None && data.m_homeBuilding != 0 && data.m_vehicle == 0)
                                {
                                    data.m_flags &= ~Citizen.Flags.Evacuating;
                                    args.ResidentAI.StartMoving(citizenID, ref data, data.m_visitBuilding, data.m_homeBuilding);
                                    data.SetVisitplace(citizenID, 0, 0u);
                                }
                            }
                            else
                            {
                                if (data.m_instance == 0 && !DoRandomMove(args.ResidentAI))
                                {
                                    return false;
                                }

                                int num8 = args.SimMgr.m_randomizer.Int32(40u);
                                if (num8 < 10 && data.m_homeBuilding != 0 && data.m_vehicle == 0)
                                {
                                    data.m_flags &= ~Citizen.Flags.Evacuating;
                                    args.ResidentAI.StartMoving(citizenID, ref data, data.m_visitBuilding, data.m_homeBuilding);
                                    data.SetVisitplace(citizenID, 0, 0u);
                                }
                            }
                        }

                        return false;
                }
            }

            return false;
        }
    }
}
