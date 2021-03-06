﻿// Title: AgentInputHandler.cs
// Author: Harry Donovan
// Date Last Edited: 07/05/2020
// Description: Script allowing an agent to take user input. Contains references to gameobjects or behaviors attached to the agent that are needed by commands. Contains delegates which run during the MonoBehaviour to allow commands to hook into MonoBehaviour without having to be a MonoBehaviour themselves. Also contains custom delegates for specific events.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ToggleBehaviourStruct
{
    public Behaviour behaviourToToggle;
    public AudioClip toggleOnSound = null;
    public AudioClip toggleOffSound = null;
}

[System.Serializable]
public class FootstepsStruct
{
    public AudioSource footstepSource = null;
    public AudioClip[] footstepClips;
    [HideInInspector]
    public float timeSinceFootstep;
}

[System.Serializable]
public class WeaponsStruct
{
    public Weapon currentWeapon;
    [HideInInspector]
    public float timeSinceLastShot = 0.0f;
    [HideInInspector]
    public float currentRecoilValue = 0.0f;
    [HideInInspector]
    public GameObject weaponObject;
    public ParticleSystem weaponMuzzleFlash;
    public AudioSource weaponAudioSource;
    public Weapon[] equippedWeapons;
    [HideInInspector]
    public int currentWeaponID = 0;
}

[System.Serializable]
public class CameraStruct
{
    public Camera agentCamera;
    public Camera mainCamera;
    public Camera aDSCamera;
    [HideInInspector]
    public bool isADS = false;
}

[System.Serializable]
public class HUDStruct
{
    public Canvas HUDCanvas;
    public GameObject HUDGameObject;
    public GameObject deathScreen;
}

[System.Serializable]
public class AgentHitFeedbackStruct
{
    public AudioClip agentHitSound;
    public GameObject agentHitParticles;
}

public class AgentInputHandler : MonoBehaviour
{
    protected AgentController agentController;
    private AgentInputHandler attachedScript;

    public GameObject pauseMenu;
    public AudioSource mainAudioSource = null;

    public AgentValues agentValues;
    public ActiveCommandObject[] activeCommands;
    public PassiveCommandObject[] passiveCommands;

    public ToggleBehaviourStruct toggleBehaviour;

    [Header("Check If Grounded")]
    [HideInInspector]
    public bool isGrounded = true;
    [HideInInspector]
    public ContactPoint groundContactPoint = new ContactPoint();

    [Header("Movement")]
    [HideInInspector]
    public bool isSprinting = false;
    [HideInInspector]
    public Vector3 gravityDirection = Vector3.down;
    [HideInInspector]
    public bool allowInput = true;
    [HideInInspector]
    public float currentLeapCharge = 0.0f;
    [HideInInspector]
    public bool isJumping = false;
    [HideInInspector]
    public float moveSpeedMultiplier = 1.0f;
    [HideInInspector]
    public Rigidbody agentRigidbody;

    public FootstepsStruct footsteps;

    [Header("Stairs")]
    [HideInInspector]
    public Vector3 lastVelocity = Vector3.zero;

    public WeaponsStruct weapons;

    public Armour equippedArmour = null;

    [Header("Reloading")]
    [HideInInspector]
    public bool isReloading = false;

    public CameraStruct cameraList;

    public HUDStruct hUD;

    [HideInInspector]
    public Vector3 UIOffset = Vector3.zero;

    public AgentHitFeedbackStruct agentHitFeedback;

    [HideInInspector]
    public bool isLocalAgent = true;

    [Header("ObjectInteraction")]
    [HideInInspector]
    public TMP_Text interactionPromptText = null;
    [HideInInspector]
    public Image progressBar = null;

    // Delegates used by commands.
    // Should add a delegate for UpdateUI(GameObject UIToUpdate, float newValue = 0.0f, int newIntValue = 0), maybe.
    public delegate void RunCommandOnUpdate(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues);
    public RunCommandOnUpdate runCommandOnUpdate;
    public delegate void RunCommandOnFixedUpdate(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues);
    public RunCommandOnFixedUpdate runCommandOnFixedUpdate;
    public delegate void RunCommandOnCollisionEnter(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collision other);
    public RunCommandOnCollisionStay runCommandOnCollisionEnter;
    public delegate void RunCommandOnCollisionStay(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collision other);
    public RunCommandOnCollisionStay runCommandOnCollisionStay;
    public delegate void RunCommandOnCollisionExit(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collision other);
    public RunCommandOnCollisionExit runCommandOnCollisionExit;
    public delegate void RunCommandOnTriggerEnter(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collider other);
    public RunCommandOnTriggerEnter runCommandOnTriggerEnter;
    public delegate void RunCommandOnTriggerStay(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collider other);
    public RunCommandOnTriggerStay runCommandOnTriggerStay;
    public delegate void RunCommandOnTriggerExit(GameObject agent, AgentInputHandler agentInputHandler, AgentValues agentValues, Collider other);
    public RunCommandOnTriggerExit runCommandOnTriggerExit;


