using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlantGrowth : MonoBehaviour
{
    public enum Stage
    {
        Cylinder,
        Cube,
        Sphere
    }

    public Stage stage;

    public GameObject cubePrefab;
    public GameObject spherePrefab;
    public GameObject cylinderPrefab;

    public float growInterval = 1f;
    public float growAmount = 2f;

    public float cylinderMaxSize = 20f;
    public float cubeMaxSize = 30f;

    private float timer;

    void Update()
    {
        // Sphere does not grow
        if (stage == Stage.Sphere)
            return;

        timer += Time.deltaTime;

        if (timer >= growInterval)
        {
            timer = 0f;

            // Grow on all axes
            // Save the previous height
            float oldHeight = transform.localScale.y;

            // Grow in all directions
            transform.localScale += Vector3.one * growAmount;

            // Calculate how much taller it became
            float heightIncrease = transform.localScale.y - oldHeight;

            // Move it up by half the height increase
            transform.position += Vector3.up * (heightIncrease / 2f);
            // Keep the bottom on the ground
            transform.position += Vector3.up * (growAmount / 2f);
        }

        // Cylinder -> Cube
        if (stage == Stage.Cylinder && transform.localScale.x >= cylinderMaxSize)
        {
            GameObject cube = Instantiate(cubePrefab, transform.position, Quaternion.identity);

            // Keep the cylinder's size
            cube.transform.localScale = transform.localScale;

            PlantGrowth cubeScript = cube.GetComponent<PlantGrowth>();
            cubeScript.stage = Stage.Cube;

            Destroy(gameObject);
            return;
        }

        // Cube -> Sphere
        if (stage == Stage.Cube && transform.localScale.x >= cubeMaxSize)
        {
            GameObject sphere = Instantiate(spherePrefab, transform.position, Quaternion.identity);
            
            // Keep the cube's size
            sphere.transform.localScale = transform.localScale;

            PlantGrowth sphereScript = sphere.GetComponent<PlantGrowth>();
            sphereScript.stage = Stage.Sphere;

            // Spawn new cylinder on top
            Transform top = sphere.transform.Find("Top");

            if (top != null)
            {
                GameObject newCylinder = Instantiate(cylinderPrefab, top.position, Quaternion.identity);

                // Original cylinder size
                newCylinder.transform.localScale = new Vector3(10f, 10f, 10f);

                PlantGrowth cylScript = newCylinder.GetComponent<PlantGrowth>();
                cylScript.stage = Stage.Cylinder;
            }
            else
            {
                Debug.LogError("Sphere prefab is missing a child named 'Top'.");
            }

            Destroy(gameObject);
        }
    }
}