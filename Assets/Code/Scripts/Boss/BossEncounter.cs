using System;
using UnityEngine;

/// <summary>
/// The boss fight as a scene event, kept apart from the boss's AI. When the player walks into the start trigger
/// (or, optionally, hits the boss first), the boss starts fighting and the arena closes. When the boss dies the
/// arena opens again. Dying reloads the scene (GameManager), which resets the encounter and the boss.
/// </summary>
public class BossEncounter : MonoBehaviour
{
    [SerializeField] private BossBrain boss;
    [Tooltip("The fight starts when the player enters this collider. Make it a trigger on the \"Ignore Raycast\" layer. Empty = a Collider2D on this object.")]
    [SerializeField] private Collider2D startTrigger;
    [Tooltip("Also start the fight when the player damages the boss before walking into the trigger. Careful: if the player can hit the boss from outside the arena, the gates close with them outside.")]
    [SerializeField] private bool startWhenBossDamaged = false;

    [Header("Arena")]
    [Tooltip("Switched on when the fight starts and off when the boss dies, e.g. walls or gates that lock the player in.")]
    [SerializeField] private GameObject[] activeDuringFight;
    [Tooltip("Switched on when the boss dies, e.g. the exit or a reward.")]
    [SerializeField] private GameObject[] activateOnVictory;

    private enum EncounterState { Waiting, Fighting, Won }

    private EncounterState state = EncounterState.Waiting;
    private BossHealth bossHealth;
    private Collider2D playerCollider;

    public event Action FightStarted;
    public event Action Won;

    private void Awake()
    {
        if (startTrigger == null) startTrigger = GetComponent<Collider2D>();

        if (boss == null)
        {
            Debug.LogError($"{name}: BossEncounter has no Boss assigned. Disabling.", this);
            enabled = false;
            return;
        }

        if (startTrigger == null && !startWhenBossDamaged)
            Debug.LogWarning($"{name}: BossEncounter has no Start Trigger, so the fight never starts.", this);

        bossHealth = boss.GetComponentInChildren<BossHealth>();
        if (bossHealth != null)
        {
            bossHealth.Died += OnBossDied;
            bossHealth.Damaged += OnBossDamaged;
        }

        SetActive(activeDuringFight, false);
        SetActive(activateOnVictory, false);
    }

    private void OnDestroy()
    {
        if (bossHealth != null)
        {
            bossHealth.Died -= OnBossDied;
            bossHealth.Damaged -= OnBossDamaged;
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerCollider = player != null ? player.GetComponentInParent<Collider2D>() : null;
        if (playerCollider == null)
            Debug.LogError("BossEncounter: No player with a Collider2D and the 'Player' tag found.", this);
    }

    private void FixedUpdate()
    {
        if (state != EncounterState.Waiting || startTrigger == null || playerCollider == null) return;

        // An overlap check instead of OnTriggerEnter2D, so it works whatever the layer collision matrix says.
        if (startTrigger.isActiveAndEnabled && Physics2D.Distance(startTrigger, playerCollider).isOverlapped)
            StartFight();
    }

    private void OnBossDamaged(float damage)
    {
        if (startWhenBossDamaged) StartFight();
    }

    /// <summary>Starts the fight. Does nothing if it already started.</summary>
    public void StartFight()
    {
        if (state != EncounterState.Waiting || boss == null) return;

        state = EncounterState.Fighting;
        SetActive(activeDuringFight, true);
        boss.BeginFight();
        FightStarted?.Invoke();
    }

    private void OnBossDied()
    {
        state = EncounterState.Won;
        SetActive(activeDuringFight, false);
        SetActive(activateOnVictory, true);
        Won?.Invoke();
    }

    private static void SetActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        foreach (GameObject target in objects)
            if (target != null) target.SetActive(active);
    }

    private void OnDrawGizmos()
    {
        Collider2D trigger = startTrigger != null ? startTrigger : GetComponent<Collider2D>();
        if (trigger == null) return;

        Gizmos.color = new Color(1f, 0f, 0.3f, 0.6f);
        Bounds bounds = trigger.bounds;
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }
}
