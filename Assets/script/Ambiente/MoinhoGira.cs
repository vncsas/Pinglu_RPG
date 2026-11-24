using UnityEngine;

// Gira as pás do moinho devagar, como se o vento estivesse soprando.
public class MoinhoGira : MonoBehaviour
{
    public float grausPorSegundo = 9f;
    private void Update() { transform.Rotate(0f, 0f, grausPorSegundo * Time.deltaTime, Space.Self); }
}
