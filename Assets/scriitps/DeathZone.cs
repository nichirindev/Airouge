using UnityEngine;
using System.Collections;

public class DeathZone : MonoBehaviour
{
    public Transform[] respawnPoints;
    public float respawnDuration = 0.5f;
    public Camera mainCamera;
    public float zoopFovOffset = 100f;
    public AnimationCurve zoopCurve;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        
        if (zoopCurve == null || zoopCurve.length == 0)
        {
            zoopCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f); 
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (respawnPoints == null || respawnPoints.Length == 0)
            {
                Debug.LogWarning("fck u no ded");
                return;
            }

            int randomIndex = Random.Range(0, respawnPoints.Length);
            Transform chosenPoint = respawnPoints[randomIndex];

            StartCoroutine(LerpRespawn(collision.gameObject, chosenPoint));
        }
    }

    IEnumerator LerpRespawn(GameObject player, Transform targetPoint)
    {
        if (player.TryGetComponent<Rigidbody>(out Rigidbody rb))
        {
            rb.isKinematic = true;
//            rb.linearVelocity = Vector3.zero;
//            rb.angularVelocity = Vector3.zero;
        }

        Vector3 startPosition = player.transform.position;
        Quaternion startRotation = player.transform.rotation;
        
        float defaultFov = mainCamera != null ? mainCamera.fieldOfView : 60f;
        float elapsedTime = 0f;

        while (elapsedTime < respawnDuration)
        {
            elapsedTime += Time.deltaTime;
            float percentageComplete = elapsedTime / respawnDuration;

            player.transform.position = Vector3.Lerp(startPosition, targetPoint.position, percentageComplete);
            player.transform.rotation = Quaternion.Lerp(startRotation, targetPoint.rotation, percentageComplete);

            if (mainCamera != null)
            {

                float zoopFactor = Mathf.Sin(percentageComplete * Mathf.PI); 
                mainCamera.fieldOfView = defaultFov + (zoopFactor * zoopFovOffset);
            }

            yield return null;
        }

        player.transform.position = targetPoint.position;
        player.transform.rotation = targetPoint.rotation;
       // mainCamera.transform.rotation = targetPoint.rotation; //fcks up gameplay this is bad pls don't uncomment this statement
        
        if (mainCamera != null) mainCamera.fieldOfView = defaultFov;

        if (rb != null) rb.isKinematic = false;
    }
}
