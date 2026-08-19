using UnityEngine;

public class GhostPlayerSpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject ghostPrefab;
    [SerializeField] private bool disableExistingControlledCharacters = true;

    private void Start()
    {
        SceneTravelStateManager travelManager = SceneTravelStateManager.Instance;
        if (travelManager != null && travelManager.HasTravelingControlledCharacter)
        {
            travelManager.ReclaimTravelingCharacterControl();
            return;
        }

        if (ghostPrefab == null)
        {
            Debug.LogWarning("GhostPlayerSpawnPoint needs a Ghost prefab before it can spawn the player.", this);
            return;
        }

        if (disableExistingControlledCharacters)
        {
            DisableExistingControlledCharacters();
        }

        GameObject ghostObject = Instantiate(ghostPrefab, transform.position, Quaternion.identity);
        ghostObject.name = ghostPrefab.name;

        ZeldaFourWayMover ghostMover = ghostObject.GetComponent<ZeldaFourWayMover>();
        if (ghostMover != null)
        {
            ghostMover.enabled = true;
        }
    }

    private void DisableExistingControlledCharacters()
    {
        foreach (ZeldaFourWayMover mover in ZeldaRuntimeRegistry.Movers)
        {
            if (mover != null && mover.isActiveAndEnabled)
            {
                mover.enabled = false;
            }
        }
    }
}
