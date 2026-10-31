using UnityEngine;
 

public class Interaction : MonoBehaviour
{
    public float distance = 3f;

    void Update()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance))
            {
                Debug.Log("Golpeó: " + hit.collider.name);

                Puerta door = hit.collider.GetComponentInParent<Puerta>();



                if (door != null)
                { 
                    door.ToggleDoor();
                }


                InterruptorLuz luz = hit.collider.GetComponent<InterruptorLuz>();
                if (luz != null)
                {
                    luz.ToggleLuz();
                    return;
                }

                Cama cama = hit.collider.GetComponentInParent<Cama>();
                if (cama != null)
                {
                    cama.Dormir();
                    return;
                }

                Gramophone gramophone = hit.collider.GetComponent<Gramophone>();

                if (gramophone != null)
                {
                    gramophone.ToggleMusic();
                }
            }
        }
    }
}