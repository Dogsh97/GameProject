using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Player;

[RequireComponent(typeof(Collider))]
public class HideSpot : MonoBehaviour
{
    [SerializeField]
    private PlayerNode node;

    [SerializeField]
    private Transform hidePoint;

    public PlayerNode Node => node;

    public Transform HidePoint => hidePoint;

    [SerializeField]
    private bool isEnabled = true;

    public bool IsEnabled => isEnabled;

    [SerializeField]
    private Transform hideCameraPoint;

    public Transform HideCameraPoint => hideCameraPoint;
}