    public delegate void RunCommandOnWeaponFired(AgentInputHandler agentInputHandler);
    public RunCommandOnWeaponFired runCommandOnWeaponFired;
    public delegate void RunCommandOnAgentHasBeenHit(AgentInputHandler agentInputHandler, Vector3 position, Vector3 normal, float value);
    public RunCommandOnAgentHasBeenHit runCommandOnAgentHasBeenHit;
    public delegate void RunCommandOnCameraMovement(Vector3 cameraMovement, AgentInputHandler agentInputHandler, AgentValues agentValues);
    public RunCommandOnCameraMovement runCommandOnCameraMovement;

    private void Start()
    {
        if (agentController != null)
        {
            attachedScript = agentController;
        }
        else
        {
            attachedScript = this;
        }

        InitiliseVariable();

        foreach (ActiveCommandObject element in activeCommands)
        {
            element.RunCommandOnStart(attachedScript);
        }
        foreach (PassiveCommandObject element in passiveCommands)
        {
            element.RunCommandOnStart(attachedScript);
        }
    }

    public virtual void ChangeWeapon(Weapon weapon)
    {
        weapons.currentWeapon = weapon;
        weapons.timeSinceLastShot = weapons.currentWeapon.fireRate;
    }

    public void ChangeArmour(Armour armour)
    {
        if (equippedArmour != null)
        {
            ChangeMovementSpeedModifier(equippedArmour.speedMultiplier, false);
        }

        equippedArmour = armour;
        ChangeMovementSpeedModifier(equippedArmour.speedMultiplier, true);
    }

    private void Update()
    {
        if (runCommandOnUpdate != null)
        {
            runCommandOnUpdate(this.gameObject, attachedScript, agentValues);
        }
    }

    private void FixedUpdate()
    {
        if (runCommandOnFixedUpdate != null)
        {
            runCommandOnFixedUpdate(this.gameObject, attachedScript, agentValues);
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        if (runCommandOnCollisionEnter != null)
        {
            runCommandOnCollisionEnter(this.gameObject, attachedScript, agentValues, other);
        }
    }

    private void OnCollisionStay(Collision other)
    {
        if (runCommandOnCollisionStay != null)
        {
            runCommandOnCollisionStay(this.gameObject, attachedScript, agentValues, other);
        }
    }

    private void OnCollisionExit(Collision other)
    {
        if (runCommandOnCollisionExit != null)
        {
            runCommandOnCollisionExit(this.gameObject, attachedScript, agentValues, other);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (runCommandOnTriggerEnter != null)
        {
            runCommandOnTriggerEnter(this.gameObject, attachedScript, agentValues, other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (runCommandOnTriggerStay != null)
        {
            runCommandOnTriggerStay(this.gameObject, attachedScript, agentValues, other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (runCommandOnTriggerExit != null)
        {
            runCommandOnTriggerExit(this.gameObject, attachedScript, agentValues, other);
        }
    }

    void InitiliseVariable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        agentRigidbody = this.gameObject.GetComponent<Rigidbody>();

        if (cameraList.agentCamera == null)
        {
            if (cameraList.mainCamera != null)
            {
                cameraList.agentCamera = cameraList.mainCamera;
            }
            else
            {
                cameraList.agentCamera = GetComponentInChildren<Camera>();
            }
        }

        if (cameraList.mainCamera == null)
        {
            cameraList.mainCamera = cameraList.agentCamera;
        }
    }

    public void ChangeMovementSpeedModifier(float value, bool multiply)
    {
        if (multiply)
        {
            moveSpeedMultiplier *= value;
        }
        else
        {
            moveSpeedMultiplier /= value;
        }
    }

    public AudioClip GetRandomFootstepClip()
    {
        if (footsteps.footstepClips.Length > 0)
        {
            return footsteps.footstepClips[Random.Range(0, footsteps.footstepClips.Length - 1)];
        }
        else
        {
            return null;
        }
    }
}