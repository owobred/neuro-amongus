﻿using System;
using System.Linq;
using Neuro.Communication.AmongUsAI.DataStructures;
using Neuro.Events;
using Neuro.Pathfinding;
using Neuro.Utilities;
using Reactor.Utilities.Attributes;
using UnityEngine;

namespace Neuro.Communication.AmongUsAI;

[RegisterInIl2Cpp]
public sealed class MapDataRecorder : MonoBehaviour
{
    public static MapDataRecorder Instance { get; private set; }

    public MapDataRecorder(IntPtr ptr) : base(ptr)
    {
    }

    public DoorData[] NearbyDoors { get; } = new DoorData[3];
    public VentData[] NearbyVents { get; } = new VentData[3];

    private void Awake()
    {
        if (Instance)
        {
            NeuroUtilities.WarnDoubleSingletonInstance();
            Destroy(this);
            return;
        }

        Instance = this;
        EventManager.RegisterHandler(this);
    }

    private void FixedUpdate()
    {
        if (MeetingHud.Instance || Minigame.Instance || !PlayerControl.LocalPlayer) return;

        UpdateNearbyDoors();
        UpdateNearbyVents();
    }

    private void UpdateNearbyDoors()
    {
        PlainDoor[] nearbyDoors = ShipStatus.Instance.AllDoors.OrderBy(Closest).Take(3).ToArray();
        for (int i = 0; i < 3; i++)
        {
            NearbyDoors[i] = nearbyDoors.ElementAtOrDefault(i) is { } door ? DoorData.Create(door) : DoorData.Absent;
        }
    }

    private void UpdateNearbyVents()
    {
        Vent[] nearbyVents = ShipStatus.Instance.AllVents.OrderBy(Closest).Take(3).ToArray();
        for (int i = 0; i < 3; i++)
        {
            NearbyVents[i] = nearbyVents.ElementAtOrDefault(i) is { } vent ? VentData.Create(vent) : VentData.Absent;
        }
    }

    private float Closest(PlainDoor door)
    {
        return PathfindingHandler.Instance.CalculateTotalDistance(PlayerControl.LocalPlayer, door, door);
    }

    private float Closest(Vent vent)
    {
        return PathfindingHandler.Instance.CalculateTotalDistance(PlayerControl.LocalPlayer, vent, vent);
    }

    [EventHandler(EventTypes.GameStarted)]
    public static void OnGameStarted()
    {
        ShipStatus.Instance.gameObject.AddComponent<MapDataRecorder>();
    }
}
