using System.Collections.Generic;
using UnityEngine;

public class InterruptorLuz : MonoBehaviour
{
    public GameObject luz; 

    private AudioSource audioSource;
    private Light[] lucesControladas;
    private bool estadoSinLuces = true;

    public bool IsOn
    {
        get
        {
            CacheLights();
            if (lucesControladas.Length > 0)
            {
                foreach (Light lightComponent in lucesControladas)
                {
                    if (lightComponent != null && lightComponent.enabled && lightComponent.gameObject.activeInHierarchy)
                        return true;
                }
                return false;
            }

            return luz != null && luz != gameObject ? luz.activeSelf : estadoSinLuces;
        }
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        CacheLights();
    }

    private void Start()
    {
        CacheLights();
        if (lucesControladas.Length > 0)
        {
            foreach (Light lightComponent in lucesControladas)
            {
                if (lightComponent != null)
                    lightComponent.enabled = true;
            }
        }
        else if (luz != null && luz != gameObject)
        {
            luz.SetActive(true);
        }

        estadoSinLuces = true;
    }

    public void ToggleLuz()
    {
        CacheLights();
        bool turnOn = !IsOn;

        if (audioSource != null)
            audioSource.Play();

        if (lucesControladas.Length > 0)
        {
            foreach (Light lightComponent in lucesControladas)
            {
                if (lightComponent != null)
                    lightComponent.enabled = turnOn;
            }
        }
        else if (luz != null && luz != gameObject)
        {
            luz.SetActive(turnOn);
        }

        estadoSinLuces = turnOn;
        Debug.Log(turnOn ? "Luz encendida" : "Luz apagada");
    }

    private void CacheLights()
    {
        if (lucesControladas != null)
            return;

        Light[] sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        List<Light> householdLights = new List<Light>();
        foreach (Light lightComponent in sceneLights)
        {
            if (lightComponent != null && lightComponent.type != LightType.Directional)
                householdLights.Add(lightComponent);
        }

        if (householdLights.Count == 0 && luz != null)
            householdLights.AddRange(luz.GetComponentsInChildren<Light>(true));

        if (householdLights.Count == 0)
            householdLights.AddRange(GetComponentsInChildren<Light>(true));

        lucesControladas = householdLights.ToArray();
    }
}
